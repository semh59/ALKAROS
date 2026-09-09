using ALKAROS.Billing.BillFoundation;
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
/// QR's own portion-reservation-on-accept is out of scope: QR has no HTTP
/// surface yet to ever reach a PendingConfirmation order through (see the
/// 2026-09-09 QR/NFC audit's finding #0).
/// </summary>
public sealed class PendingOrderConfirmationStore
{
    private readonly IOrderRepository _orders;
    private readonly IKitchenTicketRepository _tickets;
    private readonly IBillRepository _bills;
    private readonly NpgsqlDataSource _dataSource;

    public PendingOrderConfirmationStore(
        IOrderRepository orders,
        IKitchenTicketRepository tickets,
        IBillRepository bills,
        NpgsqlDataSource dataSource)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <summary>
    /// The staff member confirms the ID check passed (or otherwise clears
    /// the order for service): PendingConfirmation -&gt; Accepted, and a
    /// table the order still holds as Reserved becomes Occupied.
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
        var newVersion = await _orders.SaveAsync(accepted, expectedRowVersion, cancellationToken).ConfigureAwait(false);

        var tableReleased = await ReleaseTableAsync(order, releaseToOccupied: true, cancellationToken).ConfigureAwait(false);
        await AppendAuditAsync(order.Id, "Order.Accepted", actorId, notes, now, cancellationToken).ConfigureAwait(false);

        return new PendingOrderConfirmationResultV1(
            order.Id, accepted.Status.ToString(), newVersion, accepted.Total, 0, tableReleased, now);
    }

    /// <summary>
    /// The staff member refuses the order (e.g. the ID check failed):
    /// PendingConfirmation -&gt; Rejected, every already-dispatched kitchen
    /// ticket item is cancelled (best effort — the Order write is still the
    /// authority on whether the order is rejected), and a table the order
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
        var cancelledTicketItems = await CancelAllKitchenTicketItemsAsync(orderId, reason, now, cancellationToken)
            .ConfigureAwait(false);

        var rejected = order.TransitionTo(OrderState.Rejected, reason, actorId, now);
        var newVersion = await _orders.SaveAsync(rejected, expectedRowVersion, cancellationToken).ConfigureAwait(false);

        var tableReleased = await ReleaseTableAsync(order, releaseToOccupied: false, cancellationToken).ConfigureAwait(false);
        await AppendAuditAsync(order.Id, "Order.Rejected", actorId, reason, now, cancellationToken).ConfigureAwait(false);

        return new PendingOrderConfirmationResultV1(
            order.Id, rejected.Status.ToString(), newVersion, rejected.Total, cancelledTicketItems, tableReleased, now);
    }

    /// <summary>
    /// Best-effort mirror cancellation on every kitchen ticket item still
    /// dispatched for this order, mirroring SentItemVoidStore's own
    /// per-item pattern (V1-IAM-027) but applied to every item on every
    /// ticket rather than one — the whole order is being refused, not a
    /// single line. A ticket item already Served or Cancelled is left alone.
    /// </summary>
    private async Task<int> CancelAllKitchenTicketItemsAsync(
        Guid orderId, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tickets = await _tickets.GetByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
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
                await _tickets.SaveAsync(updated, ticket.RowVersion, cancellationToken).ConfigureAwait(false);
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
    private async Task<bool> ReleaseTableAsync(Order order, bool releaseToOccupied, CancellationToken cancellationToken)
    {
        if (order.TableId is not { } tableId)
            return false;

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE table_mgmt.tables
            SET current_status = @status,
                current_order_id = CASE WHEN @release_to_occupied THEN current_order_id ELSE NULL END,
                row_version = row_version + 1
            WHERE table_id = @table_id AND current_order_id = @order_id AND current_status = 'Reserved';
            """);
        command.Parameters.AddWithValue("status", releaseToOccupied ? "Occupied" : "Available");
        command.Parameters.AddWithValue("release_to_occupied", releaseToOccupied);
        command.Parameters.AddWithValue("table_id", tableId);
        command.Parameters.AddWithValue("order_id", order.Id);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private async Task AppendAuditAsync(
        Guid orderId, string eventName, Guid actorId, string? reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO audit.audit_events (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json,
                metadata_json, occurred_at
            ) VALUES (
                @id, @event_name, 'Order', @aggregate_id, @actor_id, 'User',
                @reason, @correlation_id, NULL, NULL, NULL, NULL, @occurred_at
            );
            """);
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
