using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Offline;

public sealed class OfflineAuthorityBudgetMigrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Dir =
        Path.Combine(RepoRoot, "database", "migrations", "V1", "V1-IAM-022");
    private static readonly string Up =
        File.ReadAllText(Path.Combine(Dir, "047-offline-authority-budget.up.sql"));
    private static readonly string Down =
        File.ReadAllText(Path.Combine(Dir, "047-offline-authority-budget.down.sql"));

    [Fact]
    public void UpCreatesTheThreeOfflineTables()
    {
        Up.Should().Contain("CREATE TABLE identity.offline_authority_budgets");
        Up.Should().Contain("CREATE TABLE identity.offline_authority_budget_lines");
        Up.Should().Contain("CREATE TABLE identity.offline_authority_replays");
    }

    [Fact]
    public void UpBoundsTheBudgetWindowAndTheLine()
    {
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain("UNIQUE (session_id)");
        flat.Should().Contain("CHECK (expires_at > issued_at)");
        flat.Should().Contain("CHECK (max_count >= 0)");
        flat.Should().Contain("CHECK (limit_amount IS NULL OR limit_amount >= 0)");
    }

    [Fact]
    public void UpLinksLinesToTheBudgetWithCascadeAndReplaysToGrantAndBudget()
    {
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain(
            "REFERENCES identity.offline_authority_budgets (budget_id) ON DELETE CASCADE");
        flat.Should().Contain("UNIQUE (grant_id)");
        flat.Should().Contain("REFERENCES identity.authorization_grants (grant_id)");
        flat.Should().Contain("REFERENCES identity.offline_authority_budgets (budget_id)");
        flat.Should().Contain("CHECK (offline_authorized_at <= reconciled_at)");
    }

    [Fact]
    public void UpDoesNotAlterTheGrantsTableAndShipsNoSeedRows()
    {
        Up.Should().NotContain("ALTER TABLE identity.authorization_grants");
        Up.Should().NotContain("INSERT INTO");
    }

    [Fact]
    public void DownDropsAllThreeTables()
    {
        Down.Should().Contain("DROP TABLE IF EXISTS identity.offline_authority_replays");
        Down.Should().Contain("DROP TABLE IF EXISTS identity.offline_authority_budget_lines");
        Down.Should().Contain("DROP TABLE IF EXISTS identity.offline_authority_budgets");
        Down.Should().NotContain("DROP TABLE IF EXISTS identity.authorization_grants");
        Down.Should().NotContain("ALTER TABLE");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
