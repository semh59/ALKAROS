namespace ALKAROS.Orders.ItemExceptions;

using ALKAROS.Orders.OrderAggregate;

/// <summary>
/// Envelope returned after successfully applying an item exception (Void / Complimentary).
/// </summary>
public sealed record ItemExceptionResult(
    Guid OrderId,
    Guid OrderItemId,
    OrderItemState NewItemStatus,
    long NewOrderRowVersion,
    decimal NewOrderSubtotal,
    decimal NewOrderTaxTotal,
    decimal NewOrderTotal,
    DateTimeOffset AppliedAt);
