namespace ALKAROS.Identity.Authorization.Catalog;

/// <summary>
/// Canonical application permission catalog (V1-IAM-017). Splits the historical
/// coarse <c>pos.cashier.mutate</c> grant into one code per protected command
/// family, per <c>docs/domain/authorization-model.md</c> §2-3.
///
/// This wave only seeds the vocabulary and the role grants. Experience endpoints
/// keep checking <c>pos.cashier.mutate</c> until V1-IAM-024 re-points them; the
/// grant map below is the single source both the migration and V1-IAM-024 read,
/// so flipping an endpoint to a granular code cannot desync from the seed.
/// </summary>
public static class ApplicationPermissions
{
    public const string OrdersCreate = "orders.create";
    public const string OrdersSend = "orders.send";
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

    /// <summary>Retained transitional alias; removed in V1-IAM-024.</summary>
    public const string PosCashierMutateAlias = "pos.cashier.mutate";

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
    };

    private static readonly string[] EveryRoleTakesOrders =
        { OrdersCreate, OrdersSend, TablesStatus };

    private static readonly string[] CashierFloorSet =
        { TablesReserve, TablesTransfer, TablesMerge, BillsSplit, CashDrawer };

    private static readonly string[] SupervisorEscalations =
        { FloorplanManage, ReportsView, BillsVoid, BillsComp, BillsDiscount };

    /// <summary>
    /// Granular grants held OUTRIGHT by each role (model §3). Absence from a
    /// role's set means the action raises an authorization request (V1-IAM-019),
    /// not that it is forbidden. <see cref="PosCashierMutateAlias"/> is granted
    /// separately by the migration to every role except <see cref="RoleWaiter"/>.
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
            [RoleManager] = new HashSet<string>(
                EveryRoleTakesOrders.Concat(CashierFloorSet).Concat(SupervisorEscalations),
                StringComparer.Ordinal),
        };

    /// <summary>
    /// Roles that keep the <see cref="PosCashierMutateAlias"/> grant through the
    /// transition. <see cref="RoleWaiter"/> never holds it — that is what stops a
    /// waiter reserving a table at any terminal before V1-IAM-024 lands.
    /// </summary>
    public static readonly IReadOnlySet<string> RolesWithMutateAlias =
        new HashSet<string>(new[] { RoleCashier, RoleSupervisor, RoleManager }, StringComparer.Ordinal);
}
