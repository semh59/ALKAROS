using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Behavioural;

public sealed class BehaviouralTighteningMigrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Dir =
        Path.Combine(RepoRoot, "database", "migrations", "V1", "V1-IAM-023");
    private static readonly string Up =
        File.ReadAllText(Path.Combine(Dir, "048-behavioural-tightening.up.sql"));
    private static readonly string Down =
        File.ReadAllText(Path.Combine(Dir, "048-behavioural-tightening.down.sql"));

    [Fact]
    public void UpCreatesTheTableWithCountAndClearChecks()
    {
        Up.Should().Contain("CREATE TABLE identity.behavioural_tightenings");
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain("CHECK (recent_count >= 0 AND baseline_per_window >= 0 AND trigger_ratio >= 0)");
        flat.Should().Contain("(cleared_at IS NULL) = (cleared_by_user_id IS NULL)");
        flat.Should().Contain("cleared_at IS NULL OR cleared_at >= triggered_at");
    }

    [Fact]
    public void UpHasAPartialUniqueIndexOnTheOpenRow()
    {
        Up.Should().Contain("uq_behavioural_tightenings_active");
        Up.Should().Contain("(user_id, permission_code)");
        Up.Should().Contain("WHERE cleared_at IS NULL");
    }

    [Fact]
    public void UpInstallsTheAppendOnceClearTrigger()
    {
        Up.Should().Contain("enforce_behavioural_tightening_transition");
        Up.Should().Contain("BEFORE UPDATE OR DELETE ON identity.behavioural_tightenings");
        Up.Should().Contain("cannot be deleted");
        Up.Should().Contain("already cleared");
    }

    [Fact]
    public void UpShipsNoSeedRows()
        => Up.Should().NotContain("INSERT INTO");

    [Fact]
    public void DownDropsTheTriggerFunctionAndTable()
    {
        Down.Should().Contain("DROP TRIGGER IF EXISTS trg_behavioural_tightenings_transition");
        Down.Should().Contain("DROP FUNCTION IF EXISTS identity.enforce_behavioural_tightening_transition");
        Down.Should().Contain("DROP TABLE IF EXISTS identity.behavioural_tightenings");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
