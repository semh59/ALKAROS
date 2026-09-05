using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace ALKAROS.Purchasing.OrdersAndReceipts;

public interface IGoodsReceiptRepository
{
    Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<GoodsReceipt?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken ct = default);
    Task<IReadOnlyList<GoodsReceipt>> ListByOrderAsync(Guid orderId, CancellationToken ct = default);
    Task SaveAsync(GoodsReceipt receipt, CancellationToken ct = default);
}

public sealed class PostgresGoodsReceiptRepository : IGoodsReceiptRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresGoodsReceiptRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT receipt_id, receipt_number, order_id, supplier_id, destination_location_id, received_at, received_by, approved_by, notes, created_at
FROM purchasing.goods_receipts
WHERE receipt_id = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var receipt = MapReceiptHeader(reader);
        await reader.CloseAsync();

        var items = await LoadItemsAsync(conn, receipt.Id, ct);
        return new GoodsReceipt(
            receipt.Id,
            receipt.ReceiptNumber,
            receipt.OrderId,
            receipt.SupplierId,
            receipt.DestinationLocationId,
            receipt.ReceivedAt,
            receipt.ReceivedBy,
            receipt.ApprovedBy,
            receipt.Notes,
            receipt.CreatedAt,
            items);
    }

    public async Task<GoodsReceipt?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptNumber);

        const string sql = @"
SELECT receipt_id, receipt_number, order_id, supplier_id, destination_location_id, received_at, received_by, approved_by, notes, created_at
FROM purchasing.goods_receipts
WHERE receipt_number = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(receiptNumber.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var receipt = MapReceiptHeader(reader);
        await reader.CloseAsync();

        var items = await LoadItemsAsync(conn, receipt.Id, ct);
        return new GoodsReceipt(
            receipt.Id,
            receipt.ReceiptNumber,
            receipt.OrderId,
            receipt.SupplierId,
            receipt.DestinationLocationId,
            receipt.ReceivedAt,
            receipt.ReceivedBy,
            receipt.ApprovedBy,
            receipt.Notes,
            receipt.CreatedAt,
            items);
    }

    public async Task<IReadOnlyList<GoodsReceipt>> ListByOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT receipt_id, receipt_number, order_id, supplier_id, destination_location_id, received_at, received_by, approved_by, notes, created_at
