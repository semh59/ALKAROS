namespace ALKAROS.Orders.OrderAggregate;

/// <summary>
/// A modifier line of an order item (orders.order_item_modifiers, PDF:III.6.3).
/// Immutable; the name and price are snapshots frozen at order time so later
/// catalog edits cannot change a placed order (V1-ORD-001 acceptance).
/// </summary>
public sealed class OrderItemModifier
{
    public OrderItemModifier(
        Guid id,
        Guid orderItemId,
        Guid modifierId,
        string modifierNameSnapshot,
        decimal priceDelta = 0,
        decimal quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(modifierNameSnapshot))
            throw new ArgumentException("Modifier name snapshot cannot be empty.", nameof(modifierNameSnapshot));
        if (priceDelta < 0)
            throw new ArgumentException("Modifier price delta cannot be negative.", nameof(priceDelta));
        if (quantity <= 0)
            throw new ArgumentException("Modifier quantity must be positive.", nameof(quantity));

        Id = id;
        OrderItemId = orderItemId;
        ModifierId = modifierId;
        ModifierNameSnapshot = modifierNameSnapshot;
        PriceDelta = priceDelta;
        Quantity = quantity;
    }

    public Guid Id { get; }
    public Guid OrderItemId { get; }
    public Guid ModifierId { get; }
    public string ModifierNameSnapshot { get; }
    public decimal PriceDelta { get; }
    public decimal Quantity { get; }

    public decimal Total() => OrderMath.RoundCurrency(PriceDelta * Quantity);

    /// <summary>
    /// Rebinds the modifier to the given owning item id; used when a modifier
    /// is attached to an order item so every child row always carries the
    /// parent row's id (FK integrity).
    /// </summary>
    public OrderItemModifier ForItem(Guid orderItemId)
        => new(Id, orderItemId, ModifierId, ModifierNameSnapshot, PriceDelta, Quantity);
}