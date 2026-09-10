namespace ALKAROS.Orders.OrderAggregate;

/// <summary>
/// An order line (orders.order_items, PDF:III.6.2). The product name, SKU
/// and unit price are snapshots frozen at order time: later catalog edits
/// never change a placed order (V1-ORD-001 acceptance). Amounts use the
/// canonical kuruş rounding (<see cref="OrderMath"/>); persisted amounts are
/// accepted through the optional constructor args so a reload always
/// round-trips with zero drift.
/// </summary>
public sealed class OrderItem
{
    public OrderItem(
        Guid id,
        Guid orderId,
        Guid productId,
        string productNameSnapshot,
        decimal quantity,
        decimal unitPrice,
        decimal taxRate,
        string? skuSnapshot = null,
        decimal discountAmount = 0,
        IReadOnlyList<OrderItemModifier>? modifiers = null,
        OrderItemState status = OrderItemState.Draft,
        KitchenState kitchenState = KitchenState.NotSent,
        PortionReservationStatus portionReservationStatus = PortionReservationStatus.NotApplicable,
        decimal? netAmount = null,
        decimal? taxAmount = null,
        decimal? grossAmount = null,
        string? notes = null,
        long rowVersion = 1,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Order item id cannot be empty.", nameof(id));
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id cannot be empty.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new ArgumentException("Product name snapshot cannot be empty.", nameof(productNameSnapshot));
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        if (taxRate < 0)
            throw new ArgumentException("Tax rate cannot be negative.", nameof(taxRate));
        if (discountAmount < 0)
            throw new ArgumentException("Discount amount cannot be negative.", nameof(discountAmount));

        Id = id;
        OrderId = orderId;
        ProductId = productId;
        ProductNameSnapshot = productNameSnapshot;
        SkuSnapshot = skuSnapshot;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount;
        TaxRate = taxRate;
        Modifiers = modifiers is null
            ? Array.Empty<OrderItemModifier>()
            : modifiers.Select(m => m.ForItem(id)).ToArray();
        Status = status;
        KitchenState = kitchenState;
        PortionReservationStatus = portionReservationStatus;
        Notes = notes;
        RowVersion = rowVersion;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt ?? CreatedAt;

        var lineSubtotal = LineSubtotal();
        if (DiscountAmount > lineSubtotal)
            throw new ArgumentException(
                "Discount amount cannot exceed the order item subtotal.",
                nameof(discountAmount));
        NetAmount = netAmount ?? OrderMath.RoundCurrency(lineSubtotal - DiscountAmount);
        TaxAmount = taxAmount ?? OrderMath.RoundCurrency(NetAmount * TaxRate / 100m);
        GrossAmount = grossAmount ?? OrderMath.RoundCurrency(NetAmount + TaxAmount);
        if (NetAmount < 0 || TaxAmount < 0 || GrossAmount < 0)
            throw new ArgumentException("Persisted order item amounts cannot be negative.");
    }

    public Guid Id { get; }

    public Guid OrderId { get; }

    public Guid ProductId { get; }

    public string ProductNameSnapshot { get; }

    public string? SkuSnapshot { get; }

    public decimal Quantity { get; }

    public decimal UnitPrice { get; }

    public decimal DiscountAmount { get; }

    public decimal TaxRate { get; }

    public IReadOnlyList<OrderItemModifier> Modifiers { get; }

    public OrderItemState Status { get; }

    public KitchenState KitchenState { get; }

    public PortionReservationStatus PortionReservationStatus { get; }

    /// <summary>Amount before discount and tax for the whole line.</summary>
    public decimal LineSubtotalValue => LineSubtotal();

    public decimal NetAmount { get; }

    public decimal TaxAmount { get; }

    public decimal GrossAmount { get; }

    public string? Notes { get; }

