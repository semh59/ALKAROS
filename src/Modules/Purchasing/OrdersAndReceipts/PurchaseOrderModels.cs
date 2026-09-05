using System;
using System.Collections.Generic;

namespace ALKAROS.Purchasing.OrdersAndReceipts;

public enum PurchaseOrderStatus
{
    Draft,
    Submitted,
    PartiallyReceived,
    Completed,
    Cancelled
}

public enum PurchaseOrderLineStatus
{
    Pending,
    PartiallyReceived,
    Completed
}

public sealed class PurchaseOrderLine
{
    public Guid Id { get; }
    public Guid OrderId { get; }
    public Guid StockItemId { get; }
    public decimal OrderedQuantity { get; }
    public decimal ReceivedQuantity { get; private set; }
    public string UnitCode { get; }
    public decimal UnitPrice { get; }
    public decimal TotalPrice { get; }
    public PurchaseOrderLineStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public decimal OpenQuantity => Math.Max(0m, OrderedQuantity - ReceivedQuantity);

    public PurchaseOrderLine(
        Guid id,
        Guid orderId,
        Guid stockItemId,
        decimal orderedQuantity,
        decimal receivedQuantity,
        string unitCode,
        decimal unitPrice,
        decimal totalPrice,
        PurchaseOrderLineStatus status,
        DateTimeOffset createdAt)
    {
        if (orderedQuantity <= 0)
        {
            throw new InvalidPurchaseOrderException("Ordered quantity must be greater than zero.");
        }
        if (receivedQuantity < 0)
        {
            throw new InvalidPurchaseOrderException("Received quantity cannot be negative.");
        }
        if (unitPrice < 0)
        {
            throw new InvalidPurchaseOrderException("Unit price cannot be negative.");
        }
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            throw new InvalidPurchaseOrderException("Unit code is required.");
        }

        Id = id;
        OrderId = orderId;
        StockItemId = stockItemId;
        OrderedQuantity = orderedQuantity;
        ReceivedQuantity = receivedQuantity;
        UnitCode = unitCode.Trim().ToLowerInvariant();
        UnitPrice = unitPrice;
        TotalPrice = totalPrice;
        Status = status;
        CreatedAt = createdAt;
    }

    public static PurchaseOrderLine Create(
        Guid orderId,
        Guid stockItemId,
        decimal orderedQuantity,
        string unitCode,
        decimal unitPrice,
        Guid? id = null)
    {
        var lineId = id ?? Guid.NewGuid();
        var totalPrice = Math.Round(orderedQuantity * unitPrice, 4, MidpointRounding.AwayFromZero);
        return new PurchaseOrderLine(
            lineId,
            orderId,
            stockItemId,
            orderedQuantity,
            0m,
            unitCode,
            unitPrice,
            totalPrice,
            PurchaseOrderLineStatus.Pending,
            DateTimeOffset.UtcNow);
    }

    public void RecordReceived(decimal acceptedQuantity)
    {
        if (acceptedQuantity <= 0)
        {
            return;
        }

        ReceivedQuantity += acceptedQuantity;
        if (ReceivedQuantity >= OrderedQuantity)
        {
            Status = PurchaseOrderLineStatus.Completed;
        }
        else
        {
            Status = PurchaseOrderLineStatus.PartiallyReceived;
        }
    }
}

public sealed class PurchaseOrder
{
    private readonly List<PurchaseOrderLine> _lines = new();

    public Guid Id { get; }
    public string OrderNumber { get; }
    public Guid SupplierId { get; }
    public PurchaseOrderStatus Status { get; private set; }
    public Guid DestinationLocationId { get; }
    public string? Notes { get; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; }
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines.AsReadOnly();
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public PurchaseOrder(
        Guid id,
        string orderNumber,
        Guid supplierId,
        PurchaseOrderStatus status,
        Guid destinationLocationId,
        string? notes,
        decimal totalAmount,
        string currency,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IEnumerable<PurchaseOrderLine>? lines = null)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new InvalidPurchaseOrderException("Order number is required.");
        }
        if (destinationLocationId == Guid.Empty)
        {
            throw new InvalidPurchaseOrderException("Destination location is required.");
        }
        if (supplierId == Guid.Empty)
        {
            throw new InvalidPurchaseOrderException("Supplier is required.");
        }

        Id = id;
        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        SupplierId = supplierId;
        Status = status;
        DestinationLocationId = destinationLocationId;
        Notes = notes?.Trim();
        TotalAmount = totalAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim().ToUpperInvariant();
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;

        if (lines != null)
        {
            _lines.AddRange(lines);
        }
    }

    public static PurchaseOrder Create(
        string orderNumber,
        Guid supplierId,
        Guid destinationLocationId,
        string? notes = null,
        string currency = "TRY",
        Guid? id = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new PurchaseOrder(
            id ?? Guid.NewGuid(),
            orderNumber,
            supplierId,
            PurchaseOrderStatus.Draft,
            destinationLocationId,
            notes,
            0m,
            currency,
            now,
            now);
    }

    public void AddLine(Guid stockItemId, decimal orderedQuantity, string unitCode, decimal unitPrice)
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new PurchaseOrderStatusException($"Cannot add lines to a purchase order in {Status} status.");
        }

        var line = PurchaseOrderLine.Create(Id, stockItemId, orderedQuantity, unitCode, unitPrice);
        _lines.Add(line);
        TotalAmount += line.TotalPrice;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Submit()
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new PurchaseOrderStatusException($"Cannot submit purchase order in {Status} status.");
        }
        if (_lines.Count == 0)
        {
            throw new InvalidPurchaseOrderException("Cannot submit purchase order without line items.");
        }

        Status = PurchaseOrderStatus.Submitted;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status == PurchaseOrderStatus.Completed)
        {
            throw new PurchaseOrderStatusException("Cannot cancel a completed purchase order.");
        }
        if (Status == PurchaseOrderStatus.PartiallyReceived)
        {
            throw new PurchaseOrderStatusException("Cannot cancel a purchase order with received goods.");
        }

        Status = PurchaseOrderStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateStatusFromLines()
    {
        if (_lines.Count == 0) return;

        if (_lines.All(l => l.Status == PurchaseOrderLineStatus.Completed))
        {
            Status = PurchaseOrderStatus.Completed;
        }
        else if (_lines.Any(l => l.Status == PurchaseOrderLineStatus.PartiallyReceived || l.ReceivedQuantity > 0))
        {
            Status = PurchaseOrderStatus.PartiallyReceived;
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
