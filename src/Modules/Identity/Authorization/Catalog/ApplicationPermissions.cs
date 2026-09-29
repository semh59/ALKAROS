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
    /// <summary>
    /// V1-RMD-401: taking a payment on a bill (card/EFT tender, and the cash tender together with
    /// <see cref="CashDrawer"/>). Cashier tier by default; a business that hands its waiters a card terminal grants
    /// it to the waiter role through role management — no code change (PO decision 2026-09-28).
    /// </summary>
    public const string PaymentsTake = "payments.take";
    /// <summary>
    /// V1-RMD-236: closing a cash session with a supervisor override (bypassing
    /// the normal variance-tolerance check) — supervisor-tier, same family as
    /// <see cref="BillsVoid"/>/<see cref="BillsComp"/>/<see cref="BillsDiscount"/>.
    /// A plain cashier holding only <see cref="CashDrawer"/> must escalate.
    /// </summary>
    public const string CashSessionOverride = "cash.session.override";
    public const string ReportsView = "reports.view";
    /// <summary>
    /// V1-RMD-250: investigating/resolving a reconciliation discrepancy case
    /// — supervisor-tier, same family as
    /// <see cref="BillsVoid"/>/<see cref="BillsComp"/> (a per-case exception
    /// a supervisor already handles), distinct from merely reading a case
    /// (<see cref="ReportsView"/>).
    /// </summary>
    public const string ReconciliationManage = "reconciliation.manage";
    /// <summary>
    /// V1-RMD-251: acknowledging/escalating/suppressing/resolving an
    /// operational alert, or recording a manual health check — supervisor
    /// tier, same family as <see cref="ReconciliationManage"/> (a per-case
    /// exception a supervisor already handles), distinct from merely
    /// reading an alert/health-check (<see cref="ReportsView"/>).
    /// </summary>
    public const string ObservabilityManage = "observability.manage";
    /// <summary>
    /// V1-RMD-266: account recovery (revoke every session of a user, clear a
    /// lockout) and the other security administration operations —
    /// manager-exclusive, same tier as <see cref="IntegrationsManage"/> and
    /// <see cref="ReportsCloseDay"/>: there is no requester/approver
    /// dynamic, and a supervisor must not be able to lock a manager out.
    /// </summary>
    public const string SecurityManage = "security.manage";
    /// <summary>V12-QRT-003: configuring a third-party integration credential (e.g. the QR relay provider token) — manager-only, no escalation path.</summary>
    public const string IntegrationsManage = "integrations.manage";
    /// <summary>
    /// V1-RMD-249: opening/closing a business day (EOD) — a one-time-per-day,
    /// no-escalation-path manager action (same family as
    /// <see cref="IntegrationsManage"/>/<c>settings.manage</c>), distinct from
    /// merely viewing a report (<see cref="ReportsView"/>, Supervisor+).
    /// </summary>
    public const string ReportsCloseDay = "reports.close-day";
    /// <summary>
    /// V1-IAM-028: advancing a kitchen ticket/item one stage forward
    /// (Queued→Preparing→Ready→Served), split out of <see cref="OrdersSend"/>
    /// so a line-cook-only role (kitchen-staff) can hold it WITHOUT also being
    /// able to cancel a ticket — cancel/report-a-problem still require
    /// OrdersSend (KitchenOperationsEndpoints branches on the target state).
    /// Every existing FOH role keeps OrdersSend, so their behavior is
    /// unchanged; this is additive.
    /// </summary>
    public const string KitchenAdvance = "kitchen.advance";

    public const string RoleWaiter = "waiter";
    public const string RoleCashier = "cashier";
    public const string RoleSupervisor = "supervisor";
    public const string RoleManager = "manager";
    /// <summary>V1-IAM-028: line kitchen staff ("Mutfak Personeli") — holds only <see cref="KitchenAdvance"/>, defined and seeded in migration 109, not in <see cref="RoleGrants"/> below (that dictionary only covers the four FOH roles; kitchen-staff's single grant is seeded directly by the migration).</summary>
    public const string RoleKitchenStaff = "kitchen-staff";

    /// <summary>Every code this catalog introduces, in seed order.</summary>
    public static readonly IReadOnlyList<string> Codes = new[]
    {
        OrdersCreate, OrdersSend, TablesStatus, TablesReserve, TablesTransfer,
        TablesMerge, FloorplanManage, BillsSplit, BillsVoid, BillsComp,
        BillsDiscount, CashDrawer, ReportsView,
        OrdersTransferServer, OrdersTransferServerAny,
        IntegrationsManage, KitchenAdvance, CashSessionOverride, ReportsCloseDay,
        ReconciliationManage, ObservabilityManage, SecurityManage, PaymentsTake,
    };

    // orders.transfer-server (self hand-off) sits alongside orders.create/
    // send in every role's outright grant — the same two-tier model
    // competitor POS systems use (Toast's "Change Server", Lightspeed's
    // "Table Ownership": self-transfer needs no manager, transferring
    // someone else's tables does). orders.transfer-server-any is the
    // broader tier, granted only at the CashierFloorSet level and up.
    // kitchen.advance (V1-IAM-028) joins this base set too: every FOH role
    // that can already fire an order to the kitchen can also advance the
    // resulting ticket, same as before the split — only the new
    // kitchen-staff role is limited to kitchen.advance alone.
    private static readonly string[] EveryRoleTakesOrders =
        { OrdersCreate, OrdersSend, TablesStatus, OrdersTransferServer, KitchenAdvance };

    private static readonly string[] CashierFloorSet =
        { TablesReserve, TablesTransfer, TablesMerge, BillsSplit, CashDrawer, OrdersTransferServerAny, PaymentsTake };

    private static readonly string[] SupervisorEscalations =
        {
            FloorplanManage, ReportsView, BillsVoid, BillsComp, BillsDiscount,
            CashSessionOverride, ReconciliationManage, ObservabilityManage,
        };

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
                EveryRoleTakesOrders.Concat(CashierFloorSet).Concat(SupervisorEscalations)
                    .Append(IntegrationsManage).Append(ReportsCloseDay).Append(SecurityManage),
                StringComparer.Ordinal),
        };
}
