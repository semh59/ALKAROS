namespace ALKAROS.Host.Experience.Orders.SentItemVoid;

/// <summary>
/// V1-IAM-027: command to void an Active item that has already been sent to
/// the kitchen (KitchenState ∈ {Sent, Preparing, Ready}) but not yet served.
/// Gated by the bills.void grant one layer up (the endpoint), not by this
/// command — by the time this reaches the store, the caller is authorized.
/// </summary>
public sealed record SentItemVoidCommand(
    Guid OrderId,
    Guid OrderItemId,
    long ExpectedRowVersion,
    Guid ActorId,
    string ReasonCode,
    string CorrelationId,
    string? Notes = null);

/// <summary>
/// <c>StockRestored</c> (V1-RMD-143 follow-up, 2026-09-09): true when the
/// item's own stock consumption was reversed because the kitchen had not
/// started on it yet (KitchenState was still Sent) — false either because
/// it had already reached Preparing/Ready (stays Waste, matching
/// docs/domain/void-complimentary-discount-policy.md's own Waste
/// definition) or because there was nothing to reverse.
/// </summary>
public sealed record SentItemVoidResult(
    Guid OrderId,
    Guid OrderItemId,
    long NewOrderRowVersion,
    decimal NewOrderTotal,
    bool KitchenTicketItemCancelled,
    bool BillLineConvertedToWaste,
    bool StockRestored,
    DateTimeOffset AppliedAt);

/// <summary>The item is still KitchenState.NotSent — the free pre-send void endpoint applies instead (V1-ORD-005).</summary>
public sealed class ItemNotYetSentException : Exception
{
    public ItemNotYetSentException(Guid orderItemId)
        : base($"Order item '{orderItemId}' has not been sent to the kitchen yet; use the pre-send void endpoint instead.")
    {
        OrderItemId = orderItemId;
    }

    public Guid OrderItemId { get; }
}

/// <summary>The item already reached KitchenState.Served — void no longer applies; that is comp/refund territory.</summary>
public sealed class ItemAlreadyServedException : Exception
{
    public ItemAlreadyServedException(Guid orderItemId)
        : base($"Order item '{orderItemId}' has already been served; void no longer applies.")
    {
        OrderItemId = orderItemId;
    }

    public Guid OrderItemId { get; }
}

/// <summary>The order item is billed on a Bill that is no longer Open/Reopened, so its line cannot be converted to waste.</summary>
public sealed class BillNotModifiableForWasteException : Exception
{
    public BillNotModifiableForWasteException(Guid billId, string billStatus)
        : base($"Bill '{billId}' is in state '{billStatus}' and cannot have an item removed for waste.")
    {
        BillId = billId;
        BillStatus = billStatus;
    }

    public Guid BillId { get; }
    public string BillStatus { get; }
}
