namespace ALKAROS.Clients.WaiterPwa.OrderStatus;

/// <summary>
/// Domain model for item status within an order tracked by a waiter (V1-WTR-003, PDF:I.8, PDF:I.16).
/// </summary>
public sealed record WaiterTicketItemProgress(
    Guid ItemId,
    string ItemName,
    int Quantity,
    string TicketStatus,
    bool IsCancelled,
    DateTimeOffset? ReadyAt);

/// <summary>
/// Status view of an active order tracked by a waiter (V1-WTR-003).
/// </summary>
public sealed record WaiterOrderStatusItem(
    Guid OrderId,
    string TableNumber,
    string OrderStatus,
    DateTimeOffset CreatedAt,
    IReadOnlyList<WaiterTicketItemProgress> Items)
{
    public bool HasReadyItems => Items.Any(i => string.Equals(i.TicketStatus, "Ready", StringComparison.OrdinalIgnoreCase));
    public bool HasCancelledItems => Items.Any(i => i.IsCancelled);
}

/// <summary>
/// Full reactive state of the Waiter PWA Order Status view (V1-WTR-003).
/// </summary>
public sealed record WaiterOrderStatusState(
    IReadOnlyList<WaiterOrderStatusItem> Orders,
    bool IsConnected,
    bool IsStale,
    DateTimeOffset LastSyncedAt);
