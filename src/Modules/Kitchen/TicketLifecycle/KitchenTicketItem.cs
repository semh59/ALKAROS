namespace ALKAROS.Kitchen.TicketLifecycle;

/// <summary>
/// Represents a single line item within a kitchen ticket (kitchen.kitchen_ticket_items).
/// Maintains independent item lifecycle state and timestamps (PDF:I.16-I.20, PDF:II.5.8).
/// </summary>
public sealed class KitchenTicketItem
{
    public KitchenTicketItem(
        Guid id,
        Guid ticketId,
        Guid orderItemId,
        Guid productId,
        string productNameSnapshot,
        decimal quantity,
        string? modifiersSummary = null,
        string? notes = null,
        KitchenTicketItemState status = KitchenTicketItemState.Queued,
        long rowVersion = 1,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        DateTimeOffset? readyAt = null,
        DateTimeOffset? servedAt = null,
        DateTimeOffset? cancelledAt = null,
        string? cancellationReason = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Item id cannot be empty.", nameof(id));
        if (ticketId == Guid.Empty)
            throw new ArgumentException("Ticket id cannot be empty.", nameof(ticketId));
        if (orderItemId == Guid.Empty)
            throw new ArgumentException("Order item id cannot be empty.", nameof(orderItemId));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id cannot be empty.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new ArgumentException("Product name snapshot cannot be empty.", nameof(productNameSnapshot));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        Id = id;
        TicketId = ticketId;
        OrderItemId = orderItemId;
        ProductId = productId;
        ProductNameSnapshot = productNameSnapshot;
        Quantity = quantity;
        ModifiersSummary = modifiersSummary;
        Notes = notes;
        Status = status;
        RowVersion = rowVersion;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt;
        ReadyAt = readyAt;
        ServedAt = servedAt;
        CancelledAt = cancelledAt;
        CancellationReason = cancellationReason;
    }

    public Guid Id { get; }
    public Guid TicketId { get; }
    public Guid OrderItemId { get; }
    public Guid ProductId { get; }
    public string ProductNameSnapshot { get; }
    public decimal Quantity { get; }
    public string? ModifiersSummary { get; }
    public string? Notes { get; }
    public KitchenTicketItemState Status { get; private set; }
    public long RowVersion { get; internal set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? ServedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public bool CanTransitionTo(KitchenTicketItemState targetState)
    {
        if (Status == targetState)
            return false;

        return (Status, targetState) switch
        {
            (KitchenTicketItemState.Queued, KitchenTicketItemState.Preparing) => true,
            (KitchenTicketItemState.Queued, KitchenTicketItemState.Cancelled) => true,
            (KitchenTicketItemState.Preparing, KitchenTicketItemState.Ready) => true,
            (KitchenTicketItemState.Preparing, KitchenTicketItemState.Cancelled) => true,
            (KitchenTicketItemState.Ready, KitchenTicketItemState.Served) => true,
            (KitchenTicketItemState.Ready, KitchenTicketItemState.Cancelled) => true,
            _ => false,
        };
    }

    public KitchenTicketItem TransitionTo(
        KitchenTicketItemState newState,
        string? reason = null,
        DateTimeOffset? timestamp = null)
    {
        if (!CanTransitionTo(newState))
        {
            throw new InvalidKitchenTransitionException(
                $"Kitchen ticket item '{Id}' cannot transition from {Status} to {newState}.");
        }

        var at = timestamp ?? DateTimeOffset.UtcNow;

        return new KitchenTicketItem(
            Id,
            TicketId,
            OrderItemId,
            ProductId,
            ProductNameSnapshot,
            Quantity,
            ModifiersSummary,
            Notes,
            status: newState,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: at,
            readyAt: newState == KitchenTicketItemState.Ready ? at : ReadyAt,
            servedAt: newState == KitchenTicketItemState.Served ? at : ServedAt,
            cancelledAt: newState == KitchenTicketItemState.Cancelled ? at : CancelledAt,
            cancellationReason: newState == KitchenTicketItemState.Cancelled ? reason : CancellationReason);
    }
}
