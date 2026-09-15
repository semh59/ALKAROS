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

public sealed class OrderSubmissionDispatchException : Exception
{
    public OrderSubmissionDispatchException(string message)
        : base(message)
    {
    }

    public OrderSubmissionDispatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
