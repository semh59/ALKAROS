namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// The RPO/RTO data classes approved in <c>docs/recovery/rpo-rto-targets.md</c>
/// (V0-BKP-002). "In-progress orders (client side)" is excluded — it never
/// touches a backup artifact, it is the WaiterPwa offline queue.
/// </summary>
public enum DataClass
{
    /// <summary>Bills, payments, fiscal documents, audit logs. RPO 5 minutes via WAL archiving.</summary>
    Fiscal,

    /// <summary>Orders, kitchen, inventory, customer accounts. RPO 1 hour via hourly pg_dump.</summary>
    OrdersInventory,

    /// <summary>Settings, config. RPO 24 hours via daily pg_dump.</summary>
    Settings
}
