using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace ALKAROS.Purchasing.OrdersAndReceipts;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrder>> ListAsync(Guid? supplierId = null, PurchaseOrderStatus? status = null, CancellationToken ct = default);
    Task SaveAsync(PurchaseOrder order, CancellationToken ct = default);
    Task UpdateAsync(PurchaseOrder order, CancellationToken ct = default);
}

public sealed class PostgresPurchaseOrderRepository : IPurchaseOrderRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPurchaseOrderRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string poSql = @"
SELECT order_id, order_number, supplier_id, status, destination_location_id, notes, total_amount, currency, created_at, updated_at
FROM purchasing.purchase_orders
WHERE order_id = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(poSql, conn);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var order = MapOrderHeader(reader);
        await reader.CloseAsync();

        var lines = await LoadLinesAsync(conn, order.Id, ct);
        return new PurchaseOrder(
            order.Id,
            order.OrderNumber,
            order.SupplierId,
            order.Status,
            order.DestinationLocationId,
            order.Notes,
            order.TotalAmount,
            order.Currency,
            order.CreatedAt,
            order.UpdatedAt,
            lines);
    }

    public async Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);

        const string poSql = @"
SELECT order_id, order_number, supplier_id, status, destination_location_id, notes, total_amount, currency, created_at, updated_at
FROM purchasing.purchase_orders
WHERE order_number = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(poSql, conn);
        cmd.Parameters.AddWithValue(orderNumber.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var order = MapOrderHeader(reader);
        await reader.CloseAsync();

        var lines = await LoadLinesAsync(conn, order.Id, ct);
        return new PurchaseOrder(
            order.Id,
            order.OrderNumber,
            order.SupplierId,
            order.Status,
            order.DestinationLocationId,
            order.Notes,
            order.TotalAmount,
            order.Currency,
            order.CreatedAt,
            order.UpdatedAt,
            lines);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> ListAsync(Guid? supplierId = null, PurchaseOrderStatus? status = null, CancellationToken ct = default)
    {
        var sql = @"
SELECT order_id, order_number, supplier_id, status, destination_location_id, notes, total_amount, currency, created_at, updated_at
FROM purchasing.purchase_orders
WHERE 1=1";

        if (supplierId.HasValue) sql += " AND supplier_id = @supplierId";
        if (status.HasValue) sql += " AND status = @status";
        sql += $" ORDER BY created_at DESC LIMIT {MaxUnpagedRows + 1};";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (supplierId.HasValue) cmd.Parameters.AddWithValue("@supplierId", supplierId.Value);
        if (status.HasValue) cmd.Parameters.AddWithValue("@status", status.Value.ToString());

        var list = new List<PurchaseOrder>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapOrderHeader(reader));
        }
        await reader.CloseAsync();

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"ListAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        var result = new List<PurchaseOrder>();
        foreach (var po in list)
        {
            var lines = await LoadLinesAsync(conn, po.Id, ct);
            result.Add(new PurchaseOrder(
                po.Id,
                po.OrderNumber,
                po.SupplierId,
                po.Status,
                po.DestinationLocationId,
                po.Notes,
                po.TotalAmount,
                po.Currency,
                po.CreatedAt,
                po.UpdatedAt,
                lines));
        }

        return result;
    }

    public async Task SaveAsync(PurchaseOrder order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string headerSql = @"
INSERT INTO purchasing.purchase_orders (order_id, order_number, supplier_id, status, destination_location_id, notes, total_amount, currency, created_at, updated_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10);";

        await using var cmd = new NpgsqlCommand(headerSql, conn, tx);
        cmd.Parameters.AddWithValue(order.Id);
        cmd.Parameters.AddWithValue(order.OrderNumber);
        cmd.Parameters.AddWithValue(order.SupplierId);
        cmd.Parameters.AddWithValue(order.Status.ToString());
        cmd.Parameters.AddWithValue(order.DestinationLocationId);
        cmd.Parameters.AddWithValue((object?)order.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue(order.TotalAmount);
        cmd.Parameters.AddWithValue(order.Currency);
        cmd.Parameters.AddWithValue(order.CreatedAt);
        cmd.Parameters.AddWithValue(order.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);

        foreach (var line in order.Lines)
        {
            await InsertLineAsync(conn, tx, line, ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task UpdateAsync(PurchaseOrder order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string headerSql = @"
UPDATE purchasing.purchase_orders
SET status = $2,
    total_amount = $3,
    updated_at = $4
WHERE order_id = $1;";

        await using var cmd = new NpgsqlCommand(headerSql, conn, tx);
        cmd.Parameters.AddWithValue(order.Id);
        cmd.Parameters.AddWithValue(order.Status.ToString());
        cmd.Parameters.AddWithValue(order.TotalAmount);
        cmd.Parameters.AddWithValue(order.UpdatedAt);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0)
        {
            throw new PurchaseOrderNotFoundException(order.Id);
        }

        foreach (var line in order.Lines)
        {
            const string lineSql = @"
UPDATE purchasing.purchase_order_lines
SET received_quantity = $2,
    status = $3
WHERE line_id = $1;";

            await using var lineCmd = new NpgsqlCommand(lineSql, conn, tx);
            lineCmd.Parameters.AddWithValue(line.Id);
            lineCmd.Parameters.AddWithValue(line.ReceivedQuantity);
            lineCmd.Parameters.AddWithValue(line.Status.ToString());
            await lineCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task InsertLineAsync(NpgsqlConnection conn, NpgsqlTransaction tx, PurchaseOrderLine line, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO purchasing.purchase_order_lines (line_id, order_id, stock_item_id, ordered_quantity, received_quantity, unit_code, unit_price, total_price, status, created_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10);";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue(line.Id);
        cmd.Parameters.AddWithValue(line.OrderId);
        cmd.Parameters.AddWithValue(line.StockItemId);
        cmd.Parameters.AddWithValue(line.OrderedQuantity);
        cmd.Parameters.AddWithValue(line.ReceivedQuantity);
        cmd.Parameters.AddWithValue(line.UnitCode);
        cmd.Parameters.AddWithValue(line.UnitPrice);
        cmd.Parameters.AddWithValue(line.TotalPrice);
        cmd.Parameters.AddWithValue(line.Status.ToString());
        cmd.Parameters.AddWithValue(line.CreatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<PurchaseOrderLine>> LoadLinesAsync(NpgsqlConnection conn, Guid orderId, CancellationToken ct)
    {
        const string sql = @"
SELECT line_id, order_id, stock_item_id, ordered_quantity, received_quantity, unit_code, unit_price, total_price, status, created_at
FROM purchasing.purchase_order_lines
WHERE order_id = $1
ORDER BY created_at ASC;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(orderId);

        var lines = new List<PurchaseOrderLine>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var oId = reader.GetGuid(1);
            var stockItemId = reader.GetGuid(2);
            var orderedQty = reader.GetDecimal(3);
            var receivedQty = reader.GetDecimal(4);
            var unitCode = reader.GetString(5);
            var unitPrice = reader.GetDecimal(6);
            var totalPrice = reader.GetDecimal(7);
            var status = Enum.Parse<PurchaseOrderLineStatus>(reader.GetString(8));
            var createdAt = reader.GetFieldValue<DateTimeOffset>(9);

            lines.Add(new PurchaseOrderLine(
                id,
                oId,
                stockItemId,
                orderedQty,
                receivedQty,
                unitCode,
                unitPrice,
                totalPrice,
                status,
                createdAt));
        }

        return lines;
    }

    private static PurchaseOrder MapOrderHeader(DbDataReader reader)
    {
        var id = reader.GetGuid(0);
        var orderNumber = reader.GetString(1);
        var supplierId = reader.GetGuid(2);
        var status = Enum.Parse<PurchaseOrderStatus>(reader.GetString(3));
        var destLocationId = reader.GetGuid(4);
        var notes = reader.IsDBNull(5) ? null : reader.GetString(5);
        var totalAmount = reader.GetDecimal(6);
        var currency = reader.GetString(7);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(8);
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(9);

        return new PurchaseOrder(
            id,
            orderNumber,
            supplierId,
            status,
            destLocationId,
            notes,
            totalAmount,
            currency,
            createdAt,
            updatedAt);
    }
}
