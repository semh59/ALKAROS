using ALKAROS.Orders.OrderAggregate;

namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// A bill line (billing.bill_items, PDF:III.7.2).
/// Represents the junction between a Bill and an OrderItem (V0-DOM-002 decision).
/// Each order item can belong to at most one active bill item (no double-billing).
/// </summary>
public sealed class BillItem
{
    public BillItem(
        Guid id,
        Guid billId,
        Guid orderItemId,
        Guid productId,
        string productNameSnapshot,
        decimal quantity,
        decimal unitPrice,
        decimal taxRate,
        decimal discountAmount = 0,
        decimal? netAmount = null,
        decimal? taxAmount = null,
        decimal? grossAmount = null,
        BillLineType lineType = BillLineType.Sale,
        string? notes = null,
        long rowVersion = 1,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        decimal? trueGrossAmount = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Bill item id cannot be empty.", nameof(id));
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (orderItemId == Guid.Empty)
            throw new ArgumentException("Order item id cannot be empty.", nameof(orderItemId));
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
        BillId = billId;
        OrderItemId = orderItemId;
        ProductId = productId;
        ProductNameSnapshot = productNameSnapshot;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount;
        TaxRate = taxRate;
        LineType = lineType;
        Notes = notes;
        RowVersion = rowVersion;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt ?? CreatedAt;

        var lineSubtotal = netAmount.HasValue
            ? BillMath.RoundCurrency(netAmount.Value + DiscountAmount)
            : BillMath.RoundCurrency(Quantity * UnitPrice);
        if (DiscountAmount > lineSubtotal)
            throw new ArgumentException(
                "Discount amount cannot exceed the bill item subtotal.",
                nameof(discountAmount));
        // V1-RMD-228: a complimentary line pays 0, but it is NOT a 0-value
        // line for fiscal purposes (Turkish fiscal register rules require
        // the real gross value plus a 100% discount, not an invisible line
        // that never happened). Enforced here rather than silently zeroed, so a
        // caller that forgets to discount the full subtotal fails loudly
        // instead of shipping an under-reported comp.
        //
        // V1-RMD-231: when netAmount is passed explicitly (every real
        // FromOrderItem call for a Complimentary line passes 0m), lineSubtotal
        // above is literally RoundCurrency(0 + DiscountAmount) == DiscountAmount
        // -- comparing DiscountAmount to lineSubtotal in that case is a
        // tautology that can never fail, even if DiscountAmount was computed
        // wrong upstream (e.g. a modifier total forgotten). trueGrossAmount is
        // an independent gross value (Quantity*UnitPrice plus modifiers,
        // OrderItem.LineSubtotalValue) supplied by the caller specifically to
        // break that tautology; when present it replaces lineSubtotal as the
        // value DiscountAmount must equal.
        if (lineType is BillLineType.Complimentary && DiscountAmount != (trueGrossAmount ?? lineSubtotal))
            throw new ArgumentException(
                "A complimentary line must be fully discounted (discount amount must equal the line's true gross value) " +
                "so its real gross value stays visible for fiscal reporting.",
                nameof(discountAmount));
        NetAmount = netAmount ?? BillMath.RoundCurrency(lineSubtotal - DiscountAmount);
        TaxAmount = taxAmount ?? BillMath.RoundCurrency(NetAmount * TaxRate / 100m);
        GrossAmount = grossAmount ?? BillMath.RoundCurrency(NetAmount + TaxAmount);
        if (NetAmount < 0 || TaxAmount < 0 || GrossAmount < 0)
            throw new ArgumentException("Persisted bill item amounts cannot be negative.");
    }

    public Guid Id { get; }

    public Guid BillId { get; }

    public Guid OrderItemId { get; }

    public Guid ProductId { get; }

    public string ProductNameSnapshot { get; }

    public decimal Quantity { get; }

    public decimal UnitPrice { get; }

    public decimal DiscountAmount { get; }

    public decimal TaxRate { get; }

    public decimal NetAmount { get; }

    public decimal TaxAmount { get; }

    public decimal GrossAmount { get; }

    public BillLineType LineType { get; }

    public string? Notes { get; }

    public long RowVersion { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public decimal LineSubtotal => BillMath.RoundCurrency(NetAmount + DiscountAmount);

    /// <summary>
    /// Creates a BillItem instance bound to a target Bill from an active OrderItem.
    /// Preserves frozen pricing and tax snapshots (V0-DOM-002 / V1-BIL-001).
    /// </summary>
    public static BillItem FromOrderItem(
        Guid billId,
        OrderItem orderItem,
        Guid? billItemId = null,
        BillLineType? lineType = null,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(orderItem);
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        var effectiveLineType = lineType ?? (orderItem.Status == OrderItemState.Complimentary
            ? BillLineType.Complimentary
            : BillLineType.Sale);

        // V1-RMD-228: a complimentary line's discount is the item's full
        // pre-discount subtotal (orderItem.NetAmount already reflects any
        // modifiers and prior discount, so adding the discount back
        // recovers the true gross including modifiers) — the real gross
        // value stays visible via LineSubtotal for fiscal reporting, only
        // the customer's payable stays 0. netAmount/taxAmount/grossAmount
        // are still passed explicitly (not left to recompute from
        // quantity * unitPrice) so a modifier's price delta is not lost.
        var discountAmount = effectiveLineType == BillLineType.Complimentary
            ? orderItem.NetAmount + orderItem.DiscountAmount
            : orderItem.DiscountAmount;

        return new BillItem(
            id: billItemId ?? Guid.NewGuid(),
            billId: billId,
            orderItemId: orderItem.Id,
            productId: orderItem.ProductId,
            productNameSnapshot: orderItem.ProductNameSnapshot,
            quantity: orderItem.Quantity,
            unitPrice: orderItem.UnitPrice,
            taxRate: orderItem.TaxRate,
            discountAmount: discountAmount,
            netAmount: effectiveLineType == BillLineType.Complimentary ? 0m : orderItem.NetAmount,
            taxAmount: effectiveLineType == BillLineType.Complimentary ? 0m : orderItem.TaxAmount,
            grossAmount: effectiveLineType == BillLineType.Complimentary ? 0m : orderItem.GrossAmount,
            lineType: effectiveLineType,
            notes: notes ?? orderItem.Notes,
            // V1-RMD-231: the order item's own independent gross (quantity *
            // unit price plus modifiers, computed without any reference to
            // NetAmount/DiscountAmount) so the constructor's Complimentary
            // check verifies discountAmount above against a real value
            // instead of one derived from itself.
            trueGrossAmount: effectiveLineType == BillLineType.Complimentary ? orderItem.LineSubtotalValue : null);
    }

    /// <summary>
    /// Returns a copy of this BillItem reassigned to another Bill.
    /// </summary>
    public BillItem ForBill(Guid newBillId)
    {
        if (newBillId == Guid.Empty)
            throw new ArgumentException("New bill id cannot be empty.", nameof(newBillId));

        return new BillItem(
            Id,
            newBillId,
            OrderItemId,
            ProductId,
            ProductNameSnapshot,
            Quantity,
            UnitPrice,
            TaxRate,
            DiscountAmount,
            NetAmount,
            TaxAmount,
            GrossAmount,
            LineType,
            Notes,
            RowVersion,
            CreatedAt,
            DateTimeOffset.UtcNow);
    }
}
