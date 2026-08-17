using ALKAROS.Orders.OrderAggregate;

namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// The Bill aggregate root (billing.bills, PDF:III.7.1).
/// Enforces the canonical Bill transition matrix (PDF:I.46A / PDF:II.5.2 / V0-DOM-001)
/// and manages bill item membership across orders with zero double-billing (V0-DOM-002).
/// </summary>
public sealed class Bill
{
    private readonly List<BillItem> _items;

    public Bill(
        Guid id,
        string billNumber,
        IReadOnlyList<BillItem>? items = null,
        Guid? tableId = null,
        Guid? orderId = null,
        Guid? customerAccountId = null,
        BillState status = BillState.Open,
        string currencyCode = "TRY",
        decimal allocatedAmount = 0m,
        decimal paidAmount = 0m,
        decimal changeAmount = 0m,
        DateTimeOffset? openedAt = null,
        DateTimeOffset? closedAt = null,
        DateTimeOffset? cancelledAt = null,
        DateTimeOffset? reopenedAt = null,
        long rowVersion = 1,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(billNumber))
            throw new ArgumentException("Bill number cannot be empty.", nameof(billNumber));
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty GUID.", nameof(tableId));
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id cannot be empty GUID.", nameof(orderId));
        if (customerAccountId == Guid.Empty)
            throw new ArgumentException("Customer account id cannot be empty GUID.", nameof(customerAccountId));
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code cannot be empty.", nameof(currencyCode));
        if (allocatedAmount < 0)
            throw new ArgumentException("Allocated amount cannot be negative.", nameof(allocatedAmount));
        if (paidAmount < 0)
            throw new ArgumentException("Paid amount cannot be negative.", nameof(paidAmount));
        if (changeAmount < 0)
            throw new ArgumentException("Change amount cannot be negative.", nameof(changeAmount));

        Id = id;
        BillNumber = billNumber;
        TableId = tableId;
        OrderId = orderId;
        CustomerAccountId = customerAccountId;
        Status = status;
        CurrencyCode = currencyCode;
        AllocatedAmount = allocatedAmount;
        PaidAmount = paidAmount;
        ChangeAmount = changeAmount;
        OpenedAt = openedAt ?? DateTimeOffset.UtcNow;
        ClosedAt = closedAt;
        CancelledAt = cancelledAt;
        ReopenedAt = reopenedAt;
        RowVersion = rowVersion;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt ?? CreatedAt;

        _items = items is null
            ? new List<BillItem>()
            : items.Select(i => i.BillId == id ? i : i.ForBill(id)).ToList();

