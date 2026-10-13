using ALKAROS.Identity.Authorization.Catalog;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Catalog;

public sealed class ApplicationPermissionsTests
{
    [Fact]
    public void CatalogHasTwentySixDistinctCodes()
    {
        // payments.take is the 23rd code, bills.cancel the 24th, then the two refund permissions.
        ApplicationPermissions.Codes.Should().HaveCount(26);
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
    [InlineData("security.manage")]
    [InlineData("orders.transfer-server-any")]
    [InlineData("payments.take")]
    [InlineData("bills.cancel")]
    [InlineData("payments.refund.request")]
    [InlineData("payments.refund.approve")]
    public void WaiterDoesNotHoldAnyEscalatedGrant(string code)
    {
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleWaiter]
            .Should().NotContain(code);
    }

    [Fact]
    public void PaymentsTakeIsHeldByCashierSupervisorAndManagerByDefault()
    {
        // V1-RMD-401: cashier tier by default; a business grants it to its waiters through role management.
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleCashier].Should().Contain(ApplicationPermissions.PaymentsTake);
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleSupervisor].Should().Contain(ApplicationPermissions.PaymentsTake);
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleManager].Should().Contain(ApplicationPermissions.PaymentsTake);
    }

    [Fact]
    public void RefundRequestIsCashierTierAndApprovalIsSupervisorTierAndUp()
    {
        var request = ApplicationPermissions.PaymentsRefundRequest;
        var approve = ApplicationPermissions.PaymentsRefundApprove;
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleCashier].Should().Contain(request).And.NotContain(approve);
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleSupervisor].Should().Contain(request).And.Contain(approve);
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleManager].Should().Contain(request).And.Contain(approve);
    }

    [Fact]
    public void BillsCancelIsHeldByCashierSupervisorAndManagerButNotWaiter()
    {
        // Cashier and manager cancel an unpaid bill with a reason.
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleCashier].Should().Contain(ApplicationPermissions.BillsCancel);
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleSupervisor].Should().Contain(ApplicationPermissions.BillsCancel);
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleManager].Should().Contain(ApplicationPermissions.BillsCancel);
        ApplicationPermissions.RoleGrants[ApplicationPermissions.RoleWaiter].Should().NotContain(ApplicationPermissions.BillsCancel);
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
        cashier.Should().NotContain(ApplicationPermissions.SecurityManage);
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
            supervisor.Append(ApplicationPermissions.IntegrationsManage).Append(ApplicationPermissions.ReportsCloseDay)
                .Append(ApplicationPermissions.SecurityManage));
        supervisor.Should().NotContain(ApplicationPermissions.IntegrationsManage);
        supervisor.Should().NotContain(ApplicationPermissions.ReportsCloseDay);
        supervisor.Should().NotContain(ApplicationPermissions.SecurityManage);
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
