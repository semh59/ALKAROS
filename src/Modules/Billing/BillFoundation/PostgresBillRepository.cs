using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// PostgreSQL implementation of <see cref="IBillRepository"/>.
/// Manages transactional persistence for bills and bill_items with optimistic concurrency control.
/// </summary>
public sealed class PostgresBillRepository : IBillRepository
{
    private const string BillsTable = "billing.bills";
    private const string BillItemsTable = "billing.bill_items";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresBillRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Bill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(id));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadBillAsync(connection, null, "bill_id = @id", [new NpgsqlParameter("id", id)], cancellationToken);
    }

    public async Task<Bill?> GetByBillNumberAsync(string billNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(billNumber))
            throw new ArgumentException("Bill number cannot be empty.", nameof(billNumber));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadBillAsync(connection, null, "bill_number = @bill_number", [new NpgsqlParameter("bill_number", billNumber)], cancellationToken);
    }

    public async Task<IReadOnlyList<Bill>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id cannot be empty.", nameof(orderId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadBillsAsync(connection, null, "order_id = @order_id", [new NpgsqlParameter("order_id", orderId)], cancellationToken);
    }

    public async Task<IReadOnlyList<Bill>> GetByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty.", nameof(tableId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadBillsAsync(connection, null, "table_id = @table_id", [new NpgsqlParameter("table_id", tableId)], cancellationToken);
    }

    public async Task AddAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await InsertBillAsync(connection, transaction, bill, cancellationToken);

        foreach (var item in bill.Items)
        {
            await InsertBillItemAsync(connection, transaction, item, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<long> SaveAsync(Bill bill, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var newRowVersion = await UpdateBillAsync(connection, transaction, bill, expectedRowVersion, cancellationToken);

        var existingItemIds = (await ReadItemIdsAsync(connection, transaction, bill.Id, cancellationToken)).ToHashSet();
        var currentItemIds = bill.Items.Select(i => i.Id).ToHashSet();

        // Delete removed items
        foreach (var removedId in existingItemIds.Except(currentItemIds))
        {
            await DeleteBillItemAsync(connection, transaction, removedId, cancellationToken);
        }

        // Insert or update items
        foreach (var item in bill.Items)
        {
            if (existingItemIds.Contains(item.Id))
                await UpdateBillItemAsync(connection, transaction, item, cancellationToken);
            else
                await InsertBillItemAsync(connection, transaction, item, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return newRowVersion;
    }

    public async Task<bool> IsOrderItemBilledAsync(Guid orderItemId, CancellationToken cancellationToken = default)
    {
        if (orderItemId == Guid.Empty)
            throw new ArgumentException("Order item id cannot be empty.", nameof(orderItemId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"SELECT 1 FROM {BillItemsTable} WHERE order_item_id = @order_item_id LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("order_item_id", orderItemId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    public async Task<IReadOnlySet<Guid>> GetBilledOrderItemIdsAsync(IEnumerable<Guid> orderItemIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderItemIds);
        var idList = orderItemIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (idList.Length == 0)
            return new HashSet<Guid>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"SELECT order_item_id FROM {BillItemsTable} WHERE order_item_id = ANY(@ids);",
            connection);
        command.Parameters.AddWithValue("ids", idList);

        var billed = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            billed.Add(reader.GetGuid(0));
        }

        return billed;
    }

    private static async Task<Bill?> ReadBillAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string whereClause,
        NpgsqlParameter[] parameters,
        CancellationToken cancellationToken)
    {
        var bills = await ReadBillsAsync(connection, transaction, whereClause, parameters, cancellationToken);
        return bills.Count > 0 ? bills[0] : null;
    }

    private static async Task<IReadOnlyList<Bill>> ReadBillsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string whereClause,
        NpgsqlParameter[] parameters,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT bill_id, bill_number, table_id, order_id, customer_account_id,
                   status, subtotal, discount_total, tax_total, payable_amount,
                   allocated_amount, paid_amount, change_amount, currency_code,
                   opened_at, closed_at, cancelled_at, reopened_at,
                   created_at, updated_at, row_version
            FROM {BillsTable}
            WHERE {whereClause};
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var p in parameters)
            command.Parameters.Add(p);

        var billRows = new List<BillHeaderRow>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                billRows.Add(new BillHeaderRow(
                    Id: reader.GetGuid(0),
                    BillNumber: reader.GetString(1),
                    TableId: reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    OrderId: reader.IsDBNull(3) ? null : reader.GetGuid(3),
                    CustomerAccountId: reader.IsDBNull(4) ? null : reader.GetGuid(4),
                    Status: Enum.Parse<BillState>(reader.GetString(5)),
                    Subtotal: reader.GetDecimal(6),
                    DiscountTotal: reader.GetDecimal(7),
                    TaxTotal: reader.GetDecimal(8),
                    PayableAmount: reader.GetDecimal(9),
                    AllocatedAmount: reader.GetDecimal(10),
                    PaidAmount: reader.GetDecimal(11),
                    ChangeAmount: reader.GetDecimal(12),
                    CurrencyCode: reader.GetString(13).Trim(),
                    OpenedAt: reader.GetFieldValue<DateTimeOffset>(14),
                    ClosedAt: reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
                    CancelledAt: reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16),
                    ReopenedAt: reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17),
                    CreatedAt: reader.GetFieldValue<DateTimeOffset>(18),
                    UpdatedAt: reader.GetFieldValue<DateTimeOffset>(19),
                    RowVersion: reader.GetInt64(20)));
            }
        }

        if (billRows.Count == 0)
            return Array.Empty<Bill>();

        var billIds = billRows.Select(b => b.Id).ToArray();
        var itemsMap = await ReadBillItemsAsync(connection, transaction, billIds, cancellationToken);

        var result = new List<Bill>(billRows.Count);
        foreach (var row in billRows)
        {
            itemsMap.TryGetValue(row.Id, out var items);
            result.Add(new Bill(
                id: row.Id,
                billNumber: row.BillNumber,
                items: items,
                tableId: row.TableId,
                orderId: row.OrderId,
                customerAccountId: row.CustomerAccountId,
                status: row.Status,
                currencyCode: row.CurrencyCode,
                allocatedAmount: row.AllocatedAmount,
                paidAmount: row.PaidAmount,
                changeAmount: row.ChangeAmount,
                openedAt: row.OpenedAt,
                closedAt: row.ClosedAt,
                cancelledAt: row.CancelledAt,
                reopenedAt: row.ReopenedAt,
                rowVersion: row.RowVersion,
                createdAt: row.CreatedAt,
                updatedAt: row.UpdatedAt));
        }

        return result;
    }

    private static async Task<Dictionary<Guid, List<BillItem>>> ReadBillItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid[] billIds,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT bill_item_id, bill_id, order_item_id, product_id,
                   product_name_snapshot, quantity, unit_price, discount_amount,
                   tax_rate, tax_amount, net_amount, gross_amount,
                   line_type, notes, created_at, updated_at, row_version
            FROM {BillItemsTable}
            WHERE bill_id = ANY(@bill_ids)
            ORDER BY created_at ASC;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("bill_ids", billIds);

        var result = new Dictionary<Guid, List<BillItem>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new BillItem(
                id: reader.GetGuid(0),
                billId: reader.GetGuid(1),
                orderItemId: reader.GetGuid(2),
                productId: reader.GetGuid(3),
                productNameSnapshot: reader.GetString(4),
                quantity: reader.GetDecimal(5),
                unitPrice: reader.GetDecimal(6),
                discountAmount: reader.GetDecimal(7),
                taxRate: reader.GetDecimal(8),
                taxAmount: reader.GetDecimal(9),
                netAmount: reader.GetDecimal(10),
                grossAmount: reader.GetDecimal(11),
                lineType: Enum.Parse<BillLineType>(reader.GetString(12)),
                notes: reader.IsDBNull(13) ? null : reader.GetString(13),
                createdAt: reader.GetFieldValue<DateTimeOffset>(14),
                updatedAt: reader.GetFieldValue<DateTimeOffset>(15),
                rowVersion: reader.GetInt64(16));

            if (!result.TryGetValue(item.BillId, out var list))
            {
                list = new List<BillItem>();
                result[item.BillId] = list;
            }
            list.Add(item);
        }

        return result;
    }

    private static async Task<List<Guid>> ReadItemIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid billId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT bill_item_id FROM {BillItemsTable} WHERE bill_id = @bill_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("bill_id", billId);

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }
        return ids;
    }

    private static async Task InsertBillAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Bill bill,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            INSERT INTO {BillsTable} (
                bill_id, bill_number, table_id, order_id, customer_account_id,
                status, subtotal, discount_total, tax_total, payable_amount,
                allocated_amount, paid_amount, change_amount, currency_code,
                opened_at, closed_at, cancelled_at, reopened_at,
                created_at, updated_at, row_version)
            VALUES (
                @bill_id, @bill_number, @table_id, @order_id, @customer_account_id,
                @status, @subtotal, @discount_total, @tax_total, @payable_amount,
                @allocated_amount, @paid_amount, @change_amount, @currency_code,
                @opened_at, @closed_at, @cancelled_at, @reopened_at,
                @created_at, @updated_at, @row_version);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        BindBillParameters(command, bill, bill.RowVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> UpdateBillAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Bill bill,
        long expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var targetRowVersion = expectedRowVersion + 1;
        var sql = $"""
            UPDATE {BillsTable}
            SET bill_number = @bill_number,
                table_id = @table_id,
                order_id = @order_id,
                customer_account_id = @customer_account_id,
                status = @status,
                subtotal = @subtotal,
                discount_total = @discount_total,
                tax_total = @tax_total,
                payable_amount = @payable_amount,
                allocated_amount = @allocated_amount,
                paid_amount = @paid_amount,
                change_amount = @change_amount,
                currency_code = @currency_code,
                opened_at = @opened_at,
                closed_at = @closed_at,
                cancelled_at = @cancelled_at,
                reopened_at = @reopened_at,
                updated_at = @updated_at,
                row_version = @new_row_version
            WHERE bill_id = @bill_id AND row_version = @expected_row_version;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        BindBillParameters(command, bill, targetRowVersion);
        command.Parameters.AddWithValue("expected_row_version", expectedRowVersion);
        command.Parameters.AddWithValue("new_row_version", targetRowVersion);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"Optimistic concurrency violation on Bill {bill.Id}. Expected row_version {expectedRowVersion}.");
        }

        return targetRowVersion;
    }

    private static async Task InsertBillItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BillItem item,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            INSERT INTO {BillItemsTable} (
                bill_item_id, bill_id, order_item_id, product_id,
                product_name_snapshot, quantity, unit_price, discount_amount,
                tax_rate, tax_amount, net_amount, gross_amount,
                line_type, notes, created_at, updated_at, row_version)
            VALUES (
                @bill_item_id, @bill_id, @order_item_id, @product_id,
                @product_name_snapshot, @quantity, @unit_price, @discount_amount,
                @tax_rate, @tax_amount, @net_amount, @gross_amount,
                @line_type, @notes, @created_at, @updated_at, @row_version);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        BindBillItemParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateBillItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BillItem item,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            UPDATE {BillItemsTable}
            SET product_name_snapshot = @product_name_snapshot,
                quantity = @quantity,
                unit_price = @unit_price,
                discount_amount = @discount_amount,
                tax_rate = @tax_rate,
                tax_amount = @tax_amount,
                net_amount = @net_amount,
                gross_amount = @gross_amount,
                line_type = @line_type,
                notes = @notes,
                updated_at = @updated_at,
                row_version = row_version + 1
            WHERE bill_item_id = @bill_item_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        BindBillItemParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteBillItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid billItemId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"DELETE FROM {BillItemsTable} WHERE bill_item_id = @bill_item_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("bill_item_id", billItemId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void BindBillParameters(NpgsqlCommand command, Bill bill, long rowVersion)
    {
        command.Parameters.AddWithValue("bill_id", bill.Id);
        command.Parameters.AddWithValue("bill_number", bill.BillNumber);
        command.Parameters.AddWithValue("table_id", (object?)bill.TableId ?? DBNull.Value);
        command.Parameters.AddWithValue("order_id", (object?)bill.OrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("customer_account_id", (object?)bill.CustomerAccountId ?? DBNull.Value);
        command.Parameters.AddWithValue("status", bill.Status.ToString());
        command.Parameters.AddWithValue("subtotal", bill.Subtotal);
        command.Parameters.AddWithValue("discount_total", bill.DiscountTotal);
        command.Parameters.AddWithValue("tax_total", bill.TaxTotal);
        command.Parameters.AddWithValue("payable_amount", bill.PayableAmount);
        command.Parameters.AddWithValue("allocated_amount", bill.AllocatedAmount);
        command.Parameters.AddWithValue("paid_amount", bill.PaidAmount);
        command.Parameters.AddWithValue("change_amount", bill.ChangeAmount);
        command.Parameters.AddWithValue("currency_code", bill.CurrencyCode);
        command.Parameters.AddWithValue("opened_at", bill.OpenedAt);
        command.Parameters.AddWithValue("closed_at", (object?)bill.ClosedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("cancelled_at", (object?)bill.CancelledAt ?? DBNull.Value);
        command.Parameters.AddWithValue("reopened_at", (object?)bill.ReopenedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("created_at", bill.CreatedAt);
        command.Parameters.AddWithValue("updated_at", bill.UpdatedAt);
        command.Parameters.AddWithValue("row_version", rowVersion);
    }

    private static void BindBillItemParameters(NpgsqlCommand command, BillItem item)
    {
        command.Parameters.AddWithValue("bill_item_id", item.Id);
        command.Parameters.AddWithValue("bill_id", item.BillId);
        command.Parameters.AddWithValue("order_item_id", item.OrderItemId);
        command.Parameters.AddWithValue("product_id", item.ProductId);
        command.Parameters.AddWithValue("product_name_snapshot", item.ProductNameSnapshot);
        command.Parameters.AddWithValue("quantity", item.Quantity);
        command.Parameters.AddWithValue("unit_price", item.UnitPrice);
        command.Parameters.AddWithValue("discount_amount", item.DiscountAmount);
        command.Parameters.AddWithValue("tax_rate", item.TaxRate);
        command.Parameters.AddWithValue("tax_amount", item.TaxAmount);
        command.Parameters.AddWithValue("net_amount", item.NetAmount);
        command.Parameters.AddWithValue("gross_amount", item.GrossAmount);
        command.Parameters.AddWithValue("line_type", item.LineType.ToString());
        command.Parameters.AddWithValue("notes", (object?)item.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("created_at", item.CreatedAt);
        command.Parameters.AddWithValue("updated_at", item.UpdatedAt);
        command.Parameters.AddWithValue("row_version", item.RowVersion);
    }

    private sealed record BillHeaderRow(
        Guid Id,
        string BillNumber,
        Guid? TableId,
        Guid? OrderId,
        Guid? CustomerAccountId,
        BillState Status,
        decimal Subtotal,
        decimal DiscountTotal,
        decimal TaxTotal,
        decimal PayableAmount,
        decimal AllocatedAmount,
        decimal PaidAmount,
        decimal ChangeAmount,
        string CurrencyCode,
        DateTimeOffset OpenedAt,
        DateTimeOffset? ClosedAt,
        DateTimeOffset? CancelledAt,
        DateTimeOffset? ReopenedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        long RowVersion);
}
