using ALKAROS.IntegrationContracts;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;

namespace ALKAROS.Orders.Integration;

/// <summary>
/// V1-KIT-005: Order's reaction to Kitchen ticket item state changes
/// (only published when a deployment has kitchen live-sync turned on,
/// V1-SET-002). Mirrors the state onto <c>OrderItem.KitchenState</c> and
/// touches nothing outside <c>orders.orders</c> — same shape as
/// <see cref="TableEventOrderConsumer"/>.
/// </summary>
/// <remarks>
/// Delivery is at-least-once and the underlying domain method
/// (<see cref="Order.AdvanceItemKitchenState"/>) is idempotent: a redelivery
/// of an already-applied (or now-stale, e.g. the item was voided in the
/// meantime) state change is a no-op.
/// </remarks>
public sealed class KitchenEventOrderConsumer : IIntegrationEventConsumer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _orders;

    public KitchenEventOrderConsumer(NpgsqlDataSource dataSource, IOrderRepository orders)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
    }

    public bool CanHandle(string eventType) => eventType is KitchenIntegrationEventTypes.KitchenTicketItemStateChanged;

    public async Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (eventType != KitchenIntegrationEventTypes.KitchenTicketItemStateChanged)
            return;

        var e = IntegrationEventSerializer.Deserialize<KitchenTicketItemStateChanged>(payload.Span);
        var kitchenState = MapKitchenState(e.ItemState);
        if (kitchenState is null)
            return; // Unknown/future state name — nothing to mirror yet, not an error.

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var order = await _orders.GetByIdAsync(e.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            // Order no longer exists (should not happen — Orders never
            // hard-deletes) — nothing to mirror onto.
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        Order updated;
        try
        {
            updated = order.AdvanceItemKitchenState(e.OrderItemId, kitchenState.Value);
        }
        catch (ArgumentException)
        {
            // The order no longer has this item id (should not happen) —
            // treat as an unrecoverable mismatch, not a retryable failure.
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!ReferenceEquals(updated, order))
        {
            await _orders.SaveAsync(updated, order.RowVersion, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static KitchenState? MapKitchenState(string kitchenTicketItemState) => kitchenTicketItemState switch
    {
        "Queued" => KitchenState.Sent,
        "Preparing" => KitchenState.Preparing,
        "Ready" => KitchenState.Ready,
        "Served" => KitchenState.Served,
        "Cancelled" => KitchenState.Cancelled,
        _ => null,
    };
}
