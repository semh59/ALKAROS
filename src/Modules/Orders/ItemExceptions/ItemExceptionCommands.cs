namespace ALKAROS.Orders.ItemExceptions;

/// <summary>
/// Command to void an un-prepared order item (PDF:I.24, PDF:I.28.1, V0-DOM-006).
/// Authorization is enforced entirely at the HTTP boundary
/// (OrderManagementEndpoints, via the grant flow); this command is a pure
/// state-transition. A former `IsManagerAuthorized` flag + guard was
/// removed as dead, misleading defense-in-depth — every real caller
/// hardcoded it true and the paired exception was never mapped (independent
/// audit, 2026-09-05).
/// </summary>
public sealed record VoidOrderItemCommand(
    Guid OrderId,
    Guid OrderItemId,
    long ExpectedRowVersion,
    Guid ActorId,
    string ReasonCode,
    string CorrelationId,
    string? Notes = null)
{
    public void Validate()
    {
        if (OrderId == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty.", nameof(OrderId));
        if (OrderItemId == Guid.Empty)
            throw new ArgumentException("OrderItemId cannot be empty.", nameof(OrderItemId));
        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), "ExpectedRowVersion must be >= 1.");
        if (ActorId == Guid.Empty)
            throw new ArgumentException("ActorId cannot be empty.", nameof(ActorId));
        if (string.IsNullOrWhiteSpace(ReasonCode))
            throw new ArgumentException("ReasonCode cannot be empty.", nameof(ReasonCode));
        if (!VoidReasonCatalog.IsValid(ReasonCode))
            throw new InvalidItemReasonException($"Reason '{ReasonCode}' is not a valid void catalog reason.");
        if (string.IsNullOrWhiteSpace(CorrelationId))
            throw new ArgumentException("CorrelationId cannot be empty.", nameof(CorrelationId));
    }
}

/// <summary>
/// Command to mark an active order item as complimentary (PDF:I.28.1, V0-DOM-006).
/// Preserves delivered quantity while reducing customer payable amount to 0.
/// Authorization is enforced at the HTTP boundary (see
/// <see cref="VoidOrderItemCommand"/> for the removed IsManagerAuthorized flag).
/// </summary>
public sealed record ApplyComplimentaryCommand(
    Guid OrderId,
    Guid OrderItemId,
    long ExpectedRowVersion,
    Guid ActorId,
    string ReasonCode,
    string CorrelationId,
    string? Notes = null)
{
    public void Validate()
    {
        if (OrderId == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty.", nameof(OrderId));
        if (OrderItemId == Guid.Empty)
            throw new ArgumentException("OrderItemId cannot be empty.", nameof(OrderItemId));
        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), "ExpectedRowVersion must be >= 1.");
        if (ActorId == Guid.Empty)
            throw new ArgumentException("ActorId cannot be empty.", nameof(ActorId));
        if (string.IsNullOrWhiteSpace(ReasonCode))
            throw new ArgumentException("ReasonCode cannot be empty.", nameof(ReasonCode));
        if (!ComplimentaryReasonCatalog.IsValid(ReasonCode))
            throw new InvalidItemReasonException($"Reason '{ReasonCode}' is not a valid complimentary catalog reason.");
        if (string.IsNullOrWhiteSpace(CorrelationId))
            throw new ArgumentException("CorrelationId cannot be empty.", nameof(CorrelationId));
    }
}
