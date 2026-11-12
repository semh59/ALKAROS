using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Tables.TableRelease;

public sealed class PostgresTableReleasePort : ITableReleasePort
{
    public async Task AttachBillPointerAsync(
        Guid tableId, Guid billId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE table_mgmt.tables SET current_bill_id = @bill_id, row_version = row_version + 1 WHERE table_id = @table_id;",
            connection, transaction);
        command.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
        command.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearBillPointerAsync(
        Guid billId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE table_mgmt.tables t
            SET current_bill_id = (
                    SELECT b.bill_id FROM billing.bills b
                    WHERE b.table_id = t.table_id AND b.bill_id <> @bill_id AND b.status NOT IN ('Paid', 'Cancelled')
                    ORDER BY b.opened_at, b.created_at, b.bill_id
                    LIMIT 1),
                row_version = row_version + 1
            WHERE t.current_bill_id = @bill_id;
            """,
            connection, transaction);
        command.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearOrderBillPointersAsync(
        Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            $"""
            UPDATE table_mgmt.tables SET current_bill_id = NULL, row_version = row_version + 1
            WHERE current_bill_id IN (SELECT bill_id FROM billing.bills WHERE {TableBillOrderLink.Matches("billing.bills", "@order_id")});
            """, connection, transaction);
        command.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ReleaseOrderAsync(
        Guid orderId, string unmergeReason, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using (var forget = new NpgsqlCommand(
            "DELETE FROM table_mgmt.table_sent_checks WHERE order_id = @order_id;", connection, transaction))
        {
            forget.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            await forget.ExecuteNonQueryAsync(cancellationToken);
        }

        var tableIds = new List<Guid>();
        await using (var find = new NpgsqlCommand(
            "SELECT table_id FROM table_mgmt.tables WHERE current_order_id = @order_id FOR UPDATE;", connection, transaction))
        {
            find.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            await using var reader = await find.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                tableIds.Add(reader.GetGuid(0));
        }

        foreach (var tableId in tableIds)
        {
            Guid? nextOrderId;
            await using (var next = new NpgsqlCommand(
                """
                SELECT order_id FROM orders.orders
                WHERE table_id = @table_id AND order_id <> @order_id
                  AND status NOT IN ('Completed', 'Cancelled', 'Rejected')
                ORDER BY created_at, order_id
                LIMIT 1;
                """, connection, transaction))
            {
                next.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
                next.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
                nextOrderId = await next.ExecuteScalarAsync(cancellationToken) as Guid?;
            }

            if (nextOrderId is { } followingOrderId)
            {
                await using var advance = new NpgsqlCommand(
                    $"""
                    UPDATE table_mgmt.tables
                    SET current_order_id = @next_order_id,
                        current_bill_id = (
                            SELECT bill_id FROM billing.bills
                            WHERE table_id = @table_id AND status NOT IN ('Paid', 'Cancelled')
                            ORDER BY COALESCE({TableBillOrderLink.Matches("billing.bills", "@next_order_id")}, false) DESC, opened_at, created_at, bill_id
                            LIMIT 1),
                        row_version = row_version + 1
                    WHERE table_id = @table_id;
                    """, connection, transaction);
                advance.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
                advance.Parameters.Add("next_order_id", NpgsqlDbType.Uuid).Value = followingOrderId;
                await advance.ExecuteNonQueryAsync(cancellationToken);
                continue;
            }

            await using (var free = new NpgsqlCommand(
                """
                UPDATE table_mgmt.tables
                SET current_order_id = NULL,
                    current_bill_id = NULL,
                    current_status = CASE WHEN current_status = 'Occupied' THEN 'Available' ELSE current_status END,
                    row_version = row_version + 1
                WHERE table_id = @table_id;
                """, connection, transaction))
            {
                free.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
                await free.ExecuteNonQueryAsync(cancellationToken);
            }

            // Everyone has left: the merge ends and its participant tables are free again.
            await using var close = new NpgsqlCommand(
                """
                WITH closed AS (
                    UPDATE table_mgmt.table_merges
                    SET status = 'Unmerged',
                        unmerged_at = now(),
                        unmerge_reason = @unmerge_reason,
                        row_version = row_version + 1
                    WHERE primary_table_id = @table_id AND status = 'Active'
                    RETURNING merged_table_id
                )
                UPDATE table_mgmt.tables
                SET current_status = 'Available',
                    current_order_id = NULL,
                    current_bill_id = NULL,
                    row_version = row_version + 1
                WHERE table_id IN (SELECT merged_table_id FROM closed)
                  AND current_status = 'Occupied' AND current_order_id IS NULL;
                """, connection, transaction);
            close.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            close.Parameters.Add("unmerge_reason", NpgsqlDbType.Text).Value = unmergeReason;
            await close.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
