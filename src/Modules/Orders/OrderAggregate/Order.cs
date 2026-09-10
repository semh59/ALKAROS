namespace ALKAROS.Orders.OrderAggregate;

/// <summary>
/// The channel-independent order aggregate root (orders.orders, PDF:III.6.1).
/// Enforces the canonical Order transition matrix
/// (docs/domain/lifecycle-transition-contracts.md Order row) and the
/// V0-DOM-006 void policy via <see cref="OrderItem.Cancel"/>. The order and
/// its items/history form one transaction boundary at persistence.
///
/// Transition matrix (V0-DOM-001):
/// Draft→Submitted; Submitted→PendingConfirmation; PendingConfirmation→
/// Accepted|Rejected; Accepted→Preparing; Preparing→Ready; Ready→Served;
/// Served→Completed; {Draft,Submitted,PendingConfirmation,Accepted,Preparing,
/// Ready}→Cancelled. Forbidden: Served→Accepted, Completed→Preparing,
/// Draft→Accepted (no skip), Cancelled→Accepted (terminal reopen).
/// </summary>
public sealed class Order
{
    private readonly List<OrderItem> _items;
    private readonly List<OrderStatusHistoryEntry> _history;

    public Order(
        Guid id,
        OrderSource source,
        string orderNumber,
        IReadOnlyList<OrderItem> items,
        Guid? tableId = null,
        Guid? customerId = null,
        Guid? sourceReferenceId = null,
        string? sourceExternalId = null,
        string? notes = null,
        OrderState status = OrderState.Draft,
        ConfirmationStatus confirmationStatus = ConfirmationStatus.NotRequired,
        string currencyCode = "TRY",
        DateTimeOffset? submittedAt = null,
        DateTimeOffset? acceptedAt = null,
        DateTimeOffset? closedAt = null,
        DateTimeOffset? cancelledAt = null,
        IReadOnlyList<OrderStatusHistoryEntry>? history = null,
        long rowVersion = 1,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        Guid? servingUserId = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Order id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new ArgumentException("Order number cannot be empty.", nameof(orderNumber));
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty.", nameof(tableId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));
        if (sourceReferenceId == Guid.Empty)
            throw new ArgumentException("Source reference id cannot be empty.", nameof(sourceReferenceId));
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code cannot be empty.", nameof(currencyCode));

        Id = id;
        Source = source;
        SourceReferenceId = sourceReferenceId;
        SourceExternalId = sourceExternalId;
        TableId = tableId;
        CustomerId = customerId;
        OrderNumber = orderNumber;
        Notes = notes;
        Status = status;
        ConfirmationStatus = confirmationStatus;
        CurrencyCode = currencyCode;
        SubmittedAt = submittedAt;
        AcceptedAt = acceptedAt;
        ClosedAt = closedAt;
        CancelledAt = cancelledAt;
        RowVersion = rowVersion;
        ServingUserId = servingUserId;

        _items = items is null
            ? new List<OrderItem>()
            : items.Select(i => i.ForOrder(Id)).ToList();
        _history = history is null ? new List<OrderStatusHistoryEntry>() : history.ToList();
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt ?? CreatedAt;
    }

    public Guid Id { get; }

    public OrderSource Source { get; }

    public Guid? SourceReferenceId { get; }

    public string? SourceExternalId { get; }

    public Guid? TableId { get; }

    public Guid? CustomerId { get; }

    public string OrderNumber { get; }

    public OrderState Status { get; }

    public ConfirmationStatus ConfirmationStatus { get; }

    public string? Notes { get; }

    public string CurrencyCode { get; }

    public DateTimeOffset? SubmittedAt { get; }

    public DateTimeOffset? AcceptedAt { get; }

    public DateTimeOffset? ClosedAt { get; }

    public DateTimeOffset? CancelledAt { get; }

    public long RowVersion { get; }

