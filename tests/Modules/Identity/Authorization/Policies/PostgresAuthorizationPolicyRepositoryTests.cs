using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Policies;

/// <summary>
/// Exercises <see cref="PostgresAuthorizationPolicyRepository"/> against the real
/// 005..044 migration chain. Needs Postgres (CI Postgres leg).
/// </summary>
public sealed class PostgresAuthorizationPolicyRepositoryTests : IClassFixture<PolicyDatabase>
{
    private readonly PolicyDatabase _db;
    private readonly PostgresAuthorizationPolicyRepository _repository;
    private readonly Guid _actor = Guid.NewGuid();

    public PostgresAuthorizationPolicyRepositoryTests(PolicyDatabase db)
    {
        _db = db;
        _repository = new PostgresAuthorizationPolicyRepository(db.DataSource);
    }

    [Fact]
    public async Task GetReturnsNullForAnUnconfiguredScope()
    {
        (await _repository.GetAsync("catalog.manage", "waiter")).Should().BeNull();
    }

    [Fact]
    public async Task UpsertInsertsThenReadsBack()
    {
        var draft = new AuthorizationPolicy(
            Guid.Empty, "bills.comp", "cashier", PolicyMode.AutoWithin, 150m, 2, 28800, 1);

        var stored = await _repository.UpsertAsync(draft, expectedRowVersion: null, _actor);

        stored.PolicyId.Should().NotBe(Guid.Empty);
        stored.RowVersion.Should().Be(1);
        stored.LimitAmount.Should().Be(150m);

        var read = await _repository.GetAsync("bills.comp", "cashier");
        read.Should().BeEquivalentTo(stored);
    }

    [Fact]
    public async Task UpsertReplaceBumpsRowVersionWhenTheExpectedVersionMatches()
    {
        var v1 = await _repository.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.discount", "supervisor", PolicyMode.AlwaysDeny, null, null, null, 1),
            expectedRowVersion: null, _actor);

        var v2 = await _repository.UpsertAsync(
            v1 with { Mode = PolicyMode.AlwaysAllow },
            expectedRowVersion: v1.RowVersion, _actor);

        v2.RowVersion.Should().Be(2);
        v2.Mode.Should().Be(PolicyMode.AlwaysAllow);
    }

    [Fact]
    public async Task UpsertReplaceWithAStaleVersionThrowsAndLeavesTheRowUntouched()
    {
        var v1 = await _repository.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "tables.reserve", "waiter", PolicyMode.AlwaysDeny, null, null, null, 1),
            expectedRowVersion: null, _actor);

        await FluentActions
            .Invoking(() => _repository.UpsertAsync(
                v1 with { Mode = PolicyMode.AlwaysAllow }, expectedRowVersion: 99L, _actor))
            .Should().ThrowAsync<AuthorizationPolicyConcurrencyException>();

        (await _repository.GetAsync("tables.reserve", "waiter"))!.Mode.Should().Be(PolicyMode.AlwaysDeny);
    }

    [Fact]
    public async Task UpsertReplaceOfADeletedPolicyThrowsInsteadOfSilentlyRecreatingIt()
    {
        var v1 = await _repository.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.comp", "waiter", PolicyMode.AlwaysDeny, null, null, null, 1),
            expectedRowVersion: null, _actor);

        (await _repository.DeleteAsync("bills.comp", "waiter")).Should().BeTrue();

        await FluentActions
            .Invoking(() => _repository.UpsertAsync(
                v1 with { Mode = PolicyMode.AlwaysAllow }, expectedRowVersion: v1.RowVersion, _actor))
            .Should().ThrowAsync<AuthorizationPolicyConcurrencyException>();

        (await _repository.GetAsync("bills.comp", "waiter")).Should().BeNull();
    }

    [Fact]
    public async Task UpsertRejectsAnInconsistentPolicyBeforeTouchingTheDatabase()
    {
        await FluentActions
            .Invoking(() => _repository.UpsertAsync(
                new AuthorizationPolicy(Guid.Empty, "bills.void", "waiter", PolicyMode.AutoWithin, 100m, null, 60, 1),
                expectedRowVersion: null, _actor))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TheDatabaseCheckConstraintAlsoRejectsAnInconsistentPolicy()
    {
        // Bypass AuthorizationPolicy.Validate by writing raw SQL: the table's
        // own CHECK must still refuse it.
        await using var command = _db.DataSource.CreateCommand(
            """
            INSERT INTO identity.authorization_policies (permission_code, role_code, mode, limit_amount)
            VALUES ('bills.void', 'manager', 'auto_within', 100);
            """);

        await FluentActions.Invoking(() => command.ExecuteNonQueryAsync())
            .Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact]
    public async Task ListIsOrderedByPermissionThenRole()
    {
        await _repository.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "cash.drawer", "supervisor", PolicyMode.AlwaysAllow, null, null, null, 1),
            null, _actor);
        await _repository.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.void", "supervisor", PolicyMode.AlwaysDeny, null, null, null, 1),
            null, _actor);
        await _repository.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.void", "cashier", PolicyMode.AlwaysDeny, null, null, null, 1),
            null, _actor);

        var listed = (await _repository.ListAsync())
            .Select(p => (p.PermissionCode, p.RoleCode))
            .ToList();

        listed.Should().ContainInOrder(
            ("bills.void", "cashier"), ("bills.void", "supervisor"), ("cash.drawer", "supervisor"));
    }

    [Fact]
    public async Task DeleteRemovesTheRowAndIsFalseWhenNothingMatched()
    {
        await _repository.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "reports.view", "cashier", PolicyMode.AlwaysDeny, null, null, null, 1),
            null, _actor);

        (await _repository.DeleteAsync("reports.view", "cashier")).Should().BeTrue();
        (await _repository.GetAsync("reports.view", "cashier")).Should().BeNull();
        (await _repository.DeleteAsync("reports.view", "cashier")).Should().BeFalse();
    }
}

/// <summary>Own fresh fixture: asserts the shipped shape before any mutation.</summary>
public sealed class PolicyDatabaseShapeTests : IClassFixture<PolicyDatabase>
{
    private readonly PolicyDatabase _db;

    public PolicyDatabaseShapeTests(PolicyDatabase db) => _db = db;

    [Fact]
    public async Task MigrationCreatesTheTableAndItStartsEmpty()
    {
        (await _db.TableExistsAsync("identity.authorization_policies")).Should().BeTrue();
        (await new PostgresAuthorizationPolicyRepository(_db.DataSource).ListAsync()).Should().BeEmpty();
    }
}

/// <summary>Own fixture: mutates the schema by applying the down migration.</summary>
public sealed class AuthorizationPoliciesDownMigrationTests : IClassFixture<PolicyDatabase>
{
    private readonly PolicyDatabase _db;

    public AuthorizationPoliciesDownMigrationTests(PolicyDatabase db) => _db = db;

    [Fact]
    public async Task DownDropsTheTableAndLeavesTheRoleCatalogIntact()
    {
        await _db.ApplyDownAsync();

        (await _db.TableExistsAsync("identity.authorization_policies")).Should().BeFalse();
        (await _db.TableExistsAsync("identity.role_permissions")).Should().BeTrue();
    }
}
