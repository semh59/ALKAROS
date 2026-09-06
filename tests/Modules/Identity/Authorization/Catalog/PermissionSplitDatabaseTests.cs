using ALKAROS.Identity.Authorization.Catalog;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Catalog;

/// <summary>
/// Applies the real 005 -> 008 -> 042 -> 043 -> 049 identity migration chain to
/// a throw-away Postgres database and asserts the seeded rows match
/// <see cref="ApplicationPermissions"/> with the transitional
/// <c>pos.cashier.mutate</c> alias already dropped. Needs Postgres (CI Postgres
/// leg).
/// </summary>
public sealed class PermissionSplitDatabaseTests : IClassFixture<PermissionSplitDatabase>
{
    private readonly PermissionSplitDatabase _db;

    public PermissionSplitDatabaseTests(PermissionSplitDatabase db) => _db = db;

    [Fact]
    public async Task AllFifteenCodesAreSeeded()
    {
        foreach (var code in ApplicationPermissions.Codes)
            (await _db.PermissionCountAsync(code)).Should().Be(1, "'{0}' must be seeded once", code);
    }

    [Fact]
    public async Task WaiterRoleExistsAndHoldsExactlyOrderTakingTableStatusAndSelfTransfer()
    {
        (await _db.RoleCountAsync(ApplicationPermissions.RoleWaiter)).Should().Be(1);

        var granted = await _db.PermissionCodesForRoleAsync(ApplicationPermissions.RoleWaiter);

        granted.Should().BeEquivalentTo(new[]
        {
            ApplicationPermissions.OrdersCreate,
            ApplicationPermissions.OrdersSend,
            ApplicationPermissions.TablesStatus,
            ApplicationPermissions.OrdersTransferServer,
        });
        granted.Should().NotContain("pos.cashier.mutate");
    }

    [Fact]
    public async Task CashierNoLongerHoldsTheMutateAliasButKeepsReserveNotVoid()
    {
        var granted = await _db.PermissionCodesForRoleAsync(ApplicationPermissions.RoleCashier);

        granted.Should().NotContain("pos.cashier.mutate");
        granted.Should().Contain(ApplicationPermissions.TablesReserve);
        granted.Should().Contain(ApplicationPermissions.CashDrawer);
        granted.Should().Contain(ApplicationPermissions.OrdersTransferServerAny);
        granted.Should().NotContain(ApplicationPermissions.BillsVoid);
        granted.Should().NotContain(ApplicationPermissions.BillsComp);
        granted.Should().NotContain(ApplicationPermissions.BillsDiscount);
        granted.Should().NotContain(ApplicationPermissions.FloorplanManage);
    }

    [Fact]
    public async Task SupervisorHoldsEscalationsButNeitherTheAliasNorCatalog()
    {
        var granted = await _db.PermissionCodesForRoleAsync(ApplicationPermissions.RoleSupervisor);

        granted.Should().NotContain("pos.cashier.mutate");
        granted.Should().Contain(ApplicationPermissions.BillsVoid);
        granted.Should().Contain(ApplicationPermissions.BillsComp);
        granted.Should().Contain(ApplicationPermissions.BillsDiscount);
        granted.Should().Contain(ApplicationPermissions.FloorplanManage);
        granted.Should().Contain(ApplicationPermissions.ReportsView);
        granted.Should().NotContain("catalog.manage");
    }

    [Fact]
    public async Task ManagerKeepsCatalogManageFromMigration042ButNotTheAlias()
    {
        var granted = await _db.PermissionCodesForRoleAsync(ApplicationPermissions.RoleManager);

        granted.Should().Contain("catalog.manage");
        granted.Should().Contain(ApplicationPermissions.BillsVoid);
        granted.Should().NotContain("pos.cashier.mutate");
    }

    [Fact]
    public async Task SeededGrantsMatchTheCatalogRoleMapForEveryRole()
    {
        foreach (var (role, expected) in ApplicationPermissions.RoleGrants)
        {
            var granted = await _db.PermissionCodesForRoleAsync(role);
            var granular = granted.Where(c => ApplicationPermissions.Codes.Contains(c));

            granular.Should().BeEquivalentTo(expected,
                "role '{0}' granular grants must equal the catalog map", role);
        }
    }
}

/// <summary>
/// Own fixture instance: this class mutates the database by applying the down
/// migrations, so it must not share with <see cref="PermissionSplitDatabaseTests"/>.
/// </summary>
public sealed class PermissionSplitDownMigrationTests : IClassFixture<PermissionSplitDatabase>
{
    private readonly PermissionSplitDatabase _db;

    public PermissionSplitDownMigrationTests(PermissionSplitDatabase db) => _db = db;

    [Fact]
    public async Task Down049Then043RestoreTheAliasDropTheGranularSetAndLeave042Alone()
    {
        await _db.ApplyDownSplitAsync();

        foreach (var code in ApplicationPermissions.Codes)
            (await _db.PermissionCountAsync(code)).Should().Be(0, "043 down must drop '{0}'", code);

        (await _db.RoleCountAsync(ApplicationPermissions.RoleWaiter)).Should().Be(0);

        (await _db.RoleCountAsync(ApplicationPermissions.RoleCashier)).Should().Be(1);
        // 049 down re-creates the alias and re-grants it to cashier/supervisor/manager.
        (await _db.PermissionCountAsync("pos.cashier.mutate")).Should().Be(1);
        (await _db.PermissionCodesForRoleAsync(ApplicationPermissions.RoleCashier))
            .Should().Contain("pos.cashier.mutate");
        (await _db.PermissionCountAsync("catalog.manage")).Should().Be(1);
    }
}
