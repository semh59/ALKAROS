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
/// and left. A check that went to the till was already detached, so its table is never touched here.
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
        await using var command = new NpgsqlCommand(
            """
            UPDATE table_mgmt.tables
            SET current_order_id = NULL,
                current_bill_id = NULL,
                current_status = CASE WHEN current_status = 'Occupied' THEN 'Available' ELSE current_status END,
                row_version = row_version + 1
            WHERE current_order_id = @order_id;
            """, connection, transaction);
        command.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
