using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Grants;

/// <summary>
/// V1-IAM-025 (D5): PostgresBehaviouralRateSource.CountGrantedSinceAsync
/// filters (requester_user_id, permission_code, status = 'granted',
/// resolved_at) with no covering index before this migration — text-only,
/// no DB fixture needed for an index-only migration.
/// </summary>
public sealed class AuthorizationGrantsRateIndexMigrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Dir =
        Path.Combine(RepoRoot, "database", "migrations", "V1", "V1-IAM-025");
    private static readonly string Up =
        File.ReadAllText(Path.Combine(Dir, "052-authorization-grants-rate-index.up.sql"));
    private static readonly string Down =
        File.ReadAllText(Path.Combine(Dir, "052-authorization-grants-rate-index.down.sql"));

    [Fact]
    public void UpCreatesAPartialIndexOnTheGrantedRateFilter()
    {
        var flat = Regex.Replace(Up, @"\s+", " ");
        flat.Should().Contain(
            "CREATE INDEX ix_authorization_grants_granted_rate " +
            "ON identity.authorization_grants (requester_user_id, permission_code, resolved_at) " +
            "WHERE status = 'granted'");
    }

    [Fact]
    public void DownDropsTheIndex()
    {
        Down.Should().Contain("DROP INDEX IF EXISTS identity.ix_authorization_grants_granted_rate");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
