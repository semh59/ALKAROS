using ALKAROS.Identity.Authorization.Grants;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Grants;

public sealed class PostgresAuthorizationGrantRepositoryTests : IClassFixture<GrantDatabase>
{
    private readonly GrantDatabase _db;
    private readonly PostgresAuthorizationGrantRepository _repository;

    public PostgresAuthorizationGrantRepositoryTests(GrantDatabase db)
    {
        _db = db;
        _repository = new PostgresAuthorizationGrantRepository(db.DataSource);
    }

    private static AuthorizationGrant Pending(string key, decimal amount = 0m) => new(
        Guid.Empty, key, "bills.void", Guid.NewGuid(), "waiter",
        SubjectType: "bill", SubjectId: Guid.NewGuid(), SubjectServingUserId: null,
        Amount: amount, ReasonCode: "OperatorError", RequestedAt: DateTimeOffset.UtcNow,
        Status: GrantStatus.Pending, Path: null, ApproverUserId: null, ResolvedAt: null);

    [Fact]
    public async Task InsertPendingThenGetAndFindByKeyReturnIt()
    {
        var stored = await _repository.InsertAsync(Pending("k-insert"));

        stored.GrantId.Should().NotBe(Guid.Empty);
        stored.Status.Should().Be(GrantStatus.Pending);
        (await _repository.GetAsync(stored.GrantId)).Should().BeEquivalentTo(stored);
        (await _repository.FindByIdempotencyKeyAsync("k-insert")).Should().BeEquivalentTo(stored);
    }

    [Fact]
    public async Task DuplicateIdempotencyKeyIsRejectedByTheUniqueConstraint()
    {
        await _repository.InsertAsync(Pending("k-dupe"));

        await FluentActions.Invoking(() => _repository.InsertAsync(Pending("k-dupe")))
            .Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23505");
    }

