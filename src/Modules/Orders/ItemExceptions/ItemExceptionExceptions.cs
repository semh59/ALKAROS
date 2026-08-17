namespace ALKAROS.Orders.ItemExceptions;

public sealed class LateVoidRejectedException : Exception
{
    public LateVoidRejectedException(Guid orderItemId, string kitchenState)
        : base($"Order item '{orderItemId}' cannot be voided because kitchen preparation has already progressed ({kitchenState}).")
    {
        OrderItemId = orderItemId;
        KitchenState = kitchenState;
    }

    public Guid OrderItemId { get; }
    public string KitchenState { get; }
}

public sealed class UnauthorizedItemOperationException : Exception
{
    public UnauthorizedItemOperationException(string operation, Guid actorId)
        : base($"Actor '{actorId}' is not authorized to perform '{operation}'. Manager authority is required.")
    {
        Operation = operation;
        ActorId = actorId;
    }

    public string Operation { get; }
    public Guid ActorId { get; }
}

public sealed class InvalidItemReasonException : Exception
{
    public InvalidItemReasonException(string message)
        : base(message)
    {
    }
}

public sealed class OrderItemNotFoundException : Exception
{
    public OrderItemNotFoundException(Guid orderId, Guid orderItemId)
        : base($"Order item '{orderItemId}' was not found on order '{orderId}'.")
    {
        OrderId = orderId;
        OrderItemId = orderItemId;
    }

    public Guid OrderId { get; }
    public Guid OrderItemId { get; }
}

public sealed class StaleOrderRowVersionException : Exception
{
    public StaleOrderRowVersionException(Guid orderId, long expectedVersion, long currentVersion)
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
