using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ALKAROS.Purchasing.Suppliers;
using Npgsql;

namespace ALKAROS.Purchasing.OrdersAndReceipts;

public sealed record CreatePOLineDto(
    Guid StockItemId,
    decimal OrderedQuantity,
    string UnitCode,
    decimal UnitPrice);

public sealed record CreatePOCommand(
    string OrderNumber,
    Guid SupplierId,
    Guid DestinationLocationId,
    IReadOnlyList<CreatePOLineDto> Lines,
    string? Notes = null,
    string Currency = "TRY");

public sealed record ReceiveLineItemDto(
    Guid OrderLineId,
    decimal DeliveredQuantity,
    string? VarianceReason = null);

public sealed record ReceiveGoodsCommand(
    string ReceiptNumber,
    Guid OrderId,
    string ReceivedBy,
    IReadOnlyList<ReceiveLineItemDto> DeliveredItems,
    bool IsManagerApproved = false,
    string? ApprovedBy = null,
    string? Notes = null);

public interface IPurchasingService
{
    Task<PurchaseOrder> CreatePurchaseOrderAsync(CreatePOCommand command, CancellationToken ct = default);
    Task SubmitPurchaseOrderAsync(Guid orderId, CancellationToken ct = default);
    Task CancelPurchaseOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<GoodsReceipt> ReceiveGoodsAsync(ReceiveGoodsCommand command, CancellationToken ct = default);
}

public sealed class PurchasingService : IPurchasingService
{
    private readonly IPurchaseOrderRepository _poRepo;
    private readonly IGoodsReceiptRepository _grRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly NpgsqlDataSource _dataSource;

    public PurchasingService(
        IPurchaseOrderRepository poRepo,
        IGoodsReceiptRepository grRepo,
        ISupplierRepository supplierRepo,
        NpgsqlDataSource dataSource)
    {
        _poRepo = poRepo ?? throw new ArgumentNullException(nameof(poRepo));
        _grRepo = grRepo ?? throw new ArgumentNullException(nameof(grRepo));
        _supplierRepo = supplierRepo ?? throw new ArgumentNullException(nameof(supplierRepo));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<PurchaseOrder> CreatePurchaseOrderAsync(CreatePOCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Assert supplier exists and is active (V11-PUR-002)
        var supplier = await _supplierRepo.GetByIdAsync(command.SupplierId, ct)
            ?? throw new SupplierNotFoundException(command.SupplierId);
        supplier.AssertCanAcceptOrders();

        // Uniqueness check for order number
        var existing = await _poRepo.GetByOrderNumberAsync(command.OrderNumber, ct);
        if (existing != null)
        {
            throw new InvalidPurchaseOrderException($"Purchase order '{command.OrderNumber}' already exists.");
        }

        var order = PurchaseOrder.Create(
            command.OrderNumber,
            command.SupplierId,
            command.DestinationLocationId,
            command.Notes,
            command.Currency);

        if (command.Lines != null)
        {
            foreach (var line in command.Lines)
            {
                order.AddLine(line.StockItemId, line.OrderedQuantity, line.UnitCode, line.UnitPrice);
            }
        }

        await _poRepo.SaveAsync(order, ct);
        return order;
    }

    public async Task SubmitPurchaseOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _poRepo.GetByIdAsync(orderId, ct)
            ?? throw new PurchaseOrderNotFoundException(orderId);

        // Assert supplier is still active
        var supplier = await _supplierRepo.GetByIdAsync(order.SupplierId, ct)
            ?? throw new SupplierNotFoundException(order.SupplierId);
        supplier.AssertCanAcceptOrders();

        order.Submit();
        await _poRepo.UpdateAsync(order, ct);
    }

    public async Task CancelPurchaseOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _poRepo.GetByIdAsync(orderId, ct)
            ?? throw new PurchaseOrderNotFoundException(orderId);

