namespace ALKAROS.Orders.SubmitOrder;

/// <summary>
/// Command to submit a Draft order idempotently (PDF:II.2.4, PDF:II.3.2, PDF:III.6).
/// Contains the client idempotency identity (ClientId, OperationId), the order id,
/// the optimistic concurrency version expected by the client, and audit metadata.
/// </summary>
public sealed record SubmitOrderCommand(
    string ClientId,
    string OperationId,
    Guid OrderId,
    long ExpectedRowVersion,
    Guid? ChangedBy = null,
    DateTimeOffset? SubmittedAt = null,
    string? Reason = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new ArgumentException("ClientId cannot be empty.", nameof(ClientId));
        if (string.IsNullOrWhiteSpace(OperationId))
            throw new ArgumentException("OperationId cannot be empty.", nameof(OperationId));
        if (OrderId == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty.", nameof(OrderId));
        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), "ExpectedRowVersion must be >= 1.");
    }
}