    public long RowVersion { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public bool IsActive => Status is OrderItemState.Draft or OrderItemState.Active;

    /// <summary>
    /// Moves a Draft item into Active; part of order submission. Returns a
    /// new immutable instance; a non-Draft item cannot be activated.
    /// </summary>
    public OrderItem Activate()
    {
        if (Status is not OrderItemState.Draft)
            throw new InvalidOperationException($"Order item {Id} cannot be activated from {Status}.");

        return Mutate(status: OrderItemState.Active);
    }

    /// <summary>
    /// Cancels the item (V0-DOM-006 void policy, amended 2026-09-04): a
    /// not-yet-prepared (kitchen_state NotSent) Active item voids for free
    /// (V1-ORD-003/V1-ORD-005). A Sent/Preparing/Ready item may also be
    /// voided once sent to the kitchen (V1-IAM-027) — that path is gated by
    /// the bills.void grant one layer up, not by this method. Served is
    /// still refused here: that is comp/refund territory, never void.
    /// </summary>
    public OrderItem Cancel()
    {
        // V1-RMD-154: a Draft line — added to an open check but not yet fired
        // — cancels too. It has consumed no stock and printed no ticket, so it
        // is strictly cheaper to void than an Active one.
        if (Status is not (OrderItemState.Active or OrderItemState.Draft))
            throw new InvalidOperationException($"Order item {Id} cannot be cancelled from {Status}.");
        if (KitchenState is KitchenState.Served or KitchenState.Cancelled)
            throw new InvalidOperationException(
                $"Order item {Id} cannot be voided once it reached kitchen state {KitchenState}.");

        return Mutate(status: OrderItemState.Cancelled, kitchenState: KitchenState.Cancelled);
    }

    /// <summary>
    /// V1-KIT-005: advances the item's kitchen-side status mirror
    /// (Sent/Preparing/Ready/Served) to match what the kitchen ticket item
    /// it corresponds to actually reports. Does not touch <see cref="Status"/>
    /// — the item stays Active while the kitchen works on it; only
    /// <see cref="Cancel"/> and the (separate) complimentary path change
    /// Status. Only reachable when a deployment turns on kitchen live-sync
    /// (V1-SET-002); otherwise KitchenState never leaves NotSent/Cancelled.
    /// </summary>
    public OrderItem AdvanceKitchenState(KitchenState kitchenState)
    {
        if (Status is not OrderItemState.Active)
            throw new InvalidOperationException(
                $"Order item {Id} cannot advance kitchen state from item status {Status}.");

        return Mutate(kitchenState: kitchenState);
    }

    /// <summary>
    /// Changes a Draft item's quantity, recomputing net/tax/gross from
    /// scratch (V1-RMD-120: previously a channel-specific concern
    /// hand-duplicated in Host/DualScreen raw SQL instead of going through
    /// this aggregate). An Active item's quantity is frozen — <see cref="Cancel"/>
    /// is the only way to remove its contribution once submitted. Returns a
    /// new immutable instance.
    /// </summary>
    public OrderItem ChangeQuantity(decimal quantity)
    {
        if (Status is not OrderItemState.Draft)
            throw new InvalidOperationException($"Order item {Id} cannot change quantity from {Status}.");
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        return new OrderItem(
            Id,
            OrderId,
            ProductId,
            ProductNameSnapshot,
            quantity,
            UnitPrice,
            TaxRate,
            SkuSnapshot,
            DiscountAmount,
            Modifiers,
            Status,
            KitchenState,
            PortionReservationStatus,
            notes: Notes,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Returns a copy with the row version advanced; used by repositories
    /// after a successful optimistic concurrency update.
    /// </summary>
    public OrderItem WithRowVersion(long rowVersion)
        => Mutate(rowVersion: rowVersion);

    /// <summary>
    /// Rebinds the item to the given owning order id; used when an item is
    /// attached to an aggregate so every child row always carries the
    /// aggregate root's id (FK integrity).
    /// </summary>
    public OrderItem ForOrder(Guid orderId)
        => Mutate(orderId: orderId);

    private OrderItem Mutate(
        Guid? orderId = null,
        OrderItemState? status = null,
        KitchenState? kitchenState = null,
        long? rowVersion = null)
        => new(
            Id,
            orderId ?? OrderId,
            ProductId,
            ProductNameSnapshot,
            Quantity,
            UnitPrice,
            TaxRate,
            SkuSnapshot,
            DiscountAmount,
            Modifiers,
            status ?? Status,
            kitchenState ?? KitchenState,
            PortionReservationStatus,
            netAmount: NetAmount,
            taxAmount: TaxAmount,
            grossAmount: GrossAmount,
            notes: Notes,
            rowVersion: rowVersion ?? RowVersion,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt);

    private decimal LineSubtotal()
    {
        var baseTotal = UnitPrice * Quantity;
        var modifierTotal = 0m;
        foreach (var modifier in Modifiers)
            modifierTotal += modifier.Total();
        return OrderMath.RoundCurrency(baseTotal + modifierTotal);
    }
}