        order.Cancel();
        await _poRepo.UpdateAsync(order, ct);
    }

    public async Task<GoodsReceipt> ReceiveGoodsAsync(ReceiveGoodsCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await _poRepo.GetByIdAsync(command.OrderId, ct)
            ?? throw new PurchaseOrderNotFoundException(command.OrderId);

        if (order.Status != PurchaseOrderStatus.Submitted && order.Status != PurchaseOrderStatus.PartiallyReceived)
        {
            throw new PurchaseOrderStatusException($"Cannot receive goods against purchase order '{order.OrderNumber}' in status {order.Status}.");
        }

        // Assert supplier is active
        var supplier = await _supplierRepo.GetByIdAsync(order.SupplierId, ct)
            ?? throw new SupplierNotFoundException(order.SupplierId);
        supplier.AssertCanAcceptOrders();

        // Idempotency: check if receipt number already exists
        var existingReceipt = await _grRepo.GetByReceiptNumberAsync(command.ReceiptNumber, ct);
        if (existingReceipt != null)
        {
            throw new DuplicateGoodsReceiptException(command.ReceiptNumber);
        }

        var receiptId = Guid.NewGuid();
        var receivedAt = DateTimeOffset.UtcNow;
        var receipt = new GoodsReceipt(
            receiptId,
            command.ReceiptNumber,
            order.Id,
            order.SupplierId,
            order.DestinationLocationId,
            receivedAt,
            command.ReceivedBy,
            command.ApprovedBy,
            command.Notes,
            receivedAt);

        var itemsToPostToStock = new List<GoodsReceiptItem>();

        foreach (var deliveredItem in command.DeliveredItems)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == deliveredItem.OrderLineId)
                ?? throw new InvalidGoodsReceiptException($"Order line '{deliveredItem.OrderLineId}' does not belong to purchase order '{order.OrderNumber}'.");

            // Tolerance and variance policy evaluation
            var eval = ReceiptVariancePolicy.Evaluate(
                orderedQuantity: line.OpenQuantity,
                deliveredQuantity: deliveredItem.DeliveredQuantity,
                isManagerApproved: command.IsManagerApproved,
                varianceReason: deliveredItem.VarianceReason);

            var receiptItem = new GoodsReceiptItem(
                id: Guid.NewGuid(),
                receiptId: receiptId,
                orderLineId: line.Id,
                stockItemId: line.StockItemId,
                deliveredQuantity: deliveredItem.DeliveredQuantity,
                acceptedQuantity: eval.AcceptedQuantity,
                rejectedQuantity: eval.RejectedQuantity,
                unitCode: line.UnitCode,
                unitPrice: line.UnitPrice,
                varianceQuantity: eval.VarianceQuantity,
                varianceReason: eval.VarianceReason,
                isApprovedByManager: eval.IsApproved,
                createdAt: receivedAt);

            receipt.AddItem(receiptItem);

            // Update line received quantity
            line.RecordReceived(eval.AcceptedQuantity);

            if (eval.AcceptedQuantity > 0)
            {
                itemsToPostToStock.Add(receiptItem);
            }
        }

        // Update order status based on remaining open lines
        order.UpdateStatusFromLines();

        // Atomic PostgreSQL persistence: GoodsReceipt + Order update + StockLedger movements
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 1. Save goods receipt
        await _grRepo.SaveAsync(receipt, ct);

        // 2. Update purchase order and lines
        await _poRepo.UpdateAsync(order, ct);

        // 3. Post stock movements to inventory.stock_movements ledger (V11-INV-001)
        foreach (var item in itemsToPostToStock)
        {
            const string stockMovementSql = @"
INSERT INTO inventory.stock_movements (
    stock_movement_id, stock_item_id, stock_location_id, movement_type, direction,
    quantity, unit_code, source_type, source_reference_id, reason, created_at
) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11);";

            await using var stockCmd = new NpgsqlCommand(stockMovementSql, conn, tx);
            var movementId = Guid.NewGuid();

            stockCmd.Parameters.AddWithValue(movementId);
            stockCmd.Parameters.AddWithValue(item.StockItemId);
            stockCmd.Parameters.AddWithValue(receipt.DestinationLocationId);
            stockCmd.Parameters.AddWithValue("PurchaseReceipt");
            stockCmd.Parameters.AddWithValue("In");
            stockCmd.Parameters.AddWithValue(item.AcceptedQuantity);
            stockCmd.Parameters.AddWithValue(item.UnitCode);
            stockCmd.Parameters.AddWithValue("GoodsReceipt");
            stockCmd.Parameters.AddWithValue(receipt.Id);
            stockCmd.Parameters.AddWithValue((object?)receipt.ReceiptNumber ?? DBNull.Value);
            stockCmd.Parameters.AddWithValue(receipt.ReceivedAt);

            await stockCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        return receipt;
    }
}
