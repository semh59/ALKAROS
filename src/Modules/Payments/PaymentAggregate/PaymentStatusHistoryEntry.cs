namespace ALKAROS.Payments.PaymentAggregate;

/// <summary>
/// An immutably recorded Payment status change (payments.payment_status_history).
/// Every Payment transition appends one entry with the previous and next
/// canonical state, optional reason and acting user — the same audit-first
/// shape as orders.order_status_history (OrderStatusHistoryEntry).
/// </summary>
public sealed class PaymentStatusHistoryEntry
{
    public PaymentStatusHistoryEntry(
        Guid id,
        Guid paymentId,
        PaymentStatus oldStatus,
        PaymentStatus newStatus,
        string? reason = null,
        Guid? changedBy = null,
        DateTimeOffset? changedAt = null)
    {
        Id = id;
        PaymentId = paymentId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Reason = reason;
        ChangedBy = changedBy;
        ChangedAt = changedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public Guid PaymentId { get; }
    public PaymentStatus OldStatus { get; }
    public PaymentStatus NewStatus { get; }
    public string? Reason { get; }
    public Guid? ChangedBy { get; }
    public DateTimeOffset ChangedAt { get; }
}
