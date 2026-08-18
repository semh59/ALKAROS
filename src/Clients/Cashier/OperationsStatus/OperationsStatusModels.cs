namespace ALKAROS.Clients.Cashier.OperationsStatus;

/// <summary>
/// Domain model for linked Order and Bill operational state (V1-CUI-003, PDF:I.16-I.19).
/// </summary>
public sealed record OrderBillStatusView(
    Guid OrderId,
    Guid BillId,
    string TableNumber,
    decimal BillTotal,
    decimal PaidAmount,
    string BillStatus,
    IReadOnlyList<KitchenTicketProgressView> Tickets)
{
    public decimal RemainingAmount => BillTotal - PaidAmount;
    public bool IsFullyPaid => PaidAmount >= BillTotal;
}

/// <summary>
/// Status view of a kitchen ticket belonging to an order (V1-CUI-003).
/// </summary>
public sealed record KitchenTicketProgressView(
    Guid TicketId,
    string StationName,
    string TicketStatus,
    int TotalItems,
    int ReadyItems,
    IReadOnlyList<PrintJobStatusView> PrintJobs);

/// <summary>
/// Status of a physical print job in the station queue (V1-CUI-003).
/// </summary>
public sealed record PrintJobStatusView(
    Guid PrintJobId,
    string StationName,
    string Status,
    int RetryCount,
    string? LastError,
    bool CanReprint);

/// <summary>
/// Request to trigger an authorized reprint for a failed/unknown print job (V1-CUI-003, V1-KIT-004).
/// </summary>
public sealed record RequestReprintCommand(
    Guid PrintJobId,
    Guid OperatorId,
    string Reason,
    bool HasReprintPermission);
