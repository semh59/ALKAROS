using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

public enum OnlineOrderActionOutcome
{
    Applied,

    /// <summary>The order already is in the requested end state; nothing was done again.</summary>
    AlreadyApplied,

    /// <summary>The order's state does not allow the action (e.g. cancelling an order already handed over).</summary>
    NotAllowed,

    /// <summary>V12-OUI-001: the caller acted on an older version of the order; nothing was changed.</summary>
    Stale
}

/// <summary>
/// V12-ONL-003: keeps an online order and the provider in step. Every change runs under the same
/// per-provider-order lock as intake, so an intake, a provider cancellation and a restaurant action
/// on one order are serialized and close deterministically.
///
/// Stock follows V11-RSV-003 without re-implementing it: a cancellation hands every hold to the
/// cross-channel arbiter's compensation, whose cancellation decision alone chooses Release (kitchen not
/// started) or Waste (preparation started) and replays safely, so a crash and retry never repeats a
/// stock effect. Compensation runs before the kitchen items are cancelled, so the decision still sees
/// the real preparation state. A handover consumes the holds through the ordinary acceptance
/// consumption, which turns them into Consumed. Nothing here creates a ReconciliationCase; an
/// unresolvable difference is recorded as typed evidence on the inbox event.
///
/// Provider-bound changes are queued in the outbox in the same transaction as the local change
/// (UNVERIFIED DRAFT provider client — see YemeksepetiPartnerHttpClient).
/// </summary>
public sealed class YemeksepetiStatusSyncService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _orders;
    private readonly IKitchenTicketRepository _tickets;
    private readonly ICrossChannelPortionArbiter _arbiter;
    private readonly OrderStockConsumptionService _consumption;

    public YemeksepetiStatusSyncService(
        NpgsqlDataSource dataSource,
        IOrderRepository orders,
        IKitchenTicketRepository tickets,
        ICrossChannelPortionArbiter arbiter,
        OrderStockConsumptionService consumption)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        _consumption = consumption ?? throw new ArgumentNullException(nameof(consumption));
    }

    /// <summary>The provider cancelled an order: applied inside the inbox processing transaction.</summary>
    public async Task ApplyProviderCancellationAsync(
        ClaimedInboxEvent claimed,
        CancellationDetail cancellation,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimed);
        ArgumentNullException.ThrowIfNull(cancellation);
        await LockOrderAsync(claimed.ExternalOrderId, connection, transaction, cancellationToken).ConfigureAwait(false);
        var detail = new { party = cancellation.Party.ToString(), reason = cancellation.Reason, afterPickup = cancellation.AfterPickup };

        var order = await FindOrderAsync(claimed.ExternalOrderId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            // Nothing local to undo (intake refused it, or its RECEIVED event has not been processed
            // yet — intake then sees this cancellation and never creates the order).
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.CancelledBeforeOrder, null, detail,
                connection, transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        var reason = $"Yemeksepeti iptali ({cancellation.Party})";
        var outcome = await CancelLocallyAsync(order, reason, YemeksepetiIntakeActor, connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        switch (outcome)
        {
            case OnlineOrderActionOutcome.Applied:
                await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                    claimed.InboxId, InboxProcessingOutcome.OrderCancelled, order.Id, detail,
                    connection, transaction, cancellationToken).ConfigureAwait(false);
                break;
            case OnlineOrderActionOutcome.AlreadyApplied:
                await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                    claimed.InboxId, InboxProcessingOutcome.AlreadyCancelled, order.Id, detail,
                    connection, transaction, cancellationToken).ConfigureAwait(false);
                break;
            default:
                // Handed over (or otherwise past cancelling) locally, cancelled at the provider: an
                // unresolvable difference. Its evidence id depends only on the order and the kind of
                // divergence, so every repeat of it carries the same evidence.
                await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                    claimed.InboxId, InboxProcessingOutcome.Diverged, order.Id,
                    new
                    {
                        reason = "CancelledAfterHandover",
                        evidenceId = EvidenceId("CancelledAfterHandover", claimed.ExternalOrderId),
                        localStatus = order.Status.ToString(),
                        cancellation = detail
                    },
                    connection, transaction, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// The restaurant hands an online order to the courier: its holds are consumed, the order reaches
    /// Served and the provider is told (READY_FOR_PICKUP for a platform courier, DISPATCHED for the
    /// restaurant's own). Repeating it changes nothing.
    /// </summary>
    public async Task<OnlineOrderActionOutcome> HandOverAsync(
        Guid orderId, Guid actorId, long? expectedRowVersion = null, CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("A handover needs an actor.", nameof(actorId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var order = await LoadOnlineOrderAsync(orderId, connection, transaction, cancellationToken).ConfigureAwait(false);

        if (order.Status is OrderState.Served or OrderState.Completed)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return OnlineOrderActionOutcome.AlreadyApplied;
        }
        if (expectedRowVersion is { } expectedForHandover && order.RowVersion != expectedForHandover)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return OnlineOrderActionOutcome.Stale;
        }
        if (order.Status is not (OrderState.Accepted or OrderState.Preparing or OrderState.Ready))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return OnlineOrderActionOutcome.NotAllowed;
        }

        var handover = YemeksepetiStatusSync.HandoverStatusFor(
            await TransportTypeAsync(order.Id, connection, transaction, cancellationToken).ConfigureAwait(false))
            ?? throw new InvalidOperationException($"Online order '{order.Id}' has no documented delivery kind to report a handover for.");

        await _consumption.ConsumeForAcceptedOrderAsync(order, actorId, connection, transaction, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var current = order;
        foreach (var next in new[] { OrderState.Preparing, OrderState.Ready, OrderState.Served })
        {
            if (!current.CanTransitionTo(next))
                continue;
            var moved = current.TransitionTo(next, "Yemeksepeti siparişi kuryeye teslim edildi.", actorId, now);
            var version = await _orders.SaveAsync(moved, current.RowVersion, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            current = moved.WithRowVersion(version);
        }

        await YemeksepetiStatusSync.EnqueueAsync(
            new YemeksepetiStatusUpdateRequested(Guid.NewGuid(), order.SourceExternalId!, handover, null, LineReferences(order), now),
            connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OnlineOrderActionOutcome.Applied;
    }

    /// <summary>The restaurant cannot fulfil an online order: it is cancelled locally and the provider is told why.</summary>
    public async Task<OnlineOrderActionOutcome> CancelByRestaurantAsync(
        Guid orderId,
        YemeksepetiCancellationReason reason,
        Guid actorId,
        long? expectedRowVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("A cancellation needs an actor.", nameof(actorId));
        if (!Enum.IsDefined(reason))
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Only a documented cancellation reason can be sent.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var order = await LoadOnlineOrderAsync(orderId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (order.Status != OrderState.Cancelled && expectedRowVersion is { } expected && order.RowVersion != expected)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return OnlineOrderActionOutcome.Stale;
        }

        var outcome = await CancelLocallyAsync(
            order, $"Restoran iptali ({reason})", actorId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (outcome == OnlineOrderActionOutcome.Applied)
        {
            await YemeksepetiStatusSync.EnqueueAsync(
                new YemeksepetiStatusUpdateRequested(
                    Guid.NewGuid(), order.SourceExternalId!, YemeksepetiOutboundStatus.Cancelled, reason, LineReferences(order),
                    DateTimeOffset.UtcNow),
                connection, transaction, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    /// <summary>Intake refused a provider order the provider already accepted: tell the provider it cannot be fulfilled.</summary>
    public static Task RequestProviderCancellationAsync(
        string externalOrderId,
        IReadOnlyList<YemeksepetiOrderLineReference> items,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default) =>
        YemeksepetiStatusSync.EnqueueAsync(
            new YemeksepetiStatusUpdateRequested(
                Guid.NewGuid(), externalOrderId, YemeksepetiOutboundStatus.Cancelled, YemeksepetiCancellationReason.ItemUnavailable,
                items, DateTimeOffset.UtcNow),
            connection, transaction, cancellationToken);

    /// <summary>The same lock intake takes, so every change to one provider order is serialized.</summary>
    public static async Task LockOrderAsync(
        string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext('online-ordering.yemeksepeti-order:' || $1));", connection, transaction);
        command.Parameters.AddWithValue(externalOrderId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static readonly Guid YemeksepetiIntakeActor = YemeksepetiOrderIntakeService.SystemActorId;

    private async Task<OnlineOrderActionOutcome> CancelLocallyAsync(
        Order order, string reason, Guid actorId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        if (order.Status == OrderState.Cancelled)
            return OnlineOrderActionOutcome.AlreadyApplied;
        if (!order.CanTransitionTo(OrderState.Cancelled))
            return OnlineOrderActionOutcome.NotAllowed;

        // Stock first, while the kitchen items still show how far preparation got.
        await _arbiter.CompensateAsync(order.Id, actorId, reason, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        foreach (var ticket in await _tickets.GetByOrderIdAsync(order.Id, cancellationToken).ConfigureAwait(false))
        {
            var updated = ticket;
            foreach (var item in ticket.Items.Where(i => i.CanTransitionTo(KitchenTicketItemState.Cancelled)))
                updated = updated.UpdateItemStatus(item.Id, KitchenTicketItemState.Cancelled, reason, now);
            if (!ReferenceEquals(updated, ticket))
                await _tickets.SaveAsync(updated, ticket.RowVersion, cancellationToken).ConfigureAwait(false);
        }

        await _orders.SaveAsync(
            order.TransitionTo(OrderState.Cancelled, reason, actorId, now), order.RowVersion, connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        return OnlineOrderActionOutcome.Applied;
    }

    private async Task<Order> LoadOnlineOrderAsync(
        Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrderNotFoundException(orderId);
        if (order.Source != OrderSource.Online || string.IsNullOrEmpty(order.SourceExternalId))
            throw new NotAnOnlineOrderException(orderId);

        await LockOrderAsync(order.SourceExternalId, connection, transaction, cancellationToken).ConfigureAwait(false);
        // Re-read under the lock: a concurrent intake/cancellation/handover of the same order has committed by now.
        return await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false) ?? throw new OrderNotFoundException(orderId);
    }

    private async Task<Order?> FindOrderAsync(
        string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT order_id FROM orders.orders WHERE source = 'Online' AND source_external_id = $1 LIMIT 1;", connection, transaction);
        command.Parameters.AddWithValue(externalOrderId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is Guid orderId
            ? await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static async Task<string?> TransportTypeAsync(
        Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT outcome_detail->>'transportType'
            FROM online_ordering.yemeksepeti_webhook_inbox
            WHERE order_id = $1 AND processing_outcome = 'OrderCreated'
            LIMIT 1;
            """, connection, transaction);
        command.Parameters.AddWithValue(orderId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private static List<YemeksepetiOrderLineReference> LineReferences(Order order) =>
        order.Items
            .Where(item => !string.IsNullOrEmpty(item.SkuSnapshot))
            .Select(item => new YemeksepetiOrderLineReference(item.SkuSnapshot!, item.Quantity))
            .ToList();

    private static Guid EvidenceId(string kind, string externalOrderId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(kind + "\u001f" + externalOrderId)).AsSpan(0, 16));
}

/// <summary>The order exists but did not come from an online channel, so online actions do not apply to it.</summary>
public sealed class NotAnOnlineOrderException : Exception
{
    public NotAnOnlineOrderException(Guid orderId) : base($"Order '{orderId}' is not an online channel order.") { }
}
