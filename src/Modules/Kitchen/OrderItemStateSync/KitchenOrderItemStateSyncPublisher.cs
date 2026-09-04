namespace ALKAROS.Kitchen.OrderItemStateSync;

using ALKAROS.IntegrationContracts;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Messaging;

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
/// Design note: <see cref="IKitchenTicketRepository.SaveAsync(KitchenTicket, long, CancellationToken)"/>
/// owns its own connection and transaction internally (no connection/transaction
/// overload exists, unlike <c>IOrderRepository</c>'s table-reparent methods), so
/// this enqueues on its own short transaction immediately after the ticket
/// save commits rather than inside the same one. That is a narrower guarantee
/// than the fully atomic transactional outbox pattern Table Management uses
/// (a crash in the narrow window between the two commits drops the event) —
/// accepted here because kitchen status is operational, not financial, and
/// every consumer of this event is already required to tolerate at-least-once
/// / out-of-order delivery; a dropped ready-notification is recoverable by the
/// next state transition, not a correctness incident.
/// </remarks>
public static class KitchenOrderItemStateSyncPublisher
{
    public static async Task PublishAsync(
        OutboxStore outbox,
        Guid orderId,
        KitchenTicketItem item,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outbox);
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

        await outbox.EnqueueAsync(envelope, cancellationToken);
    }
}
