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
        string? cancellationReason = null,
        bool isAgeRestricted = false,
        int? courseNumber = null,
        bool isHeld = false)
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
        IsAgeRestricted = isAgeRestricted;
        CourseNumber = courseNumber;
        IsHeld = isHeld;
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
    /// <summary>
    /// V1-RMD-137: a point-in-time snapshot of catalog.products.is_age_restricted
    /// taken when the ticket was created (same rationale as ProductNameSnapshot) —
    /// prompts whoever serves the item to check ID before handing it over. Does
    /// not gate preparation itself; see NfcOrderingStore's own documentation for
    /// why the kitchen ticket dispatches immediately regardless.
    /// </summary>
    public bool IsAgeRestricted { get; }

    /// <summary>V1-WTR-025: which course this line belongs to, or null when the order has no course structure.</summary>
    public int? CourseNumber { get; }

    /// <summary>
    /// V1-WTR-025: a point-in-time snapshot — true when this line was
    /// printed as part of the whole-plan ticket while its course was still
    /// Held (on the ticket for prep visibility, not yet called in). A later
    /// course's own "fire" ticket (see <see cref="KitchenTicket.CreateFromOrder"/>
    /// callers) always prints its items with this false, since by then they
    /// have already been promoted to Sent.
    /// </summary>
    public bool IsHeld { get; }

    // V1-KIT-009: a short, time-boxed recovery for a misclick — the general
    // KDS industry pattern (an undo option next to a just-completed item,
    // gone once a few seconds pass). Deliberately covers only the forward
    // progression chain (Queued<->Preparing<->Ready<->Served), not
    // Cancelled: reopening a cancelled item touches void/stock-restore
    // concerns this narrow recovery path has no business deciding.
    public static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(10);

    private static readonly Dictionary<KitchenTicketItemState, KitchenTicketItemState> UndoTargets =
        new Dictionary<KitchenTicketItemState, KitchenTicketItemState>
        {
            [KitchenTicketItemState.Preparing] = KitchenTicketItemState.Queued,
            [KitchenTicketItemState.Ready] = KitchenTicketItemState.Preparing,
            [KitchenTicketItemState.Served] = KitchenTicketItemState.Ready,
        };

    /// <summary>
    /// Whether this item's most recent transition can still be undone at
    /// <paramref name="now"/> — it left a state <see cref="UndoTargets"/>
    /// knows how to reverse, and its own <see cref="UpdatedAt"/> is within
    /// <see cref="UndoWindow"/>. A missing <see cref="UpdatedAt"/> (never
    /// transitioned) has nothing to undo.
    /// </summary>
    public bool CanUndo(DateTimeOffset now) =>
        UndoTargets.ContainsKey(Status) && UpdatedAt is { } updatedAt && now - updatedAt <= UndoWindow;

    /// <summary>
    /// Reverses this item's most recent transition one stage. Clears
    /// whichever milestone timestamp belonged to the state being left
    /// (<see cref="ReadyAt"/> leaving Ready, <see cref="ServedAt"/> leaving
    /// Served) since it is no longer accurate; an earlier milestone (e.g.
    /// <see cref="ReadyAt"/> when undoing Served back to Ready) is left
    /// alone — it really did happen.
    /// </summary>
    public KitchenTicketItem Undo(DateTimeOffset? timestamp = null)
    {
        var now = timestamp ?? DateTimeOffset.UtcNow;
        if (!CanUndo(now))
        {
            throw new InvalidKitchenTransitionException(
                $"Kitchen ticket item '{Id}' cannot be undone from {Status} (window expired or nothing to undo).");
        }

        var previous = UndoTargets[Status];
        return new KitchenTicketItem(
            Id,
            TicketId,
            OrderItemId,
            ProductId,
            ProductNameSnapshot,
            Quantity,
            ModifiersSummary,
            Notes,
            status: previous,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: now,
            readyAt: Status == KitchenTicketItemState.Ready ? null : ReadyAt,
            servedAt: Status == KitchenTicketItemState.Served ? null : ServedAt,
            cancelledAt: CancelledAt,
            cancellationReason: CancellationReason,
            isAgeRestricted: IsAgeRestricted,
            courseNumber: CourseNumber,
            isHeld: IsHeld);
    }

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
            cancellationReason: newState == KitchenTicketItemState.Cancelled ? reason : CancellationReason,
            isAgeRestricted: IsAgeRestricted,
            courseNumber: CourseNumber,
            isHeld: IsHeld);
    }
}
