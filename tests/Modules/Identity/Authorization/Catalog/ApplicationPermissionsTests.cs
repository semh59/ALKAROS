using ALKAROS.Identity.Authorization.Catalog;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Catalog;

public sealed class ApplicationPermissionsTests
{
    [Fact]
    public void CatalogHasFifteenDistinctCodes()
    {
        ApplicationPermissions.Codes.Should().HaveCount(15);
        ApplicationPermissions.Codes.Should().OnlyHaveUniqueItems();
        ApplicationPermissions.Codes.Should().NotContain("pos.cashier.mutate");
    }

    [Fact]
    public void WaiterHoldsOrderTakingTableStatusAndSelfTransferOnly()
    {
        var waiter = ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleWaiter];

        waiter.Should().BeEquivalentTo(new[]
        {
            ApplicationPermissions.OrdersCreate,
            ApplicationPermissions.OrdersSend,
            ApplicationPermissions.TablesStatus,
            ApplicationPermissions.OrdersTransferServer,
        });
    }

    [Theory]
    [InlineData("tables.reserve")]
    [InlineData("tables.transfer")]
    [InlineData("tables.merge")]
    [InlineData("bills.split")]
    [InlineData("bills.void")]
    [InlineData("bills.comp")]
    [InlineData("bills.discount")]
    [InlineData("floorplan.manage")]
    [InlineData("cash.drawer")]
    [InlineData("reports.view")]
    [InlineData("orders.transfer-server-any")]
    public void WaiterDoesNotHoldAnyEscalatedGrant(string code)
    {
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleWaiter]
            .Should().NotContain(code);
    }

    [Fact]
    public void CashierHoldsReserveButNotVoidCompOrDiscountOutright()
    {
        var cashier = ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleCashier];

        cashier.Should().Contain(ApplicationPermissions.TablesReserve);
        cashier.Should().Contain(ApplicationPermissions.CashDrawer);
        cashier.Should().Contain(ApplicationPermissions.OrdersTransferServerAny);
        cashier.Should().NotContain(ApplicationPermissions.BillsVoid);
        cashier.Should().NotContain(ApplicationPermissions.BillsComp);
        cashier.Should().NotContain(ApplicationPermissions.BillsDiscount);
        cashier.Should().NotContain(ApplicationPermissions.FloorplanManage);
        cashier.Should().NotContain(ApplicationPermissions.ReportsView);
    }

    [Fact]
    public void SupervisorIsAFloorRoleHoldingEscalationsButNotCatalog()
    {
        var supervisor = ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleSupervisor];

        supervisor.Should().Contain(ApplicationPermissions.BillsVoid);
        supervisor.Should().Contain(ApplicationPermissions.BillsComp);
        supervisor.Should().Contain(ApplicationPermissions.BillsDiscount);
        supervisor.Should().Contain(ApplicationPermissions.FloorplanManage);
        supervisor.Should().Contain(ApplicationPermissions.ReportsView);
        // catalog.manage is not an ApplicationPermissions code — it is owned by
        // migration 042 and only ever granted to `manager`. The catalog exposes
        // no supervisor path to it.
        ApplicationPermissions.Codes.Should().NotContain("catalog.manage");
    }

    [Fact]
    public void SupervisorAndManagerHoldTheSameGranularSet()
    {
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleSupervisor]
            .Should().BeEquivalentTo(ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleManager]);
    }

    [Fact]
    public void EveryRoleGrantIsAKnownCode()
    {
        foreach (var (role, grants) in ApplicationPermissions.RoleGrants)
        {
            grants.Should().OnlyContain(
                code => ApplicationPermissions.Codes.Contains(code),
                "role '{0}' must only be granted codes from the catalog", role);
        }
    }
}
