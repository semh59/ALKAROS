namespace ALKAROS.Kitchen.TicketLifecycle;

using ALKAROS.Orders.OrderAggregate;

/// <summary>
/// Aggregate root representing a station-scoped kitchen ticket (kitchen.kitchen_tickets).
/// Coordinates item-level progress and enforces lifecycle aggregation invariants (PDF:I.16-I.20, PDF:II.5.7A).
/// </summary>
public sealed class KitchenTicket
{
    public KitchenTicket(
        Guid id,
        Guid orderId,
        string ticketNumber,
        string stationId,
        IReadOnlyList<KitchenTicketItem> items,
        KitchenTicketState status = KitchenTicketState.Queued,
        long rowVersion = 1,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        DateTimeOffset? acceptedAt = null,
        DateTimeOffset? readyAt = null,
        DateTimeOffset? cancelledAt = null,
        string? cancellationReason = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Ticket id cannot be empty.", nameof(id));
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(ticketNumber))
            throw new ArgumentException("Ticket number cannot be empty.", nameof(ticketNumber));
        if (string.IsNullOrWhiteSpace(stationId))
            throw new ArgumentException("Station id cannot be empty.", nameof(stationId));
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            throw new ArgumentException("Kitchen ticket must contain at least one item.", nameof(items));

