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
        DateTimeOffset? updatedAt = null)
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
            at);
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
    /// Submits a Draft order: every remaining Draft item activates and the
    /// order moves to Submitted. A Draft order with no active-capable items
    /// cannot be submitted (empty order guard).
    /// </summary>
    public Order Submit(string? reason = null, Guid? changedBy = null, DateTimeOffset? changedAt = null)
    {
        if (Status is not OrderState.Draft)
            throw new InvalidOperationException($"Order {Id} cannot be submitted from {Status}.");

        var items = new List<OrderItem>(_items.Count);
        foreach (var item in _items)
        {
            items.Add(item.Status is OrderItemState.Draft ? item.Activate() : item);
        }

        if (items.All(i => i.Status is not OrderItemState.Active))
            throw new InvalidOperationException($"Order {Id} has no items to submit.");

        return RebuildWith(items: items).TransitionTo(OrderState.Submitted, reason, changedBy, changedAt);
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

        return RebuildWith(items: items);
    }

    /// <summary>
    /// Returns a copy with the row version advanced; used by repositories
    /// after a successful optimistic concurrency update.
    /// </summary>
    public Order WithRowVersion(long rowVersion)
        => RebuildWith(rowVersion: rowVersion);

    private Order RebuildWith(
        IReadOnlyList<OrderItem>? items = null,
        long? rowVersion = null)
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
            _history,
            rowVersion ?? RowVersion,
            CreatedAt,
            UpdatedAt);
}