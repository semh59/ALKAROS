using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.ItemExceptions;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;

/// <summary>
/// V1-RMD-137: found by an independent audit (2026-09-09) — an age-restricted
/// NFC order (V12-NFC-002) is deliberately parked at PendingConfirmation for
/// a staff ID check, but no HTTP action anywhere could ever move it out of
/// that state: it stayed pending forever, and the table it held as Reserved
/// could only be freed by a bare status change that left the order itself
/// permanently orphaned. This is the minimal, channel-agnostic slice of what
/// V12-QRO-003 eventually plans in full — Accept/Reject a PendingConfirmation
/// order, release the table it holds, and (on Reject) cancel whatever kitchen
/// ticket items were already dispatched, since NfcOrderingStore's own
/// decision (Semih, 2026-09-09) is to dispatch the kitchen ticket immediately
/// regardless of the age check, checking ID only at the point of service.
/// V12-QRO-003 completes the QR side: accepting a QR order first claims its
/// portions through the channel-neutral cross-channel arbiter (V12-STK-001),
/// in the same transaction as the stock consumption (which turns those holds
/// into Consumed), the Accepted write and the table's Reserved -&gt; Occupied
/// move; rejecting writes the order and frees the table together. Either
/// outcome is all-or-nothing: a stock loss or a stale row version leaves no
/// hold, no half-moved table and no Accepted order behind. The QR module
/// itself cannot host this — its only approved direct-call edges are
/// Identity and Table Management (V0-ARC-001 row 19).
/// </summary>
public sealed class PendingOrderConfirmationStore
{
    private readonly IOrderRepository _orders;
    private readonly IKitchenTicketRepository _tickets;
    private readonly IBillRepository _bills;
    private readonly NpgsqlDataSource _dataSource;
    private readonly OrderStockConsumptionService _stockConsumption;
    private readonly ICrossChannelPortionArbiter _portionArbiter;
    private readonly IProductStockMappingRepository _stockMappings;
    private readonly IStockItemRepository _stockItems;

