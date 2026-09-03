using System.IO;
using System.Text.RegularExpressions;
using ALKAROS.Identity.Authorization.Catalog;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Catalog;

/// <summary>
/// Text-level checks that the V1-IAM-017 migration (043) stays in lock-step with
/// <see cref="ApplicationPermissions"/>. No database — this runs in every CI leg,
/// not only the Postgres one, so a seed drift fails fast.
/// </summary>
public sealed class PermissionSplitMigrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string MigrationDir =
        Path.Combine(RepoRoot, "database", "migrations", "V1", "V1-IAM-017");
    private static readonly string Up =
        File.ReadAllText(Path.Combine(MigrationDir, "043-authorization-permission-split.up.sql"));
    private static readonly string Down =
        File.ReadAllText(Path.Combine(MigrationDir, "043-authorization-permission-split.down.sql"));

    [Fact]
    public void UpInsertsEveryCatalogCodeExactlyOnce()
    {
        foreach (var code in ApplicationPermissions.Codes)
        {
            Regex.Count(Up, $"'{Regex.Escape(code)}'")
                .Should().BeGreaterThanOrEqualTo(1, "'{0}' must be inserted", code);
            Up.Should().Contain($"'{code}',", "'{0}' must be an INSERT VALUES row", code);
        }
    }

    [Fact]
    public void UpAddsTheWaiterRoleAndNoOtherRole()
    {
        Up.Should().MatchRegex(@"INSERT INTO identity\.roles[\s\S]*'waiter'");
        Up.Should().NotContain("'cashier', 'Cashier'");
        Up.Should().NotContain("'manager', 'Manager'");
    }

    [Fact]
    public void UpGrantsGranularCodesToWaiterButNeverTheMutateAlias()
    {
        // The waiter appears in the granular grant join...
        Up.Should().MatchRegex(@"r\.code IN \('waiter', 'cashier', 'supervisor', 'manager'\)");
        // ...but the pos.cashier.mutate grant is restricted to the other three.
        Up.Should().MatchRegex(
            @"p\.code = 'pos\.cashier\.mutate'\s*\r?\nWHERE r\.code IN \('cashier', 'supervisor', 'manager'\)");
        Up.Should().NotMatchRegex(@"'pos\.cashier\.mutate'[\s\S]{0,80}'waiter'");
    }

    [Fact]
    public void UpDoesNotReinsertMigration042OwnedRows()
    {
        Up.Should().NotContain("'catalog.manage'");

        // pos.cashier.mutate may only appear in the alias-grant JOIN, never in a
        // fresh permissions INSERT (that row is owned by migration 042).
        var permissionsInsert = Regex.Match(
            Up, @"INSERT INTO identity\.permissions[\s\S]*?;", RegexOptions.IgnoreCase).Value;
        permissionsInsert.Should().NotContain("pos.cashier.mutate");
    }

    [Fact]
    public void DownRemovesExactlyWhatUpAddedAndLeaves042Alone()
    {
        foreach (var code in ApplicationPermissions.Codes)
            Down.Should().Contain($"'{code}'", "down must drop '{0}'", code);

        Down.Should().Contain("DELETE FROM identity.roles");
        Down.Should().Contain("'waiter'");
        Down.Should().NotContain("'cashier'");
        Down.Should().NotContain("'pos.cashier.mutate'");
        Down.Should().NotContain("'catalog.manage'");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
