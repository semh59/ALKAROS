using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Policies;

/// <summary>Text-level checks on the V1-IAM-018 migration (044). No database.</summary>
public sealed class AuthorizationPoliciesMigrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Dir =
        Path.Combine(RepoRoot, "database", "migrations", "V1", "V1-IAM-018");
    private static readonly string Up =
        File.ReadAllText(Path.Combine(Dir, "044-authorization-policies.up.sql"));
    private static readonly string Down =
        File.ReadAllText(Path.Combine(Dir, "044-authorization-policies.down.sql"));

    [Fact]
    public void UpCreatesTheTableWithTheScopeUniquenessAndModeConstraint()
    {
        Up.Should().Contain("CREATE TABLE identity.authorization_policies");
        Up.Should().Contain("UNIQUE (permission_code, role_code)");
        Up.Should().Contain("mode IN ('always_deny', 'always_allow', 'auto_within')");
    }

    [Fact]
    public void UpRequiresAllThreeBoundsForAutoWithin()
    {
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain(
            "mode <> 'auto_within' OR (limit_amount IS NOT NULL AND max_count IS NOT NULL AND window_seconds IS NOT NULL)");
    }

    [Fact]
    public void UpRejectsNegativeBounds()
    {
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain("limit_amount IS NULL OR limit_amount >= 0");
        flat.Should().Contain("window_seconds IS NULL OR window_seconds > 0");
    }

    [Fact]
    public void UpShipsNoSeedRows()
    {
        Up.Should().NotContain("INSERT INTO");
    }

    [Fact]
    public void DownDropsTheTable()
    {
        Down.Should().Contain("DROP TABLE IF EXISTS identity.authorization_policies");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
