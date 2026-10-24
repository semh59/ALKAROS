using System.Data;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// V1-RMD-282: closes an order when its check is settled. Nothing ever moved an order past Submitted, so it
/// stayed "open" for ever: server hand-off reassigned the whole history, the suggested-waiter load counted
/// every old order and order notes were never anonymised. Payment is the financial end of an order (and can
/// precede serving), so once every bill of the order is Paid or Cancelled (and at least one Paid) the order
/// becomes Completed. Kitchen items are untouched: they live on their own tickets.
///
/// If the order was still the table's ATTACHED check (paid at the table, never sent to the till), the table
/// pointer is cleared in the same transaction and an Occupied table becomes Available - the party has paid
/// and left. A check that went to the till was already detached, so its table is never touched here. A merged
/// table keeps its pointer on its next open order and is only freed, ending the merge, once none is left.
/// </summary>
public sealed class OrderSettlementService
{
    private const int MaxAttempts = 3;

    private readonly IOrderRepository _orders;
    private readonly IBillRepository _bills;
    private readonly NpgsqlDataSource _dataSource;

    public OrderSettlementService(IOrderRepository orders, IBillRepository bills, NpgsqlDataSource dataSource)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <returns>True when the order is Completed afterwards (now or already).</returns>
    public async Task<bool> CompleteForPaidBillAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        var bill = await _bills.GetByIdAsync(billId, cancellationToken);
        if (bill?.OrderId is not { } orderId)
            return false;

        var siblings = await _bills.GetByOrderIdAsync(orderId, cancellationToken);
        var allSettled = siblings.All(b => b.Status is BillState.Paid or BillState.Cancelled)
            && siblings.Any(b => b.Status == BillState.Paid);
        if (!allSettled)
            return false;

        for (var attempt = 1; ; attempt++)
        {
            var order = await _orders.GetByIdAsync(orderId, cancellationToken);
            if (order is null)
                return false;
            if (order.Status == OrderState.Completed)
                return true;
            if (!order.CanCompleteOnPayment)
                return false;

            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await _orders.SaveAsync(order.CompleteOnPayment(), order.RowVersion, connection, transaction, cancellationToken);
                await ReleaseAttachedTableAsync(connection, transaction, orderId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch (InvalidOperationException) when (attempt < MaxAttempts)
            {
                // Optimistic concurrency on the order (a kitchen mirror landed): reload and try again.
                await transaction.RollbackAsync(cancellationToken);
            }
        }
    }

    private static async Task ReleaseAttachedTableAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid orderId, CancellationToken cancellationToken)
    {
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
            // A merged table still holds the other tables' open orders: the pointer moves on to
            // the next one and the merge stays until the last of them is paid.
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
                    """
                    UPDATE table_mgmt.tables
                    SET current_order_id = @next_order_id,
                        current_bill_id = (
                            SELECT bill_id FROM billing.bills
                            WHERE table_id = @table_id AND status NOT IN ('Paid', 'Cancelled')
                            ORDER BY (order_id = @next_order_id) DESC, opened_at, created_at, bill_id
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

            // Everyone has paid and left: the merge ends and its participant tables are free again.
            await using var close = new NpgsqlCommand(
                """
                WITH closed AS (
                    UPDATE table_mgmt.table_merges
                    SET status = 'Unmerged',
                        unmerged_at = now(),
                        unmerge_reason = 'Hesap ödendi',
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
            await close.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
