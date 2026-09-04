using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
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
