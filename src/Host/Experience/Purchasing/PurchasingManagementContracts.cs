using ALKAROS.Purchasing.OrdersAndReceipts;
using ALKAROS.Purchasing.Suppliers;

namespace ALKAROS.Host.Experience.Purchasing;

// ---- Suppliers ----

public sealed record CreateSupplierV1(string Code, string Name, string? TaxNumber, string? TaxOffice, string? Phone, string? Email, bool Active = true);

public sealed record UpdateSupplierV1(string Name, string? TaxNumber, string? TaxOffice, string? Phone, string? Email);

public sealed record SupplierSummaryV1(Guid Id, string Code, string Name, bool Active)
{
    public static SupplierSummaryV1 From(SupplierSummaryDto value) => new(value.Id, value.Code, value.Name, value.Active);
}

public sealed record SupplierViewV1(
    Guid Id, string Code, string Name, string? TaxNumber, string? TaxOffice, string? Phone, string? Email,
    bool Active, bool IsMasked, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static SupplierViewV1 From(SupplierViewDto value)
        => new(value.Id, value.Code, value.Name, value.TaxNumber, value.TaxOffice, value.Phone, value.Email,
            value.Active, value.IsMasked, value.CreatedAt, value.UpdatedAt);
}

// ---- Purchase orders ----

public sealed record CreatePurchaseOrderLineV1(Guid StockItemId, decimal OrderedQuantity, string UnitCode, decimal UnitPrice);

public sealed record CreatePurchaseOrderV1(
    string OrderNumber, Guid SupplierId, Guid DestinationLocationId,
    IReadOnlyList<CreatePurchaseOrderLineV1> Lines, string? Notes, string Currency = "TRY");

public sealed record PurchaseOrderLineV1(
    Guid Id, Guid StockItemId, decimal OrderedQuantity, decimal ReceivedQuantity, decimal OpenQuantity,
    string UnitCode, decimal UnitPrice, decimal TotalPrice, string Status)
{
    public static PurchaseOrderLineV1 From(PurchaseOrderLine value)
        => new(value.Id, value.StockItemId, value.OrderedQuantity, value.ReceivedQuantity, value.OpenQuantity,
            value.UnitCode, value.UnitPrice, value.TotalPrice, value.Status.ToString());
}

public sealed record PurchaseOrderV1(
    Guid Id, string OrderNumber, Guid SupplierId, string Status, Guid DestinationLocationId,
    string? Notes, decimal TotalAmount, string Currency, IReadOnlyList<PurchaseOrderLineV1> Lines,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static PurchaseOrderV1 From(PurchaseOrder value)
        => new(value.Id, value.OrderNumber, value.SupplierId, value.Status.ToString(), value.DestinationLocationId,
            value.Notes, value.TotalAmount, value.Currency, value.Lines.Select(PurchaseOrderLineV1.From).ToArray(),
            value.CreatedAt, value.UpdatedAt);
}

// ---- Goods receipts ----

public sealed record ReceiveGoodsLineV1(Guid OrderLineId, decimal DeliveredQuantity, string? VarianceReason);

public sealed record ReceiveGoodsV1(
    string ReceiptNumber, IReadOnlyList<ReceiveGoodsLineV1> DeliveredItems,
    bool IsManagerApproved = false, string? Notes = null);

public sealed record GoodsReceiptItemV1(
    Guid Id, Guid OrderLineId, Guid StockItemId, decimal DeliveredQuantity, decimal AcceptedQuantity,
    decimal RejectedQuantity, string UnitCode, decimal UnitPrice, decimal VarianceQuantity,
    string? VarianceReason, bool IsApprovedByManager)
{
    public static GoodsReceiptItemV1 From(GoodsReceiptItem value)
        => new(value.Id, value.OrderLineId, value.StockItemId, value.DeliveredQuantity, value.AcceptedQuantity,
            value.RejectedQuantity, value.UnitCode, value.UnitPrice, value.VarianceQuantity, value.VarianceReason,
            value.IsApprovedByManager);
}

public sealed record GoodsReceiptV1(
    Guid Id, string ReceiptNumber, Guid OrderId, Guid SupplierId, Guid DestinationLocationId,
    DateTimeOffset ReceivedAt, string ReceivedBy, string? ApprovedBy, string? Notes,
    IReadOnlyList<GoodsReceiptItemV1> Items)
{
    public static GoodsReceiptV1 From(GoodsReceipt value)
        => new(value.Id, value.ReceiptNumber, value.OrderId, value.SupplierId, value.DestinationLocationId,
            value.ReceivedAt, value.ReceivedBy, value.ApprovedBy, value.Notes,
            value.Items.Select(GoodsReceiptItemV1.From).ToArray());
}

public sealed record PurchasingApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record PurchasingApiErrorEnvelopeV1(PurchasingApiErrorV1 Error);
