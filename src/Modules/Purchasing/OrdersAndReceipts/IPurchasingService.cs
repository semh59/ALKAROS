using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
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
    private readonly IStockBalanceRepository _balanceRepo;
    private readonly IStockMovementRepository _movementRepo;

    public PurchasingService(
        IPurchaseOrderRepository poRepo,
        IGoodsReceiptRepository grRepo,
        ISupplierRepository supplierRepo,
        NpgsqlDataSource dataSource,
        IStockBalanceRepository balanceRepo,
        IStockMovementRepository movementRepo)
    {
        _poRepo = poRepo ?? throw new ArgumentNullException(nameof(poRepo));
        _grRepo = grRepo ?? throw new ArgumentNullException(nameof(grRepo));
        _supplierRepo = supplierRepo ?? throw new ArgumentNullException(nameof(supplierRepo));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
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

        // 3. Post stock movements and their balance effect through Inventory's
        // own contract, using this receipt's connection and transaction so
        // both commit or roll back with the receipt (V0-ARC-001 row 27:
        // Purchasing → Inventory, goods receipt stock movement).
        foreach (var item in itemsToPostToStock)
        {
            await _balanceRepo.ApplyOnHandDeltaAsync(
                item.StockItemId, receipt.DestinationLocationId, item.AcceptedQuantity, conn, tx, ct);

            var movement = new StockMovement(
                id: Guid.NewGuid(),
                stockItemId: item.StockItemId,
                stockLocationId: receipt.DestinationLocationId,
                movementType: StockMovementType.PurchaseReceipt,
                direction: MovementDirection.In,
                quantity: item.AcceptedQuantity,
                unitCode: item.UnitCode,
                sourceType: StockMovementSourceType.GoodsReceipt,
                sourceReferenceId: receipt.Id,
                reason: receipt.ReceiptNumber,
                createdBy: null,
                createdAt: receipt.ReceivedAt);
            await _movementRepo.AppendAsync(movement, conn, tx, ct);
        }

        await tx.CommitAsync(ct);

        return receipt;
    }
}