    /// <summary>
    /// The user this order is attributed to for the authorization model's
    /// own-check rule (docs/domain/authorization-model.md §3, resolved
    /// decision #1: a waiter may raise a void/comp grant only on a check
    /// they serve). Set once at creation (V1-RMD-111) and changed only by
    /// <see cref="ReassignServer"/> — an explicit hand-off, never an
    /// implicit side effect of any other transition.
    /// </summary>
    public Guid? ServingUserId { get; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderStatusHistoryEntry> History => _history;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public decimal Subtotal
    {
        get
        {
            var subtotal = 0m;
            foreach (var item in _items.Where(i => i.IsActive))
                subtotal += item.LineSubtotalValue;
            return OrderMath.RoundCurrency(subtotal);
        }
    }

    public decimal DiscountTotal
    {
        get
        {
            var total = 0m;
            foreach (var item in _items.Where(i => i.IsActive))
                total += item.DiscountAmount;
            return OrderMath.RoundCurrency(total);
        }
    }

    public decimal TaxTotal
    {
        get
        {
            var tax = 0m;
            foreach (var item in _items.Where(i => i.IsActive))
                tax += item.TaxAmount;
            return OrderMath.RoundCurrency(tax);
        }
    }

    public decimal Total
    {
        get
        {
            var total = 0m;
            foreach (var item in _items.Where(i => i.IsActive))
                total += item.GrossAmount;
            return OrderMath.RoundCurrency(total);
        }
    }

    /// <summary>
    /// Returns whether <paramref name="target"/> can immediately follow the
    /// current state according to the canonical Order transition matrix.
    /// </summary>
    public bool CanTransitionTo(OrderState target) => target switch
    {
        OrderState.Submitted => Status is OrderState.Draft,
        OrderState.PendingConfirmation => Status is OrderState.Submitted,
        OrderState.Accepted => Status is OrderState.PendingConfirmation,
        OrderState.Rejected => Status is OrderState.PendingConfirmation,
        OrderState.Preparing => Status is OrderState.Accepted,
        OrderState.Ready => Status is OrderState.Preparing,
        OrderState.Served => Status is OrderState.Ready,
        OrderState.Completed => Status is OrderState.Served,
        OrderState.Cancelled => Status is OrderState.Draft or OrderState.Submitted or OrderState.PendingConfirmation
            or OrderState.Accepted or OrderState.Preparing or OrderState.Ready,
        _ => false,
    };

    /// <summary>
    /// Returns a new instance with the given state when the transition is
    /// allowed; otherwise throws. Records the transition in the order's
    /// status history and stamps the corresponding audit timestamp. The
    /// confirmation status preview is derived only when the target is
    /// PendingConfirmation/Accepted/Rejected (scope-owned by V1-ORD-001).
    /// </summary>
    public Order TransitionTo(
        OrderState target,
        string? reason = null,
        Guid? changedBy = null,
        DateTimeOffset? changedAt = null)
    {
        if (!CanTransitionTo(target))
            throw new InvalidOperationException(
                $"Order {Id} cannot transition from {Status} to {target}.");

        var at = changedAt ?? DateTimeOffset.UtcNow;
        var confirmation = target switch
        {
            OrderState.PendingConfirmation => ConfirmationStatus.Pending,
            OrderState.Accepted => ConfirmationStatus.Accepted,
            OrderState.Rejected => ConfirmationStatus.Rejected,
            _ => ConfirmationStatus,
        };

        var result = new Order(
            Id,
            Source,
            OrderNumber,
            _items,
            TableId,
            CustomerId,
            SourceReferenceId,
            SourceExternalId,
            Notes,
            target,
            confirmation,
            CurrencyCode,
            target == OrderState.Submitted ? at : SubmittedAt,
            target == OrderState.Accepted ? at : AcceptedAt,
            target == OrderState.Completed ? at : ClosedAt,
            target == OrderState.Cancelled ? at : CancelledAt,
            _history.Append(new OrderStatusHistoryEntry(Guid.NewGuid(), Id, Status, target, reason, changedBy, at)).ToList(),
            RowVersion,
            CreatedAt,
            at,
            ServingUserId);
        return result;
    }

    /// <summary>
    /// Adds a Draft item to the order. The order must still be Draft;
    /// items cannot be appended after submission. Returns a new instance.
    /// </summary>
    public Order AddItem(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status is not OrderState.Draft)
            throw new InvalidOperationException($"Order {Id} cannot accept items in state {Status}.");

        var items = _items.Append(item).ToList();
        return RebuildWith(items: items);
    }

    /// <summary>
    /// V1-ORD-006: whether this order is still an open check that can take
    /// another round. A party orders starters, then mains, then dessert on
    /// one check; only <see cref="AddItem"/>'s Draft-only rule stopped that
    /// from being expressible, which is why every round used to become its
    /// own order and only the last one was ever billed.
    /// </summary>
    public bool IsOpenCheck => Status is OrderState.Draft or OrderState.Submitted;

    /// <summary>
    /// V1-ORD-006: appends a round of not-yet-fired items to an open check.
    /// The items arrive Draft and stay Draft until <see cref="FireRound"/>
    /// activates them, so an order that is already Submitted keeps its state
    /// and its earlier rounds untouched.
    /// </summary>
    public Order AddRound(IReadOnlyList<OrderItem> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);
        if (!IsOpenCheck)
            throw new InvalidOperationException($"Order {Id} cannot accept items in state {Status}.");
        if (newItems.Any(item => item.Status is not OrderItemState.Draft))
            throw new ArgumentException("A new round may only carry Draft items.", nameof(newItems));

