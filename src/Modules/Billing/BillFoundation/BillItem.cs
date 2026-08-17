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
        DateTimeOffset? updatedAt = null)
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

        if (lineType is BillLineType.Complimentary)
        {
            NetAmount = 0m;
            TaxAmount = 0m;
            GrossAmount = 0m;
        }
        else
        {
            var lineSubtotal = BillMath.RoundCurrency(Quantity * UnitPrice);
            NetAmount = netAmount ?? BillMath.RoundCurrency(lineSubtotal - DiscountAmount);
            TaxAmount = taxAmount ?? BillMath.RoundCurrency(NetAmount * TaxRate / 100m);
            GrossAmount = grossAmount ?? BillMath.RoundCurrency(NetAmount + TaxAmount);
        }
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

    public decimal LineSubtotal => BillMath.RoundCurrency(Quantity * UnitPrice);

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

        return new BillItem(
            id: billItemId ?? Guid.NewGuid(),
            billId: billId,
            orderItemId: orderItem.Id,
            productId: orderItem.ProductId,
            productNameSnapshot: orderItem.ProductNameSnapshot,
            quantity: orderItem.Quantity,
            unitPrice: orderItem.UnitPrice,
            taxRate: orderItem.TaxRate,
            discountAmount: orderItem.DiscountAmount,
            netAmount: effectiveLineType == BillLineType.Complimentary ? 0m : orderItem.NetAmount,
            taxAmount: effectiveLineType == BillLineType.Complimentary ? 0m : orderItem.TaxAmount,
            grossAmount: effectiveLineType == BillLineType.Complimentary ? 0m : orderItem.GrossAmount,
            lineType: effectiveLineType,
            notes: notes ?? orderItem.Notes);
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
