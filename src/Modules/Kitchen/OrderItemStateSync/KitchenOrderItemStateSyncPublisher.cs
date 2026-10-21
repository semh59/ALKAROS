namespace ALKAROS.Kitchen.OrderItemStateSync;

using ALKAROS.IntegrationContracts;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Messaging;
using Npgsql;

/// <summary>
/// V1-KIT-005: the Kitchen side of the order-item state mirror. Enqueues a
/// <see cref="KitchenTicketItemStateChanged"/> event whenever a ticket item
/// moves to a state Orders cares about — but only when
/// <paramref name="enabled"/> (the caller reads
/// <c>KitchenLiveSyncSetting.IsEnabledAsync</c>, V1-SET-002; this module does
/// not reference the Settings module directly, keeping its dependencies to
/// BuildingBlocks only).
/// </summary>
/// <remarks>
/// The event is written on the caller's connection and transaction, next to the ticket save, so both commit or
/// neither does (the transactional outbox pattern the other publishers use).
/// </remarks>
public static class KitchenOrderItemStateSyncPublisher
{
    public static async Task PublishAsync(
        Guid orderId,
        KitchenTicketItem item,
        bool enabled,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id cannot be empty.", nameof(orderId));

        if (!enabled)
            return;

        // Queued is published too, not skipped: it is the item's first real
        // kitchen state and is exactly the NotSent -> Sent boundary that
        // decides whether the free pre-send void path is still open
        // (docs/domain/void-complimentary-discount-policy.md). The consumer
        // maps Kitchen's "Queued" name onto Orders' "Sent" name.
        var envelope = new OutboxEnvelope(
            KitchenIntegrationEventTypes.KitchenTicketItemStateChanged,
            "kitchen_ticket_item",
            item.Id,
            IntegrationEventSerializer.Serialize(new KitchenTicketItemStateChanged(
                item.TicketId, orderId, item.OrderItemId, item.Status.ToString(), DateTimeOffset.UtcNow)));

        await OutboxStore.EnqueueAsync(envelope, connection, transaction, cancellationToken);
    }
}
