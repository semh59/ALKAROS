namespace ALKAROS.Orders.SubmitOrder;

public sealed class OrderNotFoundException : Exception
{
    public OrderNotFoundException(Guid orderId)
        : base($"Order '{orderId}' was not found.")
    {
        OrderId = orderId;
    }

    public Guid OrderId { get; }
}

public sealed class StaleOrderVersionException : Exception
{
    public StaleOrderVersionException(Guid orderId, long expectedVersion, long currentVersion)
        : base($"Order '{orderId}' has current row version {currentVersion}, but expected version was {expectedVersion}.")
    {
        OrderId = orderId;
        ExpectedVersion = expectedVersion;
        CurrentVersion = currentVersion;
    }

    public Guid OrderId { get; }
    public long ExpectedVersion { get; }
    public long CurrentVersion { get; }
}

public sealed class SubmitOrderIdempotencyConflictException : Exception
{
    public SubmitOrderIdempotencyConflictException(string clientId, string operationId)
        : base($"Idempotency key conflict for client '{clientId}' and operation '{operationId}'. Request payload differs from previously registered execution.")
    {
        ClientId = clientId;
        OperationId = operationId;
    }

    public string ClientId { get; }
    public string OperationId { get; }
}
