using System.Data;
using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Tables.TableRelease;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders.OrderCancellation;

/// <summary>
/// Cancels a whole order in one transaction: money check under the bill locks, the order write, the stock and
/// daily menu portions of every line the kitchen had not started (lines it had started are waste), the kitchen
/// ticket lines, the order's open bills, the table and the audit record. Either all of it is kept or none.
/// </summary>
public sealed class OrderCancellationStore(
    IOrderRepository orders,
    IBillRepository bills,
    OrderItemVoidEffects effects,
    ITableReleasePort tables,
    NpgsqlDataSource dataSource,
    IAuditEventStore auditEvents)
{
    public const int MaxReasonLength = 500;

    public async Task<CancelOrderResultV1> CancelAsync(
        Guid orderId, string? reason, long? expectedRowVersion, Guid actorId, bool actorMayCancelServed, string correlationId,
        CancellationToken cancellationToken)
    {
        var trimmed = NormalizeReason(reason);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var result = await CancelInTransactionAsync(
            orderId, trimmed, expectedRowVersion, actorId, correlationId, connection, transaction, cancellationToken,
            actorMayCancelServed: actorMayCancelServed);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public static string NormalizeReason(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxReasonLength
            ? throw new OrderCancelReasonException()
            : trimmed;
    }

    /// <param name="reason">Already normalized with <see cref="NormalizeReason"/>.</param>
    /// <param name="expectedRowVersion">Null takes the version read here (a caller that holds no version).</param>
    public async Task<CancelOrderResultV1> CancelInTransactionAsync(
        Guid orderId,
        string reason,
        long? expectedRowVersion,
        Guid actorId,
        string correlationId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken,
        string actorType = "User",
        bool actorMayCancelServed = true)
    {
        var lockedBills = new HashSet<Guid>();
        await BillMoneyGuard.LockOrderBillsAsync(connection, transaction, orderId, lockedBills, cancellationToken);

        var order = await orders.GetByIdAsync(orderId, cancellationToken) ?? throw new OrderNotFoundException(orderId);
        if (!order.CanTransitionTo(OrderState.Cancelled))
            throw new OrderCancelNotAllowedException(orderId, order.Status.ToString());
        if (order.Items.Any(i => i.Status == OrderItemState.Active && i.KitchenState == KitchenState.Served))
            throw new OrderCancelHasServedItemsException(orderId);
        // A bill opened after the first locks is locked now, so the money check below sees it.
        await BillMoneyGuard.LockOrderBillsAsync(connection, transaction, orderId, lockedBills, cancellationToken);
        if (await BillMoneyGuard.OrderHasMoneyAsync(connection, transaction, orderId, cancellationToken))
            throw new OrderCancelHasPaymentException(orderId);
        if (!actorMayCancelServed && await BillsHoldServedItemAsync(connection, transaction, orderId, cancellationToken))
            throw new OrderCancelHasServedItemsException(orderId);

        var now = DateTimeOffset.UtcNow;
        await orders.SaveAsync(
            order.CancelWhole(reason, actorId, now), expectedRowVersion ?? order.RowVersion, connection, transaction, cancellationToken);

        int itemsCancelled = 0, stockRestored = 0, waste = 0;
        foreach (var item in order.Items.Where(i => i.Status is OrderItemState.Draft or OrderItemState.Active
                     && i.KitchenState != KitchenState.Cancelled))
        {
            itemsCancelled++;
            if (item.Status != OrderItemState.Active)
                continue;

            var effect = await effects.ApplyAsync(
                item,
                actorId,
                $"OrderCancelled: {reason} (item {item.Id:D})",
                $"OrderCancelled: {reason}",
                now,
                connection,
                transaction,
                cancellationToken);
            if (effect.StockRestored)
                stockRestored++;
            if (item.KitchenState is KitchenState.Preparing or KitchenState.Ready)
                waste++;
        }

        var billsCancelled = await bills.CancelOpenBillsOfOrderAsync(
            orderId, reason, actorId, now, connection, transaction, cancellationToken);
        await tables.ClearOrderBillPointersAsync(orderId, connection, transaction, cancellationToken);
        await tables.ReleaseOrderAsync(orderId, "Sipariş iptal edildi", connection, transaction, cancellationToken);

        await auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: "order.cancelled",
                aggregateType: "Order",
                aggregateId: orderId,
                actorType: actorType,
                correlationId: correlationId,
                actorId: actorId,
                reason: reason,
                beforeStateJson: JsonSerializer.Serialize(new { status = order.Status.ToString() }),
                afterStateJson: JsonSerializer.Serialize(new
                {
                    status = "Cancelled", itemsCancelled, stockRestoredItems = stockRestored, wasteItems = waste, billsCancelled,
                })),
            connection, transaction, cancellationToken);

        return new CancelOrderResultV1(orderId, "Cancelled", reason, now, itemsCancelled, stockRestored, waste, billsCancelled);
    }

    /// <summary>The bills this cancels may carry other orders' lines (a merged bill); a served one there needs a refund approver, as a bill cancellation does.</summary>
    private static async Task<bool> BillsHoldServedItemAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"""
            SELECT EXISTS (
                SELECT 1 FROM billing.bill_items bli
                JOIN billing.bills b ON b.bill_id = bli.bill_id
                JOIN orders.order_items oli ON oli.order_item_id = bli.order_item_id
                WHERE b.status NOT IN ('Paid', 'Cancelled') AND oli.status = 'Active' AND oli.kitchen_state = 'Served'
                  AND {BillOrderLink.Matches("b", "@order_id")});
            """, connection, transaction);
        command.Parameters.AddWithValue("order_id", orderId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
