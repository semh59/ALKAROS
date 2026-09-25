using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusMapping;
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

    /// <summary>Cancellations are claimed by the V12-ONL-003 status sync, not here.</summary>
    private static readonly string[] DeferredStatuses = ["CANCELLED"];

    private const string AcceptReason = "Yemeksepeti siparişi - sağlayıcı tarafından kabul edildi.";

    private readonly NpgsqlDataSource _dataSource;
    private readonly YemeksepetiWebhookInbox _inbox;
    private readonly YemeksepetiOrderNormalizer _normalizer;
    private readonly IOrderRepository _orders;
    private readonly IOrderSubmissionDispatcher _dispatcher;
    private readonly ICrossChannelPortionArbiter _arbiter;

    public YemeksepetiOrderIntakeService(
        NpgsqlDataSource dataSource,
        YemeksepetiWebhookInbox inbox,
        YemeksepetiOrderNormalizer normalizer,
        IOrderRepository orders,
        IOrderSubmissionDispatcher dispatcher,
        ICrossChannelPortionArbiter arbiter)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
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
                default:
                    throw new InvalidOperationException(
                        $"Inbox event '{claimed.InboxId}' mapped to {mapping.Kind}/{mapping.Command}, which this processor never claims.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
        // time) must never become two orders; serialize per provider order id.
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext('online-ordering.yemeksepeti-order:' || $1));", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue(claimed.ExternalOrderId);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.Rejected, null,
                new { rejection = normalized.Rejection!.Value.ToString(), detail = normalized.Detail },
                connection, transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        var items = onlineOrder.Lines
            .Select(line => new OrderItem(
                Guid.NewGuid(), orderId, line.ProductId, line.ProductName, line.Quantity, line.UnitPrice, line.TaxRate,
                skuSnapshot: line.ExternalSku, createdAt: now, updatedAt: now))
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
        if (!hold.IsHeld)
        {
            await YemeksepetiInboxProcessingStore.MarkProcessedAsync(
                claimed.InboxId, InboxProcessingOutcome.Diverged, null,
                new
                {
                    reason = hold.Outcome.ToString(),
                    shortages = hold.Shortages.Select(s => new { s.StockItemId, s.RequiredQuantity, s.AvailableQuantity }),
                    unconfigured = hold.UnconfiguredLines.Select(u => new { u.ProductId, gap = u.Gap.ToString() })
                },
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
            claimed.InboxId, InboxProcessingOutcome.OrderCreated, orderId, new { displayCode = onlineOrder.DisplayCode },
            connection, transaction, cancellationToken).ConfigureAwait(false);
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
