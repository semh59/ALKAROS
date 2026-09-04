using ALKAROS.Identity.Authorization.Delegations;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Delegations;

public sealed class PostgresAuthorizationDelegationRepositoryTests : IClassFixture<DelegationDatabase>
{
    private readonly DelegationDatabase _db;
    private readonly PostgresAuthorizationDelegationRepository _repository;
    private readonly DateTimeOffset _now = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);

    public PostgresAuthorizationDelegationRepositoryTests(DelegationDatabase db)
    {
        _db = db;
        _repository = new PostgresAuthorizationDelegationRepository(db.DataSource);
    }

    private DelegationRequest Request(string permission = "bills.comp", decimal limit = 200m, double expiresInHours = 2)
        => new(permission, Guid.NewGuid(), Guid.NewGuid(), limit, _now.AddHours(expiresInHours));

    [Fact]
    public async Task CreateStoresAndFindCoveringReturnsIt()
    {
        var request = Request();
        var created = await _repository.CreateAsync(request, _now);

        created.DelegationId.Should().NotBe(Guid.Empty);
        created.GrantedAt.Should().BeCloseTo(_now, TimeSpan.FromSeconds(2));

        var found = await _repository.FindCoveringAsync(
            request.GranteeUserId, "bills.comp", 150m, _now.AddMinutes(30));
        found.Should().NotBeNull();
        found!.DelegationId.Should().Be(created.DelegationId);
    }

    [Fact]
    public async Task TheDatabaseCheckAlsoRejectsASelfDelegation()
    {
        // Bypass DelegationRequest.Validate: the table's own CHECK must still refuse
        // grantee = delegator and expires_at <= granted_at.
        var user = Guid.NewGuid();
        await using var selfCommand = _db.DataSource.CreateCommand(
            """
            INSERT INTO identity.authorization_delegations
                (permission_code, grantee_user_id, delegator_user_id, limit_amount, expires_at)
            VALUES ('bills.comp', @u, @u, 100, now() + interval '1 hour');
            """);
        selfCommand.Parameters.AddWithValue("u", user);
        await FluentActions.Invoking(() => selfCommand.ExecuteNonQueryAsync())
            .Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task FindCoveringRespectsAmountExpiryAndRevocation()
    {
        var request = Request(limit: 100m, expiresInHours: 1);
        var created = await _repository.CreateAsync(request, _now);
        var grantee = request.GranteeUserId;

        (await _repository.FindCoveringAsync(grantee, "bills.comp", 100.01m, _now)).Should().BeNull("over the limit");
        (await _repository.FindCoveringAsync(grantee, "bills.comp", 50m, _now.AddHours(2))).Should().BeNull("expired");
        (await _repository.FindCoveringAsync(grantee, "bills.void", 50m, _now)).Should().BeNull("other permission");

        (await _repository.RevokeAsync(created.DelegationId, _now.AddMinutes(10))).Should().BeTrue();
        (await _repository.FindCoveringAsync(grantee, "bills.comp", 50m, _now.AddMinutes(30))).Should().BeNull("revoked");
        (await _repository.RevokeAsync(created.DelegationId, _now.AddMinutes(20))).Should().BeFalse("already revoked");
    }

    [Fact]
    public async Task FindCoveringReturnsTheNewestWhenSeveralApply()
    {
        var grantee = Guid.NewGuid();
        var older = await _repository.CreateAsync(
            new DelegationRequest("bills.discount", grantee, Guid.NewGuid(), 500m, _now.AddHours(3)), _now);
        var newer = await _repository.CreateAsync(
            new DelegationRequest("bills.discount", grantee, Guid.NewGuid(), 500m, _now.AddHours(4)),
            _now.AddMinutes(5));

        var found = await _repository.FindCoveringAsync(grantee, "bills.discount", 100m, _now.AddMinutes(10));
        found!.DelegationId.Should().Be(newer.DelegationId);
        found.DelegationId.Should().NotBe(older.DelegationId);
    }

    [Fact]
    public async Task ListActiveExcludesRevokedAndExpired()
    {
        var live = await _repository.CreateAsync(Request(permission: "cash.drawer", expiresInHours: 5), _now);
        var revoked = await _repository.CreateAsync(Request(permission: "cash.drawer", expiresInHours: 5), _now);
        await _repository.RevokeAsync(revoked.DelegationId, _now.AddMinutes(1));
        var expired = await _repository.CreateAsync(Request(permission: "cash.drawer", expiresInHours: 1), _now);

        var active = await _repository.ListActiveAsync(_now.AddHours(2));

        active.Select(d => d.DelegationId).Should().Contain(live.DelegationId);
        active.Select(d => d.DelegationId).Should().NotContain(new[] { revoked.DelegationId, expired.DelegationId });
    }
}

/// <summary>Own fixture: mutates the schema with the down migration.</summary>
public sealed class AuthorizationDelegationsDownMigrationTests : IClassFixture<DelegationDatabase>
{
    private readonly DelegationDatabase _db;

    public AuthorizationDelegationsDownMigrationTests(DelegationDatabase db) => _db = db;

    [Fact]
    public async Task DownDropsTheDelegationTableAndLeavesTheGrantsTable()
    {
        await _db.ApplyDownAsync();

        (await _db.RelationExistsAsync("identity.authorization_delegations")).Should().BeFalse();
        (await _db.RelationExistsAsync("identity.authorization_grants")).Should().BeTrue();
    }
}
