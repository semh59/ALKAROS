using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Grants;

/// <summary>Text-level checks on the V1-IAM-019 migration (045). No database.</summary>
public sealed class AuthorizationGrantsMigrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Dir =
        Path.Combine(RepoRoot, "database", "migrations", "V1", "V1-IAM-019");
    private static readonly string Up =
        File.ReadAllText(Path.Combine(Dir, "045-authorization-grants.up.sql"));
    private static readonly string Down =
        File.ReadAllText(Path.Combine(Dir, "045-authorization-grants.down.sql"));

    [Fact]
    public void UpCreatesTheTableWithIdempotencyUniquenessAndTerminalStatusCheck()
    {
        Up.Should().Contain("CREATE TABLE identity.authorization_grants");
        Up.Should().Contain("UNIQUE (idempotency_key)");
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain("status IN ('pending', 'granted', 'denied')");
        flat.Should().Contain("policy_path IS NULL OR policy_path IN ('auto', 'delegation', 'manual')");
        flat.Should().Contain("(status = 'pending') = (resolved_at IS NULL)");
    }

    [Fact]
    public void UpInstallsTheAppendOnceTriggerAndFunction()
    {
        Up.Should().Contain("CREATE OR REPLACE FUNCTION identity.enforce_authorization_grant_transition()");
        Up.Should().Contain("BEFORE UPDATE OR DELETE ON identity.authorization_grants");
        Up.Should().Contain("rows cannot be deleted");
        Up.Should().Contain("only the resolution fields of an authorization grant may change");
    }

    [Fact]
    public void UpDefinesTheReportingProjectionOverGrantedRowsOnly()
    {
        Up.Should().Contain("CREATE SCHEMA IF NOT EXISTS reporting;");
        Up.Should().Contain("CREATE VIEW reporting.authorization_grant_daily");
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain("WHERE status = 'granted'");
        flat.Should().Contain("sum(amount) AS amount_total");
    }

    [Fact]
    public void DownRemovesTheViewTriggerFunctionAndTable()
    {
        Down.Should().Contain("DROP VIEW IF EXISTS reporting.authorization_grant_daily");
        Down.Should().Contain("DROP TRIGGER IF EXISTS trg_authorization_grants_transition");
        Down.Should().Contain("DROP FUNCTION IF EXISTS identity.enforce_authorization_grant_transition");
        Down.Should().Contain("DROP TABLE IF EXISTS identity.authorization_grants");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
