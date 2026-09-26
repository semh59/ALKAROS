using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-ONL-002: processes stored Yemeksepeti webhook events one at a time. A new provider order
/// becomes exactly one internal Accepted order, and in the same transaction its portions are
/// held through the cross-channel arbiter (V12-STK-001), its kitchen ticket is dispatched and the
/// inbox event is marked processed. If any line cannot be mapped, or the last portion is already
/// held elsewhere, no order is written at all and the event records the typed reason instead.
///
/// The order walks Draft -&gt; Submitted -&gt; PendingConfirmation -&gt; Accepted like NFC's trusted
/// channel (no new transition). Its holds stay Reserved: an online order is not consumed at
/// acceptance, so a provider cancellation can still end in exactly one Release or Waste
/// (V11-RSV-003, handled by V12-ONL-003). This orchestration lives in the Host because it spans
/// Order, Kitchen and Inventory in one transaction; the Online Ordering module may call only
/// Catalog directly (module-dependency-rules.md row 20).
/// </summary>
public sealed class YemeksepetiOrderIntakeService
{
    /// <summary>The actor recorded for holds and order history written by webhook processing.</summary>
    public static readonly Guid SystemActorId = new("00000000-0000-0000-0000-0000000005e7");

    /// <summary>V12-ONL-003: every status is claimed here now; cancellations are applied by the status sync.</summary>
    private static readonly string[] DeferredStatuses = [];

    /// <summary>
    /// V12-ONL-003: refusals the provider hears back about as ITEM_UNAVAILABLE — the order's items
    /// themselves cannot be sold here. A structurally broken payload is left for review instead.
    /// </summary>
    private static readonly HashSet<NormalizationRejection> ItemUnavailableRejections =
    [
        NormalizationRejection.UnmappedSku,
        NormalizationRejection.AmbiguousSku,
        NormalizationRejection.ProductInactive,
        NormalizationRejection.ProductRequiresModifierChoice,
        NormalizationRejection.ProductHasNoTaxProfile,
        NormalizationRejection.UnsupportedPricingType
    ];

    private const string AcceptReason = "Yemeksepeti siparişi - sağlayıcı tarafından kabul edildi.";

    private readonly NpgsqlDataSource _dataSource;
    private readonly YemeksepetiWebhookInbox _inbox;
    private readonly YemeksepetiOrderNormalizer _normalizer;
    private readonly IOrderRepository _orders;
    private readonly IOrderSubmissionDispatcher _dispatcher;
    private readonly ICrossChannelPortionArbiter _arbiter;
    private readonly YemeksepetiStatusSyncService _statusSync;

