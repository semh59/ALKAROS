using System.IO;
using System.Text.RegularExpressions;
using ALKAROS.Identity.Authorization.Tests.Grants;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Delegations;

public sealed class AuthorizationDelegationMigrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Dir =
        Path.Combine(RepoRoot, "database", "migrations", "V1", "V1-IAM-021");
    private static readonly string Up =
        File.ReadAllText(Path.Combine(Dir, "046-authorization-delegations.up.sql"));
    private static readonly string Down =
        File.ReadAllText(Path.Combine(Dir, "046-authorization-delegations.down.sql"));

    [Fact]
    public void UpCreatesTheTableWithWindowLimitRevokeAndSelfChecks()
    {
        Up.Should().Contain("CREATE TABLE identity.authorization_delegations");
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain("CHECK (expires_at > granted_at)");
        flat.Should().Contain("CHECK (limit_amount >= 0)");
        flat.Should().Contain("CHECK (revoked_at IS NULL OR revoked_at >= granted_at)");
        flat.Should().Contain("CHECK (grantee_user_id <> delegator_user_id)");
    }

    [Fact]
    public void UpIndexesTheActiveLookup()
    {
        Up.Should().Contain("ix_authorization_delegations_active");
        Up.Should().Contain("WHERE revoked_at IS NULL");
    }

    [Fact]
    public void UpShipsNoSeedRows()
    {
        Up.Should().NotContain("INSERT INTO");
    }

    [Fact]
    public void DownDropsTheTable()
    {
        Down.Should().Contain("DROP TABLE IF EXISTS identity.authorization_delegations");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}

public sealed class AuthorizationGuardDatabaseTests : IClassFixture<DelegationDatabase>
{
    private readonly DelegationDatabase _db;

    public AuthorizationGuardDatabaseTests(DelegationDatabase db) => _db = db;

