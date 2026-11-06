using System.Data;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Tables.TableRelease;
using Npgsql;

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
    private readonly ITableReleasePort _tables;

    public OrderSettlementService(IOrderRepository orders, IBillRepository bills, NpgsqlDataSource dataSource, ITableReleasePort tables)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
    }

    /// <returns>True when every order of the bill is Completed afterwards (now or already).</returns>
    public async Task<bool> CompleteForPaidBillAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        var orderIds = await OrderIdsOfBillAsync(billId, cancellationToken);
        var completed = orderIds.Count > 0;
        foreach (var orderId in orderIds)
            completed &= await CompleteOrderWhenSettledAsync(orderId, cancellationToken);
        return completed;
    }

    private async Task<List<Guid>> OrderIdsOfBillAsync(Guid billId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(BillOrderLink.OrderIdsOfBill);
        command.Parameters.AddWithValue("bill_id", billId);
        var orderIds = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            orderIds.Add(reader.GetGuid(0));
        return orderIds;
    }

    /// <returns>True when the order is Completed afterwards (now or already).</returns>
    public async Task<bool> CompleteOrderWhenSettledAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
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
                await _tables.ReleaseOrderAsync(orderId, "Hesap ödendi", connection, transaction, cancellationToken);
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
}