    public PendingOrderConfirmationStore(
        IOrderRepository orders,
        IKitchenTicketRepository tickets,
        IBillRepository bills,
        NpgsqlDataSource dataSource,
        OrderStockConsumptionService stockConsumption,
        ICrossChannelPortionArbiter portionArbiter,
        IProductStockMappingRepository stockMappings,
        IStockItemRepository stockItems)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _stockConsumption = stockConsumption ?? throw new ArgumentNullException(nameof(stockConsumption));
        _portionArbiter = portionArbiter ?? throw new ArgumentNullException(nameof(portionArbiter));
        _stockMappings = stockMappings ?? throw new ArgumentNullException(nameof(stockMappings));
        _stockItems = stockItems ?? throw new ArgumentNullException(nameof(stockItems));
    }

    /// <summary>
    /// The staff member confirms the ID check passed (or otherwise clears
    /// the order for service): PendingConfirmation -&gt; Accepted, and a
    /// table the order still holds as Reserved becomes Occupied. Semih's
    /// decision (2026-09-09): stock is consumed here, for every item on
    /// every channel this store serves.
    ///
    /// V1-RMD-158: that channel list used to read "Cashier/Waiter/NFC
    /// age-restricted/QR", claiming all four reach Accepted only through
    /// this method. They don't — a Cashier or Waiter order never enters
    /// PendingConfirmation at all; it goes straight Draft -&gt; Submitted and
    /// consumes stock there instead, through
    /// <c>OrderSubmissionStockDispatcher</c> (see that class's own doc
    /// comment for why). The staff member who accepts here can hold either
    /// role, but the ORDER's own channel reaching this method is always
    /// NFC (age-restricted) or QR — see
    /// <see cref="OrderStockConsumptionService"/>'s own doc comment for
    /// the shared consumption primitive both paths call. Stock consumption
    /// and the order's own Accepted write share one connection/transaction
    /// (the repository's own connection-carrying SaveAsync overload) — the
    /// same "two independent writes with no shared optimistic-concurrency
    /// check between them" shape V1-RMD-133 found and fixed for Production's
    /// batch completion. Splitting them across two commits would let stock
    /// consumption succeed and then the Accepted write fail on a stale
    /// RowVersion (e.g. a concurrent void changed the order in between),
    /// leaving stock permanently decremented for an order that never
    /// actually reached Accepted. One transaction means a missing product
    /// mapping, insufficient stock, or a stale row version all refuse the
    /// whole Accept with nothing changed at all.
    /// </summary>
    public async Task<PendingOrderConfirmationResultV1> AcceptAsync(
        Guid orderId, long expectedRowVersion, Guid actorId, string? notes, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrderNotFoundException(orderId);
        if (order.RowVersion != expectedRowVersion)
            throw new StaleOrderRowVersionException(order.Id, expectedRowVersion, order.RowVersion);
        if (order.Status != OrderState.PendingConfirmation)
            throw new OrderNotAwaitingConfirmationException(order.Id, order.Status.ToString());

        var now = DateTimeOffset.UtcNow;
        var accepted = order.TransitionTo(OrderState.Accepted, notes, actorId, now);

        long newVersion;
        bool tableReleased;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            if (order.Source == OrderSource.Qr)
            {
                // Every stock row this acceptance will touch (products and modifiers) is locked first, in the one
                // global order, before the hold locks a subset of them (V12-RMD-003).
                await _stockConsumption.LockStockRowsAsync(order.Items, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
                await ReserveQrPortionsAsync(order, expectedRowVersion, actorId, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
            }
            await _stockConsumption.ConsumeForAcceptedOrderAsync(order, actorId, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            newVersion = await _orders.SaveAsync(accepted, expectedRowVersion, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            tableReleased = await ReleaseTableAsync(order, releaseToOccupied: true, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            // V1-RMD-332 (independent 2026-09-26 audit, orta seviye bulgu): the audit row now
            // shares this transaction — a crash or connection loss between the domain write and
            // a SEPARATE audit insert used to leave an Accepted order with no audit trail at all.
            await AppendAuditAsync(order.Id, "Order.Accepted", actorId, notes, now, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new PendingOrderConfirmationResultV1(
            order.Id, accepted.Status.ToString(), newVersion, accepted.Total, 0, tableReleased, now);
    }

    /// <summary>
    /// The staff member refuses the order (e.g. the ID check failed):
    /// PendingConfirmation -&gt; Rejected, every already-dispatched kitchen
    /// ticket item is cancelled in the same transaction as the rejection
    /// (V1-RMD-313: never without it), and a table the order
    /// still holds as Reserved becomes Available again. Refuses outright if
    /// a Bill already references the order — that should never happen for a
    /// still-PendingConfirmation order, and silently proceeding would risk
    /// leaving a billed line pointing at a rejected order.
    /// </summary>
    public async Task<PendingOrderConfirmationResultV1> RejectAsync(
        Guid orderId, long expectedRowVersion, Guid actorId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        var order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrderNotFoundException(orderId);
        if (order.RowVersion != expectedRowVersion)
            throw new StaleOrderRowVersionException(order.Id, expectedRowVersion, order.RowVersion);
        if (order.Status != OrderState.PendingConfirmation)
            throw new OrderNotAwaitingConfirmationException(order.Id, order.Status.ToString());

        var bills = await _bills.GetByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (bills.Count > 0)
            throw new OrderAlreadyBilledException(order.Id);

        var now = DateTimeOffset.UtcNow;
        var rejected = order.TransitionTo(OrderState.Rejected, reason, actorId, now);
        int cancelledTicketItems;
        long newVersion;
        bool tableReleased;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            // V1-RMD-313: the kitchen cancellation commits with the rejection or not at all. If a concurrent accept
            // wins the order's version, the save below fails and the kitchen items stay as they were.
            cancelledTicketItems = await CancelAllKitchenTicketItemsAsync(orderId, reason, now, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            newVersion = await _orders.SaveAsync(rejected, expectedRowVersion, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            tableReleased = await ReleaseTableAsync(order, releaseToOccupied: false, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            // V1-RMD-332: same reasoning as AcceptAsync above — the audit row shares this
            // transaction, so a rejection is never recorded without also being audited (or vice versa).
            await AppendAuditAsync(order.Id, "Order.Rejected", actorId, reason, now, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new PendingOrderConfirmationResultV1(
            order.Id, rejected.Status.ToString(), newVersion, rejected.Total, cancelledTicketItems, tableReleased, now);
    }

    /// <summary>
    /// Mirror cancellation, inside the rejection's transaction, on every kitchen ticket item still
    /// dispatched for this order, mirroring SentItemVoidStore's own
    /// per-item pattern (V1-IAM-027) but applied to every item on every
    /// ticket rather than one — the whole order is being refused, not a
    /// single line. A ticket item already Served or Cancelled is left alone.
    /// </summary>
    private async Task<int> CancelAllKitchenTicketItemsAsync(
        Guid orderId, string reason, DateTimeOffset now, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var tickets = await _tickets.GetByOrderIdAsync(orderId, connection, transaction, cancellationToken).ConfigureAwait(false);
        var cancelledCount = 0;

        foreach (var ticket in tickets)
        {
            var updated = ticket;
            foreach (var item in ticket.Items)
            {
                if (!item.CanTransitionTo(KitchenTicketItemState.Cancelled))
                    continue;
                updated = updated.UpdateItemStatus(item.Id, KitchenTicketItemState.Cancelled, reason, now);
                cancelledCount++;
            }

            if (!ReferenceEquals(updated, ticket))
                await _tickets.SaveAsync(updated, ticket.RowVersion, connection, transaction, cancellationToken).ConfigureAwait(false);
        }

        return cancelledCount;
    }

    /// <summary>
    /// Releases a table the order still holds as Reserved — the same
    /// invariant NfcOrderingStore's self-check-in path relies on
    /// (Reserved only while current_order_id points at a not-yet-decided
    /// order). Conditional on BOTH current_order_id and current_status
    /// still matching, so a table a different order or the manual
    /// reservation flow has since claimed is never clobbered. A table-less
    /// order (should not happen for NFC, but this store is channel-agnostic)
    /// is a no-op, not an error.
    /// </summary>
    private static async Task<bool> ReleaseTableAsync(
        Order order,
        bool releaseToOccupied,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (order.TableId is not { } tableId)
            return false;

        await using var command = new NpgsqlCommand(
            """
            UPDATE table_mgmt.tables
            SET current_status = @status,
                current_order_id = CASE WHEN @release_to_occupied THEN current_order_id ELSE NULL END,
                row_version = row_version + 1
            WHERE table_id = @table_id AND current_order_id = @order_id AND current_status = 'Reserved';
            """, connection, transaction);
        command.Parameters.AddWithValue("status", releaseToOccupied ? "Occupied" : "Available");
        command.Parameters.AddWithValue("release_to_occupied", releaseToOccupied);
        command.Parameters.AddWithValue("table_id", tableId);
        command.Parameters.AddWithValue("order_id", order.Id);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    /// <summary>
    /// V12-QRO-003: a QR order claims its portions only now, on a successful accept, through the
    /// same arbiter every channel uses — so an online or other QR order already holding the last
    /// portion wins, and this accept is refused with the same typed stock errors the consumption
    /// path raises. Cancelled lines never compete for stock.
    /// </summary>
    private async Task ReserveQrPortionsAsync(
        Order order,
        long expectedRowVersion,
        Guid actorId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var lines = order.Items
            .Where(item => item.Status != OrderItemState.Cancelled)
            .Select(item => new CrossChannelReservationLine(item.Id, item.ProductId, item.Quantity))
            .ToList();
        if (lines.Count == 0)
            return;

        var result = await _portionArbiter.ReserveAsync(
            new CrossChannelReservationRequest(
                ReservationChannel.Qr,
                (order.SourceReferenceId ?? order.Id).ToString("D"),
                order.Id,
                actorId,
                lines),
            connection,
            transaction,
            cancellationToken).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case CrossChannelReservationOutcome.Reserved:
            case CrossChannelReservationOutcome.Replayed:
                return;
            case CrossChannelReservationOutcome.AlreadyConsumed:
            {
                // A concurrent acceptance of this order committed first: its version is the one to report.
                var current = await _orders.GetByIdAsync(order.Id, cancellationToken).ConfigureAwait(false);
                throw new StaleOrderRowVersionException(order.Id, expectedRowVersion, current?.RowVersion ?? expectedRowVersion);
            }
            case CrossChannelReservationOutcome.OutOfStock:
            {
                var shortage = result.Shortages[0];
                var item = order.Items.First(i => i.Id == shortage.OrderItemIds[0]);
                var stockItem = await _stockItems.GetByIdAsync(shortage.StockItemId, cancellationToken).ConfigureAwait(false)
                    ?? throw new StockItemNotFoundException(shortage.StockItemId);
                throw new InsufficientOrderStockException(item.ProductId, item.ProductNameSnapshot, stockItem.Name);
            }
            case CrossChannelReservationOutcome.NotConfigured:
                throw await UnconfiguredStockExceptionAsync(order, result.UnconfiguredLines[0], cancellationToken)
                    .ConfigureAwait(false);
            default:
                throw new InvalidOperationException($"Unhandled reservation outcome '{result.Outcome}'.");
        }
    }

    private async Task<Exception> UnconfiguredStockExceptionAsync(
        Order order, UnconfiguredLine line, CancellationToken cancellationToken)
    {
        var item = order.Items.First(i => i.Id == line.OrderItemId);
        if (line.Gap == StockConfigurationGap.StockItemHasNoDefaultLocation)
        {
            foreach (var mapping in await _stockMappings.GetByProductIdAsync(item.ProductId, cancellationToken).ConfigureAwait(false))
            {
                var stockItem = await _stockItems.GetByIdAsync(mapping.StockItemId, cancellationToken).ConfigureAwait(false);
                if (stockItem is { DefaultLocationId: null })
                    return new StockItemHasNoDefaultLocationException(stockItem.Id, stockItem.Name);
            }
        }

        return new ProductStockNotConfiguredException(item.ProductId, item.ProductNameSnapshot);
    }

    private static async Task AppendAuditAsync(
        Guid orderId, string eventName, Guid actorId, string? reason, DateTimeOffset now,
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit.audit_events (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json,
                metadata_json, occurred_at
            ) VALUES (
                @id, @event_name, 'Order', @aggregate_id, @actor_id, 'User',
                @reason, @correlation_id, NULL, NULL, NULL, NULL, @occurred_at
            );
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("event_name", eventName);
        command.Parameters.AddWithValue("aggregate_id", orderId);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        command.Parameters.AddWithValue("correlation_id", orderId.ToString("D"));
        command.Parameters.AddWithValue("occurred_at", now);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