    [Fact]
    public async Task ResolveMovesPendingToGrantedAndStampsTheResolution()
    {
        var pending = await _repository.InsertAsync(Pending("k-resolve"));
        var approver = Guid.NewGuid();

        var resolved = await _repository.ResolveAsync(
            pending.GrantId, GrantStatus.Granted, PolicyPath.Manual, approver);

        resolved.Status.Should().Be(GrantStatus.Granted);
        resolved.Path.Should().Be(PolicyPath.Manual);
        resolved.ApproverUserId.Should().Be(approver);
        resolved.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolvingAnAlreadyResolvedGrantThrows()
    {
        var pending = await _repository.InsertAsync(Pending("k-twice"));
        await _repository.ResolveAsync(pending.GrantId, GrantStatus.Denied, PolicyPath.Manual, Guid.NewGuid());

        await FluentActions
            .Invoking(() => _repository.ResolveAsync(pending.GrantId, GrantStatus.Granted, PolicyPath.Manual, Guid.NewGuid()))
            .Should().ThrowAsync<AuthorizationGrantAlreadyResolvedException>();
    }

    [Fact]
    public async Task TheTriggerBlocksMutatingARequestFieldAndBlocksDelete()
    {
        var pending = await _repository.InsertAsync(Pending("k-trigger"));

        await using (var badUpdate = _db.DataSource.CreateCommand(
            "UPDATE identity.authorization_grants SET amount = amount + 1 WHERE grant_id = @id;"))
        {
            badUpdate.Parameters.AddWithValue("id", pending.GrantId);
            await FluentActions.Invoking(() => badUpdate.ExecuteNonQueryAsync())
                .Should().ThrowAsync<PostgresException>();
        }

        await using var badDelete = _db.DataSource.CreateCommand(
            "DELETE FROM identity.authorization_grants WHERE grant_id = @id;");
        badDelete.Parameters.AddWithValue("id", pending.GrantId);
        await FluentActions.Invoking(() => badDelete.ExecuteNonQueryAsync())
            .Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task TheTriggerBlocksReResolvingViaRawSql()
    {
        var pending = await _repository.InsertAsync(Pending("k-reresolve"));
        await _repository.ResolveAsync(pending.GrantId, GrantStatus.Granted, PolicyPath.Auto, null);

        await using var command = _db.DataSource.CreateCommand(
            "UPDATE identity.authorization_grants SET status = 'denied' WHERE grant_id = @id;");
        command.Parameters.AddWithValue("id", pending.GrantId);
        await FluentActions.Invoking(() => command.ExecuteNonQueryAsync())
            .Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task CountAutoGrantsSinceCountsOnlyGrantedAutoRowsForTheRequesterAndPermission()
    {
        var user = Guid.NewGuid();
        var since = DateTimeOffset.UtcNow.AddHours(-1);

        // two granted-auto for (user, bills.comp)
        for (var i = 0; i < 2; i++)
        {
            var g = await _repository.InsertAsync(Pending($"k-count-{i}") with
            {
                RequesterUserId = user, PermissionCode = "bills.comp",
            });
            await _repository.ResolveAsync(g.GrantId, GrantStatus.Granted, PolicyPath.Auto, null);
        }
        // one granted-manual (must NOT count), one pending (must NOT count),
        // one for a different permission
        var manual = await _repository.InsertAsync(Pending("k-count-manual") with
        { RequesterUserId = user, PermissionCode = "bills.comp" });
        await _repository.ResolveAsync(manual.GrantId, GrantStatus.Granted, PolicyPath.Manual, Guid.NewGuid());
        await _repository.InsertAsync(Pending("k-count-pending") with
        { RequesterUserId = user, PermissionCode = "bills.comp" });
        var other = await _repository.InsertAsync(Pending("k-count-other") with
        { RequesterUserId = user, PermissionCode = "bills.discount" });
        await _repository.ResolveAsync(other.GrantId, GrantStatus.Granted, PolicyPath.Auto, null);

        (await _repository.CountAutoGrantsSinceAsync(user, "bills.comp", since)).Should().Be(2);
        (await _repository.CountAutoGrantsSinceAsync(user, "bills.comp", DateTimeOffset.UtcNow.AddYears(1)))
            .Should().Be(0);
    }

    [Fact]
    public async Task TheReportingViewRollsUpGrantedRowsByReasonRoleAndPath()
    {
        var g1 = await _repository.InsertAsync(Pending("k-rpt-1", amount: 100m) with { ReasonCode = "CustomerChange" });
        var g2 = await _repository.InsertAsync(Pending("k-rpt-2", amount: 50m) with { ReasonCode = "CustomerChange" });
        await _repository.ResolveAsync(g1.GrantId, GrantStatus.Granted, PolicyPath.Manual, Guid.NewGuid());
        await _repository.ResolveAsync(g2.GrantId, GrantStatus.Granted, PolicyPath.Manual, Guid.NewGuid());
        var denied = await _repository.InsertAsync(Pending("k-rpt-denied", amount: 999m));
        await _repository.ResolveAsync(denied.GrantId, GrantStatus.Denied, PolicyPath.Manual, Guid.NewGuid());

        await using var command = _db.DataSource.CreateCommand(
            """
            SELECT grant_count, amount_total
            FROM reporting.authorization_grant_daily
            WHERE permission_code = 'bills.void' AND reason_code = 'CustomerChange'
              AND requester_role_code = 'waiter' AND policy_path = 'manual';
            """);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt64(0).Should().Be(2);
        reader.GetDecimal(1).Should().Be(150m);
    }
}

/// <summary>Own fresh fixture: shipped shape before any write.</summary>
public sealed class GrantDatabaseShapeTests : IClassFixture<GrantDatabase>
{
    private readonly GrantDatabase _db;

    public GrantDatabaseShapeTests(GrantDatabase db) => _db = db;

    [Fact]
    public async Task MigrationCreatesTheTableAndTheViewAndTheTableStartsEmpty()
    {
        (await _db.RelationExistsAsync("identity.authorization_grants")).Should().BeTrue();
        (await _db.RelationExistsAsync("reporting.authorization_grant_daily")).Should().BeTrue();
        (await _db.GrantCountAsync()).Should().Be(0);
    }
}

/// <summary>Own fixture: mutates the schema with the down migration.</summary>
public sealed class AuthorizationGrantsDownMigrationTests : IClassFixture<GrantDatabase>
{
    private readonly GrantDatabase _db;

    public AuthorizationGrantsDownMigrationTests(GrantDatabase db) => _db = db;

    [Fact]
    public async Task DownDropsTheGrantSurfaceAndLeavesThePolicyTable()
    {
        await _db.ApplyDownAsync();

        (await _db.RelationExistsAsync("identity.authorization_grants")).Should().BeFalse();
        (await _db.RelationExistsAsync("reporting.authorization_grant_daily")).Should().BeFalse();
        (await _db.RelationExistsAsync("identity.authorization_policies")).Should().BeTrue();
    }
}
