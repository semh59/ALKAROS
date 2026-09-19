using ALKAROS.Identity.Authorization.Catalog;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Catalog;

public sealed class ApplicationPermissionsTests
{
    [Fact]
    public void CatalogHasTwentyOneDistinctCodes()
    {
        // V1-RMD-251 added observability.manage, the 21st code.
        ApplicationPermissions.Codes.Should().HaveCount(21);
        ApplicationPermissions.Codes.Should().OnlyHaveUniqueItems();
        ApplicationPermissions.Codes.Should().NotContain("pos.cashier.mutate");
    }

    [Fact]
    public void WaiterHoldsOrderTakingTableStatusSelfTransferAndKitchenAdvanceOnly()
    {
        var waiter = ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleWaiter];

        waiter.Should().BeEquivalentTo(new[]
        {
            ApplicationPermissions.OrdersCreate,
            ApplicationPermissions.OrdersSend,
            ApplicationPermissions.TablesStatus,
            ApplicationPermissions.OrdersTransferServer,
            ApplicationPermissions.KitchenAdvance,
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
    [InlineData("cash.session.override")]
    [InlineData("reports.view")]
    [InlineData("reconciliation.manage")]
    [InlineData("observability.manage")]
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
        cashier.Should().NotContain(ApplicationPermissions.CashSessionOverride);
        cashier.Should().NotContain(ApplicationPermissions.ReconciliationManage);
        cashier.Should().NotContain(ApplicationPermissions.ObservabilityManage);
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
        supervisor.Should().Contain(ApplicationPermissions.CashSessionOverride);
        supervisor.Should().Contain(ApplicationPermissions.ReconciliationManage);
        supervisor.Should().Contain(ApplicationPermissions.ObservabilityManage);
        // catalog.manage is not an ApplicationPermissions code — it is owned by
        // migration 042 and only ever granted to `manager`. The catalog exposes
        // no supervisor path to it.
        ApplicationPermissions.Codes.Should().NotContain("catalog.manage");
    }

    [Fact]
    public void ManagerHoldsEverySupervisorGrantPlusIntegrationsManageAndReportsCloseDay()
    {
        // V12-QRT-003: the first manager-exclusive grant — every tier before
        // it was identical to supervisor's. integrations.manage (configuring
        // a third-party relay credential) is a one-time setup action with no
        // requester/approver dynamic, unlike bills.void/comp/discount, which
        // are per-transaction exceptions supervisor can already resolve.
        // V1-RMD-249 added reports.close-day (opening/closing a business
        // day) to the same manager-exclusive tier.
        var supervisor = ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleSupervisor];
        var manager = ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleManager];

        manager.Should().BeEquivalentTo(
            supervisor.Append(ApplicationPermissions.IntegrationsManage).Append(ApplicationPermissions.ReportsCloseDay));
        supervisor.Should().NotContain(ApplicationPermissions.IntegrationsManage);
        supervisor.Should().NotContain(ApplicationPermissions.ReportsCloseDay);
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
