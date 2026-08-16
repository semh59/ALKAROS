namespace ALKAROS.Orders.OrderAggregate;

/// <summary>
/// An immutably recorded order status change (orders.order_status_history,
/// PDF:III.6.4). Every order transition appends one entry with the previous
/// and next canonical state, optional reason and acting user (PDF:III.1.7
/// audit-first; V0-DOM-006 void audit requirement).
/// </summary>
public sealed class OrderStatusHistoryEntry
{
    public OrderStatusHistoryEntry(
        Guid id,
        Guid orderId,
        OrderState oldStatus,
        OrderState newStatus,
        string? reason = null,
        Guid? changedBy = null,
        DateTimeOffset? changedAt = null)
    {
        Id = id;
        OrderId = orderId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Reason = reason;
        ChangedBy = changedBy;
        ChangedAt = changedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public Guid OrderId { get; }
    public OrderState OldStatus { get; }
    public OrderState NewStatus { get; }
    public string? Reason { get; }
    public Guid? ChangedBy { get; }
    public DateTimeOffset ChangedAt { get; }
}