    public YemeksepetiOrderIntakeService(
        NpgsqlDataSource dataSource,
        YemeksepetiWebhookInbox inbox,
        YemeksepetiOrderNormalizer normalizer,
        IOrderRepository orders,
        IOrderSubmissionDispatcher dispatcher,
        ICrossChannelPortionArbiter arbiter,
        YemeksepetiStatusSyncService statusSync)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        _statusSync = statusSync ?? throw new ArgumentNullException(nameof(statusSync));
    }

    /// <summary>Processes the oldest pending event, if any. Returns false when nothing was waiting.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var claimed = await YemeksepetiInboxProcessingStore.ClaimNextAsync(DeferredStatuses, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (claimed is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        try
        {
            var rawPayload = _inbox.OpenPayload(claimed.PayloadEnvelope);
            var mapping = YemeksepetiStatusMapper.Map(
                YemeksepetiInboxProcessingStore.ReadStatusSignal(claimed.ExternalOrderId, claimed.ProviderStatus, rawPayload));

            switch (mapping.Kind)
            {
                case StatusMappingKind.NoOp:
                    await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                        claimed.InboxId, InboxProcessingOutcome.NoOp, null, new { reason = mapping.NoOpReason!.Value.ToString() },
                        connection, transaction, cancellationToken).ConfigureAwait(false);
                    break;
                case StatusMappingKind.Unknown:
                    await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                        claimed.InboxId, InboxProcessingOutcome.UnknownStatus, null, mapping.Evidence,
                        connection, transaction, cancellationToken).ConfigureAwait(false);
                    break;
                case StatusMappingKind.Command when mapping.Command == InternalOrderCommand.AcceptIncomingOrder:
                    await IntakeAsync(claimed, rawPayload, connection, transaction, cancellationToken).ConfigureAwait(false);
                    break;
                case StatusMappingKind.Command when mapping.Command == InternalOrderCommand.CancelOrder:
                    await _statusSync.ApplyProviderCancellationAsync(
                        claimed, mapping.Cancellation!, connection, transaction, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Inbox event '{claimed.InboxId}' mapped to {mapping.Kind}/{mapping.Command}, which this processor never claims.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // V12-RMD-004: only the host shutting down leaves an attempt uncounted; a timeout is a failure like
            // any other and must use up an attempt, or the event would be retried forever.
            // Nothing of this attempt survives the rollback; the failure itself is recorded on a
            // separate connection so the event is retried behind healthier ones, then closed.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            await using var failureConnection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await YemeksepetiInboxProcessingStore.RecordFailureAsync(
                claimed.InboxId, ex.GetType().Name, failureConnection, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task IntakeAsync(
        ClaimedInboxEvent claimed,
        string rawPayload,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        // Two different events of the same provider order (a retried RECEIVED with a new update
        // time) must never become two orders; serialize per provider order id — the same lock the
        // V12-ONL-003 status sync takes, so a cancellation racing this intake closes deterministically.
        await YemeksepetiStatusSyncService.LockOrderAsync(claimed.ExternalOrderId, connection, transaction, cancellationToken)
            .ConfigureAwait(false);

        if (await ProviderAlreadyCancelledAsync(claimed.ExternalOrderId, connection, transaction, cancellationToken).ConfigureAwait(false))
        {
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.SkippedCancelledOrder, null, null,
                connection, transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await CancellationAlreadyRequestedAsync(claimed.ExternalOrderId, connection, transaction, cancellationToken).ConfigureAwait(false))
        {
            // V12-RMD-004: an earlier event of this order was refused and the provider was asked to cancel it; a new
            // RECEIVED (a fresh update time) must not now create the order the provider was told cannot be made.
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.SkippedCancellationRequested, null, null,
                connection, transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await FindOrderAsync(claimed.ExternalOrderId, connection, transaction, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.OrderAlreadyExists, existing, null,
                connection, transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        var normalized = await _normalizer.NormalizeAsync(rawPayload, claimed.ReceivedAt, cancellationToken).ConfigureAwait(false);
        if (normalized.Order is not { } onlineOrder)
        {
            var itemsUnavailable = ItemUnavailableRejections.Contains(normalized.Rejection!.Value);
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.Rejected, null,
                new { rejection = normalized.Rejection!.Value.ToString(), detail = normalized.Detail, providerCancellationRequested = itemsUnavailable },
                connection, transaction, cancellationToken).ConfigureAwait(false);
            var references = YemeksepetiStatusSync.ReadItemReferences(rawPayload);
            if (itemsUnavailable && references.Count > 0)
            {
                await YemeksepetiStatusSyncService.RequestProviderCancellationAsync(
                    claimed.ExternalOrderId, references, connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        var items = onlineOrder.Lines
            .Select(line => new OrderItem(
                Guid.NewGuid(), orderId, line.ProductId, line.ProductName, line.Quantity, line.UnitPrice, line.TaxRate,
                skuSnapshot: line.ExternalSku, notes: line.Instructions, createdAt: now, updatedAt: now))
            .ToList();

        // Stock first: a refusal writes nothing, so there is never an order without its holds.
        var hold = await _arbiter.ReserveAsync(
            new CrossChannelReservationRequest(
                ReservationChannel.Online,
                onlineOrder.ExternalOrderId,
                orderId,
                SystemActorId,
                items.Select(item => new CrossChannelReservationLine(item.Id, item.ProductId, item.Quantity)).ToList()),
            connection, transaction, cancellationToken).ConfigureAwait(false);
        if (hold.Outcome == CrossChannelReservationOutcome.AlreadyConsumed)
        {
            // Cannot happen for a fresh order id; if it ever does, it is not a stock refusal and must never ask
            // the provider to cancel. Failing lets processing retry, where the existing order is found first.
            throw new InvalidOperationException($"Online order '{orderId}' already had its holds consumed.");
        }

        if (!hold.IsHeld)
        {
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.Diverged, null,
                new
                {
                    reason = hold.Outcome.ToString(),
                    shortages = hold.Shortages.Select(s => new { s.StockItemId, s.RequiredQuantity, s.AvailableQuantity }),
                    unconfigured = hold.UnconfiguredLines.Select(u => new { u.ProductId, gap = u.Gap.ToString() }),
                    providerCancellationRequested = true
                },
                connection, transaction, cancellationToken).ConfigureAwait(false);
            await YemeksepetiStatusSyncService.RequestProviderCancellationAsync(
                onlineOrder.ExternalOrderId,
                onlineOrder.Lines.Select(line => new YemeksepetiOrderLineReference(line.ExternalSku, line.Quantity)).ToList(),
                connection, transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        var order = new Order(
            orderId,
            OrderSource.Online,
            "YS-" + onlineOrder.ExternalOrderId[..Math.Min(47, onlineOrder.ExternalOrderId.Length)],
            items,
            sourceExternalId: onlineOrder.ExternalOrderId,
            notes: onlineOrder.Comment is null
                ? $"Yemeksepeti {onlineOrder.DisplayCode}"
                : $"Yemeksepeti {onlineOrder.DisplayCode}: {onlineOrder.Comment}",
            status: OrderState.Draft,
            createdAt: now,
            updatedAt: now,
            rowVersion: 1);
        await _orders.AddAsync(order, connection, transaction, cancellationToken).ConfigureAwait(false);

        var (submitted, fired) = order.FireRound(AcceptReason, SystemActorId, now);
        var version = await _orders.SaveAsync(submitted, order.RowVersion, connection, transaction, cancellationToken).ConfigureAwait(false);
        await _dispatcher.DispatchAsync(submitted, fired, connection, transaction, cancellationToken).ConfigureAwait(false);

        var pending = submitted.WithRowVersion(version).TransitionTo(OrderState.PendingConfirmation, AcceptReason, SystemActorId, now);
        version = await _orders.SaveAsync(pending, version, connection, transaction, cancellationToken).ConfigureAwait(false);
        var accepted = pending.WithRowVersion(version).TransitionTo(OrderState.Accepted, AcceptReason, SystemActorId, now);
        await _orders.SaveAsync(accepted, version, connection, transaction, cancellationToken).ConfigureAwait(false);

        await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
            claimed.InboxId, InboxProcessingOutcome.OrderCreated, orderId,
            new
            {
                displayCode = onlineOrder.DisplayCode,
                transportType = onlineOrder.TransportType,
                totalsMatch = onlineOrder.TotalsMatch,
                providerSubTotal = onlineOrder.ProviderSubTotal,
                localSubTotal = onlineOrder.LocalSubTotal
            },
            connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> CancellationAlreadyRequestedAsync(
        string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM online_ordering.yemeksepeti_webhook_inbox
                WHERE external_order_id = $1
                  AND processing_outcome IN ('Rejected', 'Diverged')
                  AND COALESCE((outcome_detail->>'providerCancellationRequested')::boolean, false));
            """, connection, transaction);
        command.Parameters.AddWithValue(externalOrderId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task<bool> ProviderAlreadyCancelledAsync(
        string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM online_ordering.yemeksepeti_webhook_inbox
                WHERE external_order_id = $1 AND processing_outcome = 'CancelledBeforeOrder');
            """, connection, transaction);
        command.Parameters.AddWithValue(externalOrderId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task<Guid?> FindOrderAsync(
        string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT order_id FROM orders.orders WHERE source = 'Online' AND source_external_id = $1 LIMIT 1;",
            connection, transaction);
        command.Parameters.AddWithValue(externalOrderId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as Guid?;
    }
}