        return RebuildWith(items: _items.Concat(newItems).ToList());
    }

    /// <summary>
    /// V1-ORD-006: activates the Draft items — and only those — returning the
    /// order together with the round that was actually fired. The caller
    /// hands that list to <see cref="IOrderSubmissionDispatcher"/> so the
    /// kitchen and the stock ledger see the new round alone; deriving it from
    /// the order's active items would fire every earlier round again.
    ///
    /// A Draft order also moves to Submitted, which is what
    /// <see cref="Submit"/> always did. An order that is already Submitted
    /// stays Submitted: the check was opened by the first round and the
    /// second one does not reopen it.
    /// </summary>
    public (Order Order, IReadOnlyList<OrderItem> FiredItems) FireRound(
        string? reason = null, Guid? changedBy = null, DateTimeOffset? changedAt = null)
    {
        if (!IsOpenCheck)
            throw new InvalidOperationException($"Order {Id} cannot be submitted from {Status}.");

        var items = new List<OrderItem>(_items.Count);
        var fired = new List<OrderItem>();
        foreach (var item in _items)
        {
            if (item.Status is OrderItemState.Draft)
            {
                // V1-RMD-154: firing IS sending to the kitchen — the kitchen
                // ticket for this round is written in the same transaction, so
                // if the dispatch fails this state rolls back with it.
                //
                // Nothing used to set KitchenState here, and the only other
                // thing that advances it is the KDS live-sync path, which is
                // off by default (kitchen.live_sync_enabled). So a plated,
                // eaten dish still read NotSent, VoidItemAsync's "already sent
                // to the kitchen" wall never fired, and a waiter could void it
                // under orders.create — skipping the bills.void approval,
                // leaving the kitchen ticket standing, and never giving the
                // consumed stock back.
                var activated = item.Activate().AdvanceKitchenState(KitchenState.Sent);
                items.Add(activated);
                fired.Add(activated);
            }
            else
            {
                items.Add(item);
            }
        }

        if (fired.Count == 0)
            throw new InvalidOperationException($"Order {Id} has no items to submit.");

        var updated = RebuildWith(items: items);
        if (Status is OrderState.Draft)
            updated = updated.TransitionTo(OrderState.Submitted, reason, changedBy, changedAt);

        return (updated, fired);
    }

    /// <summary>
    /// Submits a Draft order: every remaining Draft item activates and the
    /// order moves to Submitted. A Draft order with no active-capable items
    /// cannot be submitted (empty order guard).
    /// </summary>
    public Order Submit(string? reason = null, Guid? changedBy = null, DateTimeOffset? changedAt = null)
    {
        if (Status is not OrderState.Draft)
            throw new InvalidOperationException($"Order {Id} cannot be submitted from {Status}.");

        return FireRound(reason, changedBy, changedAt).Order;
    }

    /// <summary>
    /// Cancels an Active item following the void policy
    /// (V0-DOM-006): the transition itself is validated by the item; the
    /// order-level precondition is that the order is cancellable
    /// (non-terminal and not already served). Returns a new instance.
    /// </summary>
    public Order CancelItem(Guid orderItemId, string? reason = null, Guid? changedBy = null, DateTimeOffset? changedAt = null)
    {
        if (!CanTransitionTo(OrderState.Cancelled))
            throw new InvalidOperationException(
                $"Order {Id} cannot void items in terminal/prepared state {Status}.");

        var items = new List<OrderItem>(_items.Count);
        var found = false;
        foreach (var item in _items)
        {
            if (item.Id == orderItemId)
            {
                items.Add(item.Cancel());
                found = true;
            }
            else
            {
                items.Add(item);
            }
        }

        if (!found)
            throw new ArgumentException($"Order {Id} has no item {orderItemId}.", nameof(orderItemId));

        // Found by an independent audit (2026-09-05): reason/changedBy/
        // changedAt were accepted but silently dropped — an item-level void
        // never appended an OrderStatusHistoryEntry, unlike every
        // order-level TransitionTo. The order's own Status does not change
        // for an item void, so OldStatus and NewStatus are both the
        // current Status; this entry exists purely to record the reason
        // and actor (V0-DOM-006 void audit requirement).
        var at = changedAt ?? DateTimeOffset.UtcNow;
        var history = _history
            .Append(new OrderStatusHistoryEntry(Guid.NewGuid(), Id, Status, Status, reason, changedBy, at))
            .ToList();

        return RebuildWith(items: items, history: history);
    }

    /// <summary>
    /// V1-KIT-005: mirrors a kitchen ticket item's state onto its order
    /// item. A no-op — returns this same instance — when the item is no
    /// longer Active (already voided/comped before the event arrived) or
    /// already at that state, so an at-least-once redelivery of the same
    /// event never produces a spurious write.
    /// </summary>
    public Order AdvanceItemKitchenState(Guid orderItemId, KitchenState kitchenState)
    {
        var items = new List<OrderItem>(_items.Count);
        var changed = false;
        var found = false;
        foreach (var item in _items)
        {
            if (item.Id == orderItemId)
            {
                found = true;
                if (item.Status == OrderItemState.Active && item.KitchenState != kitchenState)
                {
                    items.Add(item.AdvanceKitchenState(kitchenState));
                    changed = true;
                    continue;
                }
            }

            items.Add(item);
        }

        if (!found)
            throw new ArgumentException($"Order {Id} has no item {orderItemId}.", nameof(orderItemId));

        return changed ? RebuildWith(items: items) : this;
    }

    /// <summary>
    /// Changes a Draft item's quantity in place (V1-RMD-120: the line-quantity
    /// stepper a channel like Host/DualScreen's quick-sale cart needs before
    /// submission). The order itself must still be Draft; the target item
    /// must be Draft too — an Active item's quantity is frozen, only
    /// <see cref="CancelItem"/> removes its contribution. Returns a new
    /// instance.
    /// </summary>
    public Order ChangeItemQuantity(Guid orderItemId, decimal quantity)
    {
        if (Status is not OrderState.Draft)
            throw new InvalidOperationException($"Order {Id} cannot change an item quantity in state {Status}.");

        var items = new List<OrderItem>(_items.Count);
        var found = false;
        foreach (var item in _items)
        {
            if (item.Id == orderItemId)
            {
                items.Add(item.ChangeQuantity(quantity));
                found = true;
            }
            else
            {
                items.Add(item);
            }
        }

        if (!found)
            throw new ArgumentException($"Order {Id} has no item {orderItemId}.", nameof(orderItemId));

        return RebuildWith(items: items);
    }

    /// <summary>
    /// Removes a Draft item from the order entirely (V1-RMD-120: the
    /// line-delete action a pre-submission cart needs — distinct from
    /// <see cref="CancelItem"/>, which voids an already-Active line and keeps
    /// it for audit). The order must still be Draft; the target item must be
    /// Draft too. Returns a new instance.
    /// </summary>
    public Order RemoveItem(Guid orderItemId)
    {
        if (Status is not OrderState.Draft)
            throw new InvalidOperationException($"Order {Id} cannot remove an item in state {Status}.");

        var item = _items.FirstOrDefault(i => i.Id == orderItemId)
            ?? throw new ArgumentException($"Order {Id} has no item {orderItemId}.", nameof(orderItemId));
        if (item.Status is not OrderItemState.Draft)
            throw new InvalidOperationException($"Order item {orderItemId} cannot be removed from state {item.Status}.");

        return RebuildWith(items: _items.Where(i => i.Id != orderItemId).ToList());
    }

    /// <summary>
    /// Returns a copy with the row version advanced; used by repositories
    /// after a successful optimistic concurrency update.
    /// </summary>
    public Order WithRowVersion(long rowVersion)
        => RebuildWith(rowVersion: rowVersion);

    /// <summary>
    /// Hands the order off to a different serving user (V1-RMD-111) — the
    /// only way <see cref="ServingUserId"/> ever changes after creation.
    /// The caller (Host/Experience layer) is responsible for authorizing
    /// the hand-off (self vs. any, orders.transfer-server[-any]) before
    /// calling this.
    /// </summary>
    public Order ReassignServer(Guid toUserId)
    {
        if (toUserId == Guid.Empty)
            throw new ArgumentException("Serving user id cannot be empty.", nameof(toUserId));

        return RebuildWith(servingUserId: toUserId);
    }

    private Order RebuildWith(
        IReadOnlyList<OrderItem>? items = null,
        long? rowVersion = null,
        IReadOnlyList<OrderStatusHistoryEntry>? history = null,
        Guid? servingUserId = null)
        => new(
            Id,
            Source,
            OrderNumber,
            items ?? _items,
            TableId,
            CustomerId,
            SourceReferenceId,
            SourceExternalId,
            Notes,
            Status,
            ConfirmationStatus,
            CurrencyCode,
            SubmittedAt,
            AcceptedAt,
            ClosedAt,
            CancelledAt,
            history ?? _history,
            rowVersion ?? RowVersion,
            CreatedAt,
            UpdatedAt,
            servingUserId ?? ServingUserId);
}