        // Enforce uniqueness of order_item_id within the aggregate
        var duplicateOrderItemId = _items
            .GroupBy(i => i.OrderItemId)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateOrderItemId is not null)
        {
            throw new InvalidOperationException(
                $"Order item {duplicateOrderItemId.Key} cannot appear more than once in Bill {Id}.");
        }
    }

    public Guid Id { get; }

    public string BillNumber { get; }

    public Guid? TableId { get; }

    /// <summary>
    /// Origin order ID (nullable metadata, V0-DOM-002). Not a cardinality lock.
    /// </summary>
    public Guid? OrderId { get; }

    public Guid? CustomerAccountId { get; }

    public BillState Status { get; }

    public string CurrencyCode { get; }

    public decimal AllocatedAmount { get; }

    public decimal PaidAmount { get; }

    public decimal ChangeAmount { get; }

    public DateTimeOffset OpenedAt { get; }

    public DateTimeOffset? ClosedAt { get; }

    public DateTimeOffset? CancelledAt { get; }

    public DateTimeOffset? ReopenedAt { get; }

    public long RowVersion { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public IReadOnlyList<BillItem> Items => _items;

    public decimal Subtotal
    {
        get
        {
            var sum = 0m;
            foreach (var item in _items)
                sum += item.LineSubtotal;
            return BillMath.RoundCurrency(sum);
        }
    }

    public decimal DiscountTotal
    {
        get
        {
            var sum = 0m;
            foreach (var item in _items)
                sum += item.DiscountAmount;
            return BillMath.RoundCurrency(sum);
        }
    }

    public decimal TaxTotal
    {
        get
        {
            var sum = 0m;
            foreach (var item in _items)
                sum += item.TaxAmount;
            return BillMath.RoundCurrency(sum);
        }
    }

    public decimal PayableAmount
    {
        get
        {
            var sum = 0m;
            foreach (var item in _items)
                sum += item.GrossAmount;
            return BillMath.RoundCurrency(sum);
        }
    }

    /// <summary>
    /// Checks whether transitioning to <paramref name="target"/> is valid according to
    /// the canonical Bill transition matrix (PDF:II.5.2 / V0-DOM-001).
    /// </summary>
    public bool CanTransitionTo(BillState target) => target switch
    {
        BillState.PartiallyAllocated => Status is BillState.Open,
        BillState.Allocated => Status is BillState.Open or BillState.PartiallyAllocated,
        BillState.PartiallyPaid => Status is BillState.Allocated,
        BillState.Paid => Status is BillState.Allocated or BillState.PartiallyPaid,
        BillState.Cancelled => Status is BillState.Open or BillState.PartiallyAllocated
            or BillState.Allocated or BillState.PartiallyPaid,
        BillState.Reopened => Status is BillState.Paid or BillState.Cancelled,
        _ => false,
    };

    /// <summary>
    /// Executes a lifecycle transition on the Bill. Throws if forbidden.
    /// </summary>
    public Bill TransitionTo(BillState target, DateTimeOffset? changedAt = null)
    {
        if (!CanTransitionTo(target))
        {
            throw new InvalidOperationException(
                $"Bill {Id} cannot transition from {Status} to {target}.");
        }

        var at = changedAt ?? DateTimeOffset.UtcNow;

        return new Bill(
            Id,
            BillNumber,
            _items,
            TableId,
            OrderId,
            CustomerAccountId,
            target,
            CurrencyCode,
            AllocatedAmount,
            PaidAmount,
            ChangeAmount,
            OpenedAt,
            target is BillState.Paid ? at : ClosedAt,
            target is BillState.Cancelled ? at : CancelledAt,
            target is BillState.Reopened ? at : ReopenedAt,
            RowVersion,
            CreatedAt,
            at);
    }

    /// <summary>
    /// Cancels an open or unclosed Bill.
    /// </summary>
    public Bill Cancel(DateTimeOffset? cancelledAt = null)
        => TransitionTo(BillState.Cancelled, cancelledAt);

    /// <summary>
    /// Explicitly reopens a Paid or Cancelled Bill (V0-DOM-001 reopen policy).
    /// </summary>
    public Bill Reopen(DateTimeOffset? reopenedAt = null)
        => TransitionTo(BillState.Reopened, reopenedAt);

    /// <summary>
    /// Appends a new item to this Bill. The bill must be in Open or Reopened status.
    /// </summary>
    public Bill AddItem(BillItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status is not (BillState.Open or BillState.Reopened))
        {
            throw new InvalidOperationException(
                $"Cannot add items to Bill {Id} in state {Status}.");
        }

        if (_items.Any(i => i.OrderItemId == item.OrderItemId))
        {
            throw new InvalidOperationException(
                $"Order item {item.OrderItemId} is already attached to Bill {Id}.");
        }

        var updatedItems = _items.Append(item.BillId == Id ? item : item.ForBill(Id)).ToList();
        return RebuildWith(items: updatedItems);
    }

    /// <summary>
    /// Appends multiple items to this Bill.
    /// </summary>
    public Bill AddItems(IEnumerable<BillItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var result = this;
        foreach (var item in items)
            result = result.AddItem(item);

        return result;
    }

    /// <summary>
    /// Removes an item from this Bill by item ID.
    /// </summary>
    public Bill RemoveItem(Guid billItemId)
    {
        if (Status is not (BillState.Open or BillState.Reopened))
        {
            throw new InvalidOperationException(
                $"Cannot remove items from Bill {Id} in state {Status}.");
        }

        var item = _items.FirstOrDefault(i => i.Id == billItemId);
        if (item is null)
            return this;

        var updatedItems = _items.Where(i => i.Id != billItemId).ToList();
        return RebuildWith(items: updatedItems);
    }

    /// <summary>
    /// Creates a Bill directly from an Order, adding all active/non-cancelled order items.
    /// Sets order.TableId and order.Id as initial metadata.
    /// </summary>
    public static Bill FromOrder(
        Guid billId,
        string billNumber,
        Order order,
        DateTimeOffset? openedAt = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (string.IsNullOrWhiteSpace(billNumber))
            throw new ArgumentException("Bill number cannot be empty.", nameof(billNumber));

        var billItems = order.Items
            .Where(i => i.Status != OrderItemState.Cancelled)
            .Select(i => BillItem.FromOrderItem(billId, i))
            .ToList();

        return new Bill(
            id: billId,
            billNumber: billNumber,
            items: billItems,
            tableId: order.TableId,
            orderId: order.Id,
            status: BillState.Open,
            currencyCode: order.CurrencyCode,
            openedAt: openedAt ?? DateTimeOffset.UtcNow);
    }

    private Bill RebuildWith(IReadOnlyList<BillItem> items)
    {
        return new Bill(
            Id,
            BillNumber,
            items,
            TableId,
            OrderId,
            CustomerAccountId,
            Status,
            CurrencyCode,
            AllocatedAmount,
            PaidAmount,
            ChangeAmount,
            OpenedAt,
            ClosedAt,
            CancelledAt,
            ReopenedAt,
            RowVersion,
            CreatedAt,
            DateTimeOffset.UtcNow);
    }
}