    private async Task<Guid> InsertGrantAsync(Guid requester, string status = "pending", Guid? approver = null)
    {
        await using var command = _db.DataSource.CreateCommand(
            """
            INSERT INTO identity.authorization_grants
                (idempotency_key, permission_code, requester_user_id, requester_role_code, amount, reason_code,
                 status, policy_path, approver_user_id, resolved_at)
            VALUES (@key, 'bills.comp', @requester, 'waiter', 10, 'CustomerChange',
                    @status, CASE WHEN @status = 'pending' THEN NULL ELSE 'manual' END, @approver,
                    CASE WHEN @status = 'pending' THEN NULL ELSE now() END)
            RETURNING grant_id;
            """);
        command.Parameters.AddWithValue("key", "guard-" + Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("requester", requester);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("approver", (object?)approver ?? DBNull.Value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task<Guid> InsertDelegationAsync()
    {
        await using var command = _db.DataSource.CreateCommand(
            """
            INSERT INTO identity.authorization_delegations
                (permission_code, grantee_user_id, delegator_user_id, limit_amount, expires_at)
            VALUES ('bills.comp', gen_random_uuid(), gen_random_uuid(), 100, now() + interval '2 hours')
            RETURNING delegation_id;
            """);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task AGrantCannotBeResolvedByItsOwnRequester()
    {
        var requester = Guid.NewGuid();
        var grantId = await InsertGrantAsync(requester);

        await FluentActions
            .Invoking(() => _db.ExecuteAsync(
                $"UPDATE identity.authorization_grants SET status = 'granted', policy_path = 'manual', approver_user_id = '{requester}', resolved_at = now() WHERE grant_id = '{grantId}';"))
            .Should().ThrowAsync<PostgresException>().Where(e => e.ConstraintName == "ck_authorization_grants_approver_not_requester");
        await FluentActions
            .Invoking(() => InsertGrantAsync(requester, "granted", requester))
            .Should().ThrowAsync<PostgresException>().Where(e => e.ConstraintName == "ck_authorization_grants_approver_not_requester");
    }

    [Theory]
    [InlineData("requester_role_code = 'manager'")]
    [InlineData("subject_type = 'OrderItem', subject_id = gen_random_uuid()")]
    [InlineData("subject_serving_user_id = gen_random_uuid()")]
    public async Task TheRoleAndSubjectOfAGrantRequestCannotChangeOnResolution(string change)
    {
        var grantId = await InsertGrantAsync(Guid.NewGuid());

        await FluentActions
            .Invoking(() => _db.ExecuteAsync(
                $"UPDATE identity.authorization_grants SET {change}, status = 'granted', policy_path = 'manual', approver_user_id = gen_random_uuid(), resolved_at = now() WHERE grant_id = '{grantId}';"))
            .Should().ThrowAsync<PostgresException>().WithMessage("*only the resolution fields*");
    }

    [Fact]
    public async Task AnOrdinaryResolutionByAnotherApproverStillWorks()
    {
        var grantId = await InsertGrantAsync(Guid.NewGuid());

        await _db.ExecuteAsync(
            $"UPDATE identity.authorization_grants SET status = 'granted', policy_path = 'manual', approver_user_id = gen_random_uuid(), resolved_at = now() WHERE grant_id = '{grantId}';");

        (await _db.ScalarAsync<string>($"SELECT status FROM identity.authorization_grants WHERE grant_id = '{grantId}';"))
            .Should().Be("granted");
    }

    [Fact]
    public async Task ADelegationCannotBeDeletedOrChangedExceptByItsOneRevocation()
    {
        var id = await InsertDelegationAsync();

        await FluentActions
            .Invoking(() => _db.ExecuteAsync($"UPDATE identity.authorization_delegations SET limit_amount = 5000 WHERE delegation_id = '{id}';"))
            .Should().ThrowAsync<PostgresException>().WithMessage("*only the revocation fields*");
        await FluentActions
            .Invoking(() => _db.ExecuteAsync($"UPDATE identity.authorization_delegations SET expires_at = expires_at + interval '10 days' WHERE delegation_id = '{id}';"))
            .Should().ThrowAsync<PostgresException>().WithMessage("*only the revocation fields*");
        await FluentActions
            .Invoking(() => _db.ExecuteAsync($"DELETE FROM identity.authorization_delegations WHERE delegation_id = '{id}';"))
            .Should().ThrowAsync<PostgresException>().WithMessage("*cannot be deleted*");

        await _db.ExecuteAsync($"UPDATE identity.authorization_delegations SET revoked_at = now(), revoked_by_user_id = gen_random_uuid() WHERE delegation_id = '{id}';");
        await FluentActions
            .Invoking(() => _db.ExecuteAsync($"UPDATE identity.authorization_delegations SET revoked_at = NULL WHERE delegation_id = '{id}';"))
            .Should().ThrowAsync<PostgresException>().WithMessage("*already revoked*");
    }
}

public sealed class WaiterDiscountPolicySeedTests : IClassFixture<GrantDatabase>
{
    private static readonly string Dir = Path.Combine(FindRoot(), "database", "migrations", "V1", "V1-RMD-674");
    private readonly GrantDatabase _db;

    public WaiterDiscountPolicySeedTests(GrantDatabase db) => _db = db;

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private Task<int> ApplyUpAsync() => _db.ExecuteAsync(File.ReadAllText(Path.Combine(Dir, "196-waiter-discount-policy.up.sql")));

    private Task<string> PolicyAsync() => _db.ScalarAsync<string>(
        "SELECT mode || ':' || limit_amount || ':' || max_count || ':' || window_seconds FROM identity.authorization_policies WHERE permission_code = 'bills.discount' AND role_code = 'waiter';");

    [Fact]
    public async Task TheSeedGivesAWaiterAHundredLiraDiscountLimitAndLeavesComplimentaryAndVoidAlone()
    {
        await ApplyUpAsync();
        await ApplyUpAsync();

        (await PolicyAsync()).Should().Be("auto_within:100.00:10:28800");
        (await _db.ScalarAsync<long>(
            "SELECT count(*) FROM identity.authorization_policies WHERE permission_code IN ('bills.comp', 'bills.void');"))
            .Should().Be(0);
    }

    [Fact]
    public async Task ARowAManagerAlreadySetIsNotOverwrittenAndDownRemovesOnlyTheSeededRow()
    {
        await _db.ExecuteAsync("DELETE FROM identity.authorization_policies;");
        await _db.ExecuteAsync(
            "INSERT INTO identity.authorization_policies (permission_code, role_code, mode, limit_amount, max_count, window_seconds) VALUES ('bills.discount', 'waiter', 'auto_within', 40, 3, 3600);");
        await ApplyUpAsync();
        (await PolicyAsync()).Should().Be("auto_within:40.00:3:3600");

        await _db.ExecuteAsync(File.ReadAllText(Path.Combine(Dir, "196-waiter-discount-policy.down.sql")));
        (await PolicyAsync()).Should().Be("auto_within:40.00:3:3600", "the manager's row is not the seeded one");

        await _db.ExecuteAsync("DELETE FROM identity.authorization_policies;");
        await ApplyUpAsync();
        await _db.ExecuteAsync(File.ReadAllText(Path.Combine(Dir, "196-waiter-discount-policy.down.sql")));
        (await _db.ScalarAsync<long>("SELECT count(*) FROM identity.authorization_policies;")).Should().Be(0);
    }
}
