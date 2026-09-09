namespace ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;

/// <summary>
/// V1-RMD-137: the order exists but is not (or is no longer) in
/// PendingConfirmation — e.g. a concurrent request already accepted/rejected
/// it, or the caller targeted an order that was never held for confirmation
/// in the first place. Distinct from a row-version mismatch
/// (<see cref="ALKAROS.Orders.ItemExceptions.StaleOrderRowVersionException"/>),
/// which means "the caller's own copy is stale"; this means "the caller's
/// copy is current, but this action never applies to this order's state."
/// </summary>
public sealed class OrderNotAwaitingConfirmationException : Exception
{
    public OrderNotAwaitingConfirmationException(Guid orderId, string currentStatus)
        : base($"Order '{orderId}' is not awaiting confirmation (current status: {currentStatus}).")
    {
        OrderId = orderId;
        CurrentStatus = currentStatus;
    }

    public Guid OrderId { get; }
    public string CurrentStatus { get; }
}

/// <summary>
/// V1-RMD-137: a PendingConfirmation order is never expected to already have
/// a Bill (billing normally starts only after Accepted) — one existing
/// anyway is treated as a sign something unexpected already happened rather
/// than silently rejecting an order a cashier may already be working from.
/// </summary>
public sealed class OrderAlreadyBilledException : Exception
{
    public OrderAlreadyBilledException(Guid orderId)
        : base($"Order '{orderId}' already has a bill and cannot be rejected through this action.")
    {
        OrderId = orderId;
    }

    public Guid OrderId { get; }
}
