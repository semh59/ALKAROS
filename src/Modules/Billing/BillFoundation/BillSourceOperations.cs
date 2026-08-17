using ALKAROS.Orders.OrderAggregate;

namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// Domain operations for multi-order bill aggregation and order-to-bills splitting (V0-DOM-002).
/// Enforces that each OrderItem is billed at most once with lossless monetary totals.
/// </summary>
public static class BillSourceOperations
{
    /// <summary>
    /// Creates a single merged Bill containing selected items from multiple Orders.
    /// Used in table merges and multi-order checkout scenarios (PDF:III.7 / V0-DOM-002 Positive 1).
    /// </summary>
    public static Bill CreateMergedBill(
        Guid billId,
        string billNumber,
        IEnumerable<OrderItem> orderItems,
        Guid? tableId = null,
        string currencyCode = "TRY",
        DateTimeOffset? openedAt = null)
    {
        ArgumentNullException.ThrowIfNull(orderItems);
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (string.IsNullOrWhiteSpace(billNumber))
            throw new ArgumentException("Bill number cannot be empty.", nameof(billNumber));

        var itemsList = orderItems.ToList();
        if (itemsList.Count == 0)
            throw new ArgumentException("Cannot create a merged bill with zero items.", nameof(orderItems));

        var duplicate = itemsList.GroupBy(i => i.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Order item {duplicate.Key} appears more than once in the merged items list.");
        }

        var billItems = itemsList
            .Where(i => i.Status != OrderItemState.Cancelled)
            .Select(i => BillItem.FromOrderItem(billId, i))
            .ToList();

        return new Bill(
            id: billId,
            billNumber: billNumber,
            items: billItems,
            tableId: tableId,
            orderId: null, // Merged bill has no single origin order dominance
            status: BillState.Open,
            currencyCode: currencyCode,
            openedAt: openedAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Splits an Order's items into multiple Bills (V0-DOM-002 Positive 2).
    /// Enforces that the item partition is exact and disjoint: each active item appears in exactly one Bill.
    /// </summary>
    public static IReadOnlyList<Bill> CreateSplitBills(
        Order order,
        IReadOnlyList<(Guid BillId, string BillNumber, IReadOnlyList<Guid> OrderItemIds)> splitPartitions,
        DateTimeOffset? openedAt = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(splitPartitions);

        if (splitPartitions.Count < 2)
            throw new ArgumentException("Split requires at least 2 target bills.", nameof(splitPartitions));

        var activeOrderItems = order.Items.Where(i => i.Status != OrderItemState.Cancelled).ToDictionary(i => i.Id);

        var assignedItemIds = new HashSet<Guid>();
        var bills = new List<Bill>(splitPartitions.Count);

        foreach (var (billId, billNumber, itemIds) in splitPartitions)
        {
            if (billId == Guid.Empty)
                throw new ArgumentException("Split bill id cannot be empty.");
            if (string.IsNullOrWhiteSpace(billNumber))
                throw new ArgumentException("Split bill number cannot be empty.");
            if (itemIds is null || itemIds.Count == 0)
                throw new ArgumentException($"Split bill {billNumber} must have at least one assigned item.");

            var billItems = new List<BillItem>(itemIds.Count);
            foreach (var itemId in itemIds)
            {
                if (!activeOrderItems.TryGetValue(itemId, out var orderItem))
                {
                    throw new InvalidOperationException(
                        $"Order item {itemId} does not exist or is not active in Order {order.Id}.");
                }

                if (!assignedItemIds.Add(itemId))
                {
                    throw new InvalidOperationException(
                        $"Order item {itemId} cannot be assigned to multiple split bills (duplicate billing prevention).");
                }

                billItems.Add(BillItem.FromOrderItem(billId, orderItem));
            }

            bills.Add(new Bill(
                id: billId,
                billNumber: billNumber,
                items: billItems,
                tableId: order.TableId,
                orderId: order.Id,
                status: BillState.Open,
                currencyCode: order.CurrencyCode,
                openedAt: openedAt ?? DateTimeOffset.UtcNow));
        }

        // Verify total coverage: every active item in order must be assigned
        if (assignedItemIds.Count != activeOrderItems.Count)
        {
            var unassigned = activeOrderItems.Keys.Except(assignedItemIds);
            throw new InvalidOperationException(
                $"All active order items must be partitioned. Unassigned item(s): {string.Join(", ", unassigned)}");
        }

        return bills;
    }
}
