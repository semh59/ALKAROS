using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.OnlineOrdering.OrderLinks;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
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
    private readonly OnlineOrderProviderRegistry _providers;

    public YemeksepetiStatusSyncService(
        NpgsqlDataSource dataSource,
        IOrderRepository orders,
        IKitchenTicketRepository tickets,
        ICrossChannelPortionArbiter arbiter,
        OrderStockConsumptionService consumption,
        OnlineOrderProviderRegistry providers)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        _consumption = consumption ?? throw new ArgumentNullException(nameof(consumption));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
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
        // The event came from the Yemeksepeti inbox (the shared inbox arrives with V12-ONL-008).
        var provider = _providers.Get(OnlineOrderProviders.Yemeksepeti);
        await LockOrderAsync(provider.Provider, claimed.ExternalOrderId, connection, transaction, cancellationToken).ConfigureAwait(false);
        var detail = new { party = cancellation.Party.ToString(), reason = cancellation.Reason, afterPickup = cancellation.AfterPickup };

        var order = await FindOrderAsync(provider.Provider, claimed.ExternalOrderId, connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        if (order is null)
        {
            // Nothing local to undo (intake refused it, or its RECEIVED event has not been processed
            // yet — intake then sees this cancellation and never creates the order).
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.CancelledBeforeOrder, null, detail,
                connection, transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        var reason = $"{provider.DisplayName} iptali ({cancellation.Party})";
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
    /// restaurant's own). Repeating it changes nothing. Since V12-RMD-008 the order then closes as Completed:
    /// the platform settles the payment and the restaurant's part ends at handover, so the order leaves every
    /// open-order view and the closed-order retention rules apply to it. A provider cancellation that comes
    /// after this is recorded as divergence evidence, as before.
    /// </summary>
    public async Task<OnlineOrderActionOutcome> HandOverAsync(
        Guid orderId, Guid actorId, long? expectedRowVersion = null, CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("A handover needs an actor.", nameof(actorId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var (order, provider, externalOrderId) = await LoadOnlineOrderAsync(orderId, connection, transaction, cancellationToken)
            .ConfigureAwait(false);

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

        var handover = await provider.HandoverStatusAsync(order.Id, connection, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new OnlineOrderHandoverNotSupportedException(order.Id);

        await _consumption.ConsumeForAcceptedOrderAsync(order, actorId, connection, transaction, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var current = order;
        foreach (var next in new[] { OrderState.Preparing, OrderState.Ready, OrderState.Served, OrderState.Completed })
        {
            if (!current.CanTransitionTo(next))
                continue;
            var reason = next == OrderState.Completed
                ? $"{provider.DisplayName} siparişi teslim edildi; ödemesi platform üzerinden."
                : $"{provider.DisplayName} siparişi kuryeye teslim edildi.";
            var moved = current.TransitionTo(next, reason, actorId, now);
            var version = await _orders.SaveAsync(moved, current.RowVersion, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
            current = moved.WithRowVersion(version);
        }

        await provider.RequestStatusAsync(
            new OnlineOrderStatusRequest(externalOrderId, handover, null, LineReferences(order), now),
            connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return OnlineOrderActionOutcome.Applied;
    }

    /// <summary>The restaurant cannot fulfil an online order: it is cancelled locally and the provider is told why.</summary>
    public async Task<OnlineOrderActionOutcome> CancelByRestaurantAsync(
        Guid orderId,
        OnlineCancellationReason reason,
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
        var (order, provider, externalOrderId) = await LoadOnlineOrderAsync(orderId, connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        if (order.Status != OrderState.Cancelled && expectedRowVersion is { } expected && order.RowVersion != expected)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return OnlineOrderActionOutcome.Stale;
        }

        var outcome = await CancelLocallyAsync(
            order, $"Restoran iptali ({reason})", actorId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (outcome == OnlineOrderActionOutcome.Applied)
        {
            await provider.RequestStatusAsync(
                new OnlineOrderStatusRequest(
                    externalOrderId, OnlineOutboundStatus.Cancelled, reason, LineReferences(order), DateTimeOffset.UtcNow),
                connection, transaction, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    /// <summary>
    /// The same lock intake takes, so every change to one platform order is serialized. The key names the platform
    /// too (V12-ONL-007): the same number on two platforms is two orders and never waits on the other.
    /// </summary>
    public static async Task LockOrderAsync(
        string provider, string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext('online-ordering.order:' || $1 || ':' || $2));", connection, transaction);
        command.Parameters.AddWithValue(provider);
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

        // V12-RMD-008: the order's kitchen tickets are locked first, in this transaction. Every kitchen write
        // updates its ticket row before its items, so no preparation can start between the stock decision below
        // and the cancellation after it; and if this transaction rolls back, the kitchen cancellation does too.
        var tickets = await _tickets.GetByOrderIdAsync(order.Id, connection, transaction, cancellationToken).ConfigureAwait(false);

        // Stock first, while the kitchen items still show how far preparation got. It joins this transaction
        // (V1-RMD-310): if the order change below fails, the holds are not released either.
        await _arbiter.CompensateAsync(order.Id, actorId, reason, connection, transaction, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        foreach (var ticket in tickets)
        {
            var updated = ticket;
            foreach (var item in ticket.Items.Where(i => i.CanTransitionTo(KitchenTicketItemState.Cancelled)))
                updated = updated.UpdateItemStatus(item.Id, KitchenTicketItemState.Cancelled, reason, now);
            if (!ReferenceEquals(updated, ticket))
                await _tickets.SaveAsync(updated, ticket.RowVersion, connection, transaction, cancellationToken).ConfigureAwait(false);
        }

        await _orders.SaveAsync(
            order.TransitionTo(OrderState.Cancelled, reason, actorId, now), order.RowVersion, connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        return OnlineOrderActionOutcome.Applied;
    }

    /// <summary>
    /// The order, the platform it came from (V12-ONL-006 link) and that platform's order number, read again under
    /// the platform order lock. An order without a platform link is not an online order.
    /// </summary>
    private async Task<(Order Order, IOnlineOrderProvider Provider, string ExternalOrderId)> LoadOnlineOrderAsync(
        Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrderNotFoundException(orderId);
        if (order.Source != OrderSource.Online
            || await OnlineOrderLinkStore.FindLinkAsync(orderId, connection, transaction, cancellationToken).ConfigureAwait(false)
                is not { } link)
            throw new NotAnOnlineOrderException(orderId);

        var provider = _providers.Get(link.Provider);
        await LockOrderAsync(provider.Provider, link.ExternalOrderId, connection, transaction, cancellationToken).ConfigureAwait(false);
        // Re-read under the lock: a concurrent intake/cancellation/handover of the same order has committed by now.
        var current = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false) ?? throw new OrderNotFoundException(orderId);
        return (current, provider, link.ExternalOrderId);
    }

    private async Task<Order?> FindOrderAsync(
        string provider, string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        // V12-ONL-006: a provider order number means something only together with its platform.
        return await OnlineOrderLinkStore.FindOrderIdAsync(provider, externalOrderId, connection, transaction, cancellationToken)
            .ConfigureAwait(false) is { } orderId
            ? await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static List<OnlineOrderLineReference> LineReferences(Order order) =>
        order.Items
            .Where(item => !string.IsNullOrEmpty(item.SkuSnapshot))
            .Select(item => new OnlineOrderLineReference(item.SkuSnapshot!, item.Quantity))
            .ToList();

    private static Guid EvidenceId(string kind, string externalOrderId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(kind + "\u001f" + externalOrderId)).AsSpan(0, 16));
}

/// <summary>
/// V12-RMD-008: the order's delivery kind is not one the provider documents a handover status for, so a handover
/// cannot be reported (intake records such an order as unknown; a person resolves it).
/// </summary>
public sealed class OnlineOrderHandoverNotSupportedException : Exception
{
    public OnlineOrderHandoverNotSupportedException(Guid orderId)
        : base($"Online order '{orderId}' has no documented delivery kind to report a handover for.") { }
}

/// <summary>The order exists but did not come from an online channel, so online actions do not apply to it.</summary>
public sealed class NotAnOnlineOrderException : Exception
{
    public NotAnOnlineOrderException(Guid orderId) : base($"Order '{orderId}' is not an online channel order.") { }
}
