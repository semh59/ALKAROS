namespace ALKAROS.Orders.SubmitOrder;

using ALKAROS.Orders.OrderAggregate;

/// <summary>
/// Result envelope returned by the idempotent submit order handler.
/// </summary>
public sealed record SubmitOrderResult(
    Guid OrderId,
    string OrderNumber,
    OrderState Status,
    long RowVersion,
    DateTimeOffset SubmittedAt,
    decimal Total,
    int ItemCount,
    bool IsReplay);
