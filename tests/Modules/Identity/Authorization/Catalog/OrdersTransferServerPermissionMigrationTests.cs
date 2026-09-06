using System.IO;
using ALKAROS.Identity.Authorization.Catalog;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Catalog;

/// <summary>
/// Text-level checks that migration 055 (V1-RMD-111) stays in lock-step with
/// the two codes it adds to <see cref="ApplicationPermissions"/>. No
/// database — mirrors <see cref="PermissionSplitMigrationTests"/>'s approach
/// for migration 043.
/// </summary>
public sealed class OrdersTransferServerPermissionMigrationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string MigrationDir =
        Path.Combine(RepoRoot, "database", "migrations", "V1", "V1-RMD-111");
    private static readonly string Up =
        File.ReadAllText(Path.Combine(MigrationDir, "055-orders-transfer-server-permissions.up.sql"));
    private static readonly string Down =
        File.ReadAllText(Path.Combine(MigrationDir, "055-orders-transfer-server-permissions.down.sql"));

    private static readonly string[] CodesIntroducedByThisMigration =
    {
        ApplicationPermissions.OrdersTransferServer,
        ApplicationPermissions.OrdersTransferServerAny,
    };

    [Fact]
    public void UpInsertsBothCodesExactlyOnce()
    {
        foreach (var code in CodesIntroducedByThisMigration)
            Up.Should().Contain($"'{code}',", "'{0}' must be an INSERT VALUES row", code);
    }

    [Fact]
    public void UpGrantsSelfTransferToEveryRoleButAnyTransferOnlyToTheFloorTierAndUp()
    {
        Up.Should().MatchRegex(
            @"r\.code IN \('waiter', 'cashier', 'supervisor', 'manager'\)\s*\r?\n\s*AND p\.code = 'orders\.transfer-server'");
        Up.Should().MatchRegex(
            @"r\.code IN \('cashier', 'supervisor', 'manager'\)\s*\r?\n\s*AND p\.code = 'orders\.transfer-server-any'");
        Up.Should().NotMatchRegex(@"'waiter'[\s\S]{0,40}'orders\.transfer-server-any'");
    }

    [Fact]
    public void DownRemovesExactlyTheTwoCodesAndTheirGrants()
    {
        foreach (var code in CodesIntroducedByThisMigration)
            Down.Should().Contain($"'{code}'", "down must drop '{0}'", code);

        Down.Should().Contain("DELETE FROM identity.role_permissions");
        Down.Should().Contain("DELETE FROM identity.permissions");
        Down.Should().NotContain("identity.roles");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
