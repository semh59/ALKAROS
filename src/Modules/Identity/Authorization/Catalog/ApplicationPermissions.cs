namespace ALKAROS.Identity.Authorization.Catalog;

/// <summary>
/// Canonical application permission catalog (V1-IAM-017). Splits the historical
/// coarse <c>pos.cashier.mutate</c> grant into one code per protected command
/// family, per <c>docs/domain/authorization-model.md</c> §2-3.
///
/// Every Experience endpoint checks one of these codes (V1-IAM-024). The grant
/// map below is the single source the seed migration and the endpoints share,
/// so an endpoint's code cannot desync from what its role is granted.
/// </summary>
public static class ApplicationPermissions
{
    public const string OrdersCreate = "orders.create";
    public const string OrdersSend = "orders.send";
    public const string OrdersTransferServer = "orders.transfer-server";
    public const string OrdersTransferServerAny = "orders.transfer-server-any";
    public const string TablesStatus = "tables.status";
    public const string TablesReserve = "tables.reserve";
    public const string TablesTransfer = "tables.transfer";
    public const string TablesMerge = "tables.merge";
    public const string FloorplanManage = "floorplan.manage";
    public const string BillsSplit = "bills.split";
    public const string BillsVoid = "bills.void";
    public const string BillsComp = "bills.comp";
    public const string BillsDiscount = "bills.discount";
    public const string CashDrawer = "cash.drawer";
    public const string ReportsView = "reports.view";
    /// <summary>V12-QRT-003: configuring a third-party integration credential (e.g. the QR relay provider token) — manager-only, no escalation path.</summary>
    public const string IntegrationsManage = "integrations.manage";

    public const string RoleWaiter = "waiter";
    public const string RoleCashier = "cashier";
    public const string RoleSupervisor = "supervisor";
    public const string RoleManager = "manager";

    /// <summary>Every code this catalog introduces, in seed order.</summary>
    public static readonly IReadOnlyList<string> Codes = new[]
    {
        OrdersCreate, OrdersSend, TablesStatus, TablesReserve, TablesTransfer,
        TablesMerge, FloorplanManage, BillsSplit, BillsVoid, BillsComp,
        BillsDiscount, CashDrawer, ReportsView,
        OrdersTransferServer, OrdersTransferServerAny,
        IntegrationsManage,
    };

    // orders.transfer-server (self hand-off) sits alongside orders.create/
    // send in every role's outright grant — the same two-tier model
    // competitor POS systems use (Toast's "Change Server", Lightspeed's
    // "Table Ownership": self-transfer needs no manager, transferring
    // someone else's tables does). orders.transfer-server-any is the
    // broader tier, granted only at the CashierFloorSet level and up.
    private static readonly string[] EveryRoleTakesOrders =
        { OrdersCreate, OrdersSend, TablesStatus, OrdersTransferServer };

    private static readonly string[] CashierFloorSet =
        { TablesReserve, TablesTransfer, TablesMerge, BillsSplit, CashDrawer, OrdersTransferServerAny };

    private static readonly string[] SupervisorEscalations =
        { FloorplanManage, ReportsView, BillsVoid, BillsComp, BillsDiscount };

    /// <summary>
    /// Granular grants held OUTRIGHT by each role (model §3). Absence from a
    /// role's set means the action raises an authorization request (V1-IAM-019),
    /// not that it is forbidden.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RoleGrants =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [RoleWaiter] = new HashSet<string>(EveryRoleTakesOrders, StringComparer.Ordinal),
            [RoleCashier] = new HashSet<string>(
                EveryRoleTakesOrders.Concat(CashierFloorSet), StringComparer.Ordinal),
            [RoleSupervisor] = new HashSet<string>(
                EveryRoleTakesOrders.Concat(CashierFloorSet).Concat(SupervisorEscalations),
                StringComparer.Ordinal),
            // V12-QRT-003: the first manager-exclusive grant — every prior
            // tier here was identical to supervisor's. Configuring a
            // third-party relay credential is a one-time setup action with
            // no requester/approver dynamic (unlike bills.void/comp, which
            // are per-transaction exceptions a supervisor can already
            // resolve), so it sits a level above supervisor rather than
            // going through the grant-request escalation path.
            [RoleManager] = new HashSet<string>(
                EveryRoleTakesOrders.Concat(CashierFloorSet).Concat(SupervisorEscalations).Append(IntegrationsManage),
                StringComparer.Ordinal),
        };
}
