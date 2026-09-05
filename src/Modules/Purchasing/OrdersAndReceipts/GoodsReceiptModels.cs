using System;
using System.Collections.Generic;

namespace ALKAROS.Purchasing.OrdersAndReceipts;

public sealed class GoodsReceiptItem
{
    public Guid Id { get; }
    public Guid ReceiptId { get; }
    public Guid OrderLineId { get; }
    public Guid StockItemId { get; }
    public decimal DeliveredQuantity { get; }
    public decimal AcceptedQuantity { get; }
    public decimal RejectedQuantity { get; }
    public string UnitCode { get; }
    public decimal UnitPrice { get; }
    public decimal VarianceQuantity { get; }
    public string? VarianceReason { get; }
    public bool IsApprovedByManager { get; }
    public DateTimeOffset CreatedAt { get; }

    public GoodsReceiptItem(
        Guid id,
        Guid receiptId,
        Guid orderLineId,
        Guid stockItemId,
        decimal deliveredQuantity,
        decimal acceptedQuantity,
        decimal rejectedQuantity,
        string unitCode,
        decimal unitPrice,
        decimal varianceQuantity,
        string? varianceReason,
        bool isApprovedByManager,
        DateTimeOffset createdAt)
    {
        Id = id;
        ReceiptId = receiptId;
        OrderLineId = orderLineId;
        StockItemId = stockItemId;
        DeliveredQuantity = deliveredQuantity;
        AcceptedQuantity = acceptedQuantity;
        RejectedQuantity = rejectedQuantity;
        UnitCode = unitCode;
        UnitPrice = unitPrice;
        VarianceQuantity = varianceQuantity;
        VarianceReason = varianceReason;
        IsApprovedByManager = isApprovedByManager;
        CreatedAt = createdAt;
    }
}

public sealed class GoodsReceipt
{
    private readonly List<GoodsReceiptItem> _items = new();

    public Guid Id { get; }
    public string ReceiptNumber { get; }
    public Guid OrderId { get; }
    public Guid SupplierId { get; }
    public Guid DestinationLocationId { get; }
    public DateTimeOffset ReceivedAt { get; }
    public string ReceivedBy { get; }
    public string? ApprovedBy { get; }
    public string? Notes { get; }
    public IReadOnlyList<GoodsReceiptItem> Items => _items.AsReadOnly();
    public DateTimeOffset CreatedAt { get; }

    public GoodsReceipt(
        Guid id,
        string receiptNumber,
        Guid orderId,
        Guid supplierId,
        Guid destinationLocationId,
        DateTimeOffset receivedAt,
        string receivedBy,
        string? approvedBy,
        string? notes,
        DateTimeOffset createdAt,
        IEnumerable<GoodsReceiptItem>? items = null)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new InvalidGoodsReceiptException("Receipt number is required.");
        }
        if (string.IsNullOrWhiteSpace(receivedBy))
        {
            throw new InvalidGoodsReceiptException("ReceivedBy is required.");
        }

        Id = id;
        ReceiptNumber = receiptNumber.Trim().ToUpperInvariant();
        OrderId = orderId;
        SupplierId = supplierId;
        DestinationLocationId = destinationLocationId;
        ReceivedAt = receivedAt;
        ReceivedBy = receivedBy.Trim();
        ApprovedBy = approvedBy?.Trim();
        Notes = notes?.Trim();
        CreatedAt = createdAt;

        if (items != null)
        {
            _items.AddRange(items);
        }
    }

    public void AddItem(GoodsReceiptItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }
}