FROM purchasing.goods_receipts
WHERE order_id = $1
ORDER BY received_at ASC;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(orderId);

        var list = new List<GoodsReceipt>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapReceiptHeader(reader));
        }
        await reader.CloseAsync();

        var result = new List<GoodsReceipt>();
        foreach (var r in list)
        {
            var items = await LoadItemsAsync(conn, r.Id, ct);
            result.Add(new GoodsReceipt(
                r.Id,
                r.ReceiptNumber,
                r.OrderId,
                r.SupplierId,
                r.DestinationLocationId,
                r.ReceivedAt,
                r.ReceivedBy,
                r.ApprovedBy,
                r.Notes,
                r.CreatedAt,
                items));
        }

        return result;
    }

    public async Task SaveAsync(GoodsReceipt receipt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string headerSql = @"
INSERT INTO purchasing.goods_receipts (receipt_id, receipt_number, order_id, supplier_id, destination_location_id, received_at, received_by, approved_by, notes, created_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10);";

        await using var cmd = new NpgsqlCommand(headerSql, conn, tx);
        cmd.Parameters.AddWithValue(receipt.Id);
        cmd.Parameters.AddWithValue(receipt.ReceiptNumber);
        cmd.Parameters.AddWithValue(receipt.OrderId);
        cmd.Parameters.AddWithValue(receipt.SupplierId);
        cmd.Parameters.AddWithValue(receipt.DestinationLocationId);
        cmd.Parameters.AddWithValue(receipt.ReceivedAt);
        cmd.Parameters.AddWithValue(receipt.ReceivedBy);
        cmd.Parameters.AddWithValue((object?)receipt.ApprovedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)receipt.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue(receipt.CreatedAt);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new DuplicateGoodsReceiptException(receipt.ReceiptNumber);
        }

        foreach (var item in receipt.Items)
        {
            const string itemSql = @"
INSERT INTO purchasing.goods_receipt_items (item_id, receipt_id, order_line_id, stock_item_id, delivered_quantity, accepted_quantity, rejected_quantity, unit_code, unit_price, variance_quantity, variance_reason, is_approved_by_manager, created_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13);";

            await using var itemCmd = new NpgsqlCommand(itemSql, conn, tx);
            itemCmd.Parameters.AddWithValue(item.Id);
            itemCmd.Parameters.AddWithValue(item.ReceiptId);
            itemCmd.Parameters.AddWithValue(item.OrderLineId);
            itemCmd.Parameters.AddWithValue(item.StockItemId);
            itemCmd.Parameters.AddWithValue(item.DeliveredQuantity);
            itemCmd.Parameters.AddWithValue(item.AcceptedQuantity);
            itemCmd.Parameters.AddWithValue(item.RejectedQuantity);
            itemCmd.Parameters.AddWithValue(item.UnitCode);
            itemCmd.Parameters.AddWithValue(item.UnitPrice);
            itemCmd.Parameters.AddWithValue(item.VarianceQuantity);
            itemCmd.Parameters.AddWithValue((object?)item.VarianceReason ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue(item.IsApprovedByManager);
            itemCmd.Parameters.AddWithValue(item.CreatedAt);
            await itemCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task<List<GoodsReceiptItem>> LoadItemsAsync(NpgsqlConnection conn, Guid receiptId, CancellationToken ct)
    {
        const string sql = @"
SELECT item_id, receipt_id, order_line_id, stock_item_id, delivered_quantity, accepted_quantity, rejected_quantity, unit_code, unit_price, variance_quantity, variance_reason, is_approved_by_manager, created_at
FROM purchasing.goods_receipt_items
WHERE receipt_id = $1
ORDER BY created_at ASC;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(receiptId);

        var items = new List<GoodsReceiptItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var rId = reader.GetGuid(1);
            var lineId = reader.GetGuid(2);
            var stockItemId = reader.GetGuid(3);
            var deliveredQty = reader.GetDecimal(4);
            var acceptedQty = reader.GetDecimal(5);
            var rejectedQty = reader.GetDecimal(6);
            var unitCode = reader.GetString(7);
            var unitPrice = reader.GetDecimal(8);
            var varianceQty = reader.GetDecimal(9);
            var varianceReason = reader.IsDBNull(10) ? null : reader.GetString(10);
            var isApproved = reader.GetBoolean(11);
            var createdAt = reader.GetFieldValue<DateTimeOffset>(12);

            items.Add(new GoodsReceiptItem(
                id,
                rId,
                lineId,
                stockItemId,
                deliveredQty,
                acceptedQty,
                rejectedQty,
                unitCode,
                unitPrice,
                varianceQty,
                varianceReason,
                isApproved,
                createdAt));
        }

        return items;
    }

    private static GoodsReceipt MapReceiptHeader(DbDataReader reader)
    {
        var id = reader.GetGuid(0);
        var receiptNumber = reader.GetString(1);
        var orderId = reader.GetGuid(2);
        var supplierId = reader.GetGuid(3);
        var destLocationId = reader.GetGuid(4);
        var receivedAt = reader.GetFieldValue<DateTimeOffset>(5);
        var receivedBy = reader.GetString(6);
        var approvedBy = reader.IsDBNull(7) ? null : reader.GetString(7);
        var notes = reader.IsDBNull(8) ? null : reader.GetString(8);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(9);

        return new GoodsReceipt(
            id,
            receiptNumber,
            orderId,
            supplierId,
            destLocationId,
            receivedAt,
            receivedBy,
            approvedBy,
            notes,
            createdAt);
    }
}