        Id = id;
        OrderId = orderId;
        TicketNumber = ticketNumber;
        StationId = stationId;
        Items = items;
        Status = status;
        RowVersion = rowVersion;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt;
        AcceptedAt = acceptedAt;
        ReadyAt = readyAt;
        CancelledAt = cancelledAt;
        CancellationReason = cancellationReason;
    }

    public Guid Id { get; }
    public Guid OrderId { get; }
    public string TicketNumber { get; }
    public string StationId { get; }
    public IReadOnlyList<KitchenTicketItem> Items { get; }
    public KitchenTicketState Status { get; private set; }
    public long RowVersion { get; internal set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public bool CanTransitionTo(KitchenTicketState targetState)
    {
        if (Status == targetState)
            return false;

        return (Status, targetState) switch
        {
            (KitchenTicketState.Queued, KitchenTicketState.Accepted) => true,
            (KitchenTicketState.Queued, KitchenTicketState.Cancelled) => true,
            (KitchenTicketState.Accepted, KitchenTicketState.Preparing) => true,
            (KitchenTicketState.Accepted, KitchenTicketState.Cancelled) => true,
            (KitchenTicketState.Preparing, KitchenTicketState.Ready) => CanBeMarkedReady(),
            (KitchenTicketState.Preparing, KitchenTicketState.Cancelled) => true,
            (KitchenTicketState.Ready, KitchenTicketState.Cancelled) => true,
            _ => false,
        };
    }

    /// <summary>
    /// Acceptance invariant: Parent ticket Ready only occurs when every non-cancelled
    /// item is Ready or Served (PDF:II.5.7A). If all items are cancelled, ticket cannot be Ready.
    /// </summary>
    public bool CanBeMarkedReady()
    {
        var nonCancelledItems = Items.Where(i => i.Status != KitchenTicketItemState.Cancelled).ToList();
        if (nonCancelledItems.Count == 0)
            return false;

        return nonCancelledItems.All(i =>
            i.Status == KitchenTicketItemState.Ready || i.Status == KitchenTicketItemState.Served);
    }

    public KitchenTicket TransitionTo(
        KitchenTicketState newState,
        string? reason = null,
        DateTimeOffset? timestamp = null)
    {
        if (!CanTransitionTo(newState))
        {
            throw new InvalidKitchenTransitionException(
                $"Kitchen ticket '{Id}' cannot transition from {Status} to {newState}. (CanBeMarkedReady={CanBeMarkedReady()})");
        }

        var at = timestamp ?? DateTimeOffset.UtcNow;
        var updatedItems = Items;

        if (newState == KitchenTicketState.Cancelled)
        {
            // Cascade cancellation to non-terminal items
            updatedItems = Items.Select(item =>
            {
                if (item.Status != KitchenTicketItemState.Cancelled && item.Status != KitchenTicketItemState.Served)
                {
                    return item.TransitionTo(KitchenTicketItemState.Cancelled, reason ?? "Parent ticket cancelled", at);
                }
                return item;
            }).ToList();
        }

        return new KitchenTicket(
            Id,
            OrderId,
            TicketNumber,
            StationId,
            updatedItems,
            status: newState,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: at,
            acceptedAt: newState == KitchenTicketState.Accepted ? at : AcceptedAt,
            readyAt: newState == KitchenTicketState.Ready ? at : ReadyAt,
            cancelledAt: newState == KitchenTicketState.Cancelled ? at : CancelledAt,
            cancellationReason: newState == KitchenTicketState.Cancelled ? reason : CancellationReason);
    }

    public KitchenTicket UpdateItemStatus(
        Guid itemId,
        KitchenTicketItemState newItemStatus,
        string? reason = null,
        DateTimeOffset? timestamp = null)
    {
        var itemIndex = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == itemId)
            {
                itemIndex = i;
                break;
            }
        }

        if (itemIndex < 0)
        {
            throw new ArgumentException($"Kitchen ticket item '{itemId}' does not exist on ticket '{Id}'.", nameof(itemId));
        }

        var targetItem = Items[itemIndex];
        var updatedItem = targetItem.TransitionTo(newItemStatus, reason, timestamp);

        var newItems = new List<KitchenTicketItem>(Items);
        newItems[itemIndex] = updatedItem;

        var at = timestamp ?? DateTimeOffset.UtcNow;
        var newTicketStatus = Status;

        // Auto-promote: If ticket was Accepted and an item started Preparing -> ticket becomes Preparing
        if (Status == KitchenTicketState.Accepted && newItemStatus == KitchenTicketItemState.Preparing)
        {
            newTicketStatus = KitchenTicketState.Preparing;
        }

        // Auto-cancel: If every item on the ticket is now Cancelled -> ticket becomes Cancelled
        if (newItems.All(i => i.Status == KitchenTicketItemState.Cancelled))
        {
            newTicketStatus = KitchenTicketState.Cancelled;
        }

        return new KitchenTicket(
            Id,
            OrderId,
            TicketNumber,
            StationId,
            newItems,
            status: newTicketStatus,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: at,
            acceptedAt: AcceptedAt,
            readyAt: ReadyAt,
            cancelledAt: newTicketStatus == KitchenTicketState.Cancelled ? at : CancelledAt,
            cancellationReason: newTicketStatus == KitchenTicketState.Cancelled ? (reason ?? "All items cancelled") : CancellationReason);
    }

    public static KitchenTicket CreateFromOrder(
        Order order,
        string stationId,
        Func<OrderItem, bool>? itemFilter = null,
        string? ticketNumber = null,
        DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (string.IsNullOrWhiteSpace(stationId))
            throw new ArgumentException("Station id cannot be empty.", nameof(stationId));

        var filter = itemFilter ?? (_ => true);
        var activeOrderItems = order.Items.Where(i => i.IsActive && filter(i)).ToList();

        if (activeOrderItems.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot create kitchen ticket for order '{order.Id}' at station '{stationId}': no matching active order items.");
        }

        var ticketId = Guid.NewGuid();
        var num = ticketNumber ?? $"KT-{order.OrderNumber}-{stationId}";
        var at = timestamp ?? DateTimeOffset.UtcNow;

        var ticketItems = activeOrderItems.Select(orderItem =>
        {
            var modSummary = orderItem.Modifiers.Count > 0
                ? string.Join(", ", orderItem.Modifiers.Select(m => m.ModifierNameSnapshot))
                : null;

            return new KitchenTicketItem(
                Guid.NewGuid(),
                ticketId,
                orderItem.Id,
                orderItem.ProductId,
                orderItem.ProductNameSnapshot,
                orderItem.Quantity,
                modifiersSummary: modSummary,
                notes: orderItem.Notes,
                status: KitchenTicketItemState.Queued,
                rowVersion: 1,
                createdAt: at);
        }).ToList();

        return new KitchenTicket(
            ticketId,
            order.Id,
            num,
            stationId,
            ticketItems,
            status: KitchenTicketState.Queued,
            rowVersion: 1,
            createdAt: at);
    }
}
