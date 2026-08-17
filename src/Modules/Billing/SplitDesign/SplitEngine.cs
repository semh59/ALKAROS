using ALKAROS.Billing.BillFoundation;

namespace ALKAROS.Billing.SplitDesign;

/// <summary>
/// Domain engine that calculates deterministic, lossless split allocations for Bills (V1-BIL-002 / V0-CMP-002).
/// Enforces that no item is over-allocated and that the sum of allocated amounts matches the target payable.
/// </summary>
public static class SplitEngine
{
    /// <summary>
    /// Calculates an equal split of a Bill across <paramref name="personCount"/> people.
    /// Distributes remainder kuruş deterministically to the last allocation so the sum exactly equals <see cref="Bill.PayableAmount"/>.
    /// </summary>
    public static IReadOnlyList<BillAllocation> CreateEqualSplit(
        Bill bill,
        int personCount,
        IReadOnlyList<string>? personLabels = null,
        Guid? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(bill);

        if (personCount < 2)
            throw new ArgumentException("Equal split requires at least 2 people.", nameof(personCount));
        if (bill.PayableAmount <= 0)
            throw new InvalidOperationException($"Cannot split Bill {bill.Id} with non-positive payable amount {bill.PayableAmount}.");

        var totalPayable = bill.PayableAmount;
        var totalTax = bill.TaxTotal;

        // Base amount rounded down to kuruş
        var baseAmount = BillMath.RoundCurrency(Math.Floor((totalPayable / personCount) * 100m) / 100m);
        if (baseAmount <= 0m)
        {
            throw new InvalidOperationException(
                $"Cannot split payable amount {totalPayable} equally among {personCount} people because amount per person is less than 0.01 TRY.");
        }

        var baseTax = totalTax > 0 ? BillMath.RoundCurrency(Math.Floor((totalTax / personCount) * 100m) / 100m) : 0m;

        var amountRemainder = totalPayable - (baseAmount * personCount);
        var taxRemainder = totalTax - (baseTax * personCount);

        var allocations = new List<BillAllocation>(personCount);

        for (var i = 0; i < personCount; i++)
        {
            var isLast = i == personCount - 1;
            var label = personLabels is not null && i < personLabels.Count && !string.IsNullOrWhiteSpace(personLabels[i])
                ? personLabels[i]
                : $"Person {i + 1}";

            var allocatedAmount = baseAmount + (isLast ? amountRemainder : 0m);
            var taxAmount = baseTax + (isLast ? taxRemainder : 0m);

            allocations.Add(new BillAllocation(
                id: Guid.NewGuid(),
                billId: bill.Id,
                ownerType: AllocationOwnerType.Person,
                ownerReference: label,
                allocatedAmount: allocatedAmount,
                taxAmount: taxAmount,
                createdBy: createdBy));
        }

        return allocations;
    }

    /// <summary>
    /// Calculates an explicit amount split for a Bill.
    /// Enforces that the sum of target amounts matches <see cref="Bill.PayableAmount"/> exactly.
    /// </summary>
    public static IReadOnlyList<BillAllocation> CreateAmountSplit(
        Bill bill,
        IReadOnlyList<(string OwnerReference, decimal Amount)> targets,
        Guid? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(targets);

        if (targets.Count < 2)
            throw new ArgumentException("Amount split requires at least 2 allocation targets.", nameof(targets));

        foreach (var (owner, amount) in targets)
        {
            if (string.IsNullOrWhiteSpace(owner))
                throw new ArgumentException("Target has empty owner reference.", nameof(targets));
            if (amount <= 0)
                throw new ArgumentException($"Target amount for '{owner}' must be positive.", nameof(targets));
        }

        var totalSpecified = targets.Sum(t => BillMath.RoundCurrency(t.Amount));
        if (totalSpecified != bill.PayableAmount)
        {
            throw new InvalidOperationException(
                $"Sum of split amounts ({totalSpecified}) does not match Bill payable amount ({bill.PayableAmount}).");
        }

        var totalTax = bill.TaxTotal;
        var allocations = new List<BillAllocation>(targets.Count);
        var runningTax = 0m;

        for (var i = 0; i < targets.Count; i++)
        {
            var (owner, amount) = targets[i];
            var roundedAmount = BillMath.RoundCurrency(amount);

            decimal taxAmount;
            if (i == targets.Count - 1)
            {
                taxAmount = BillMath.RoundCurrency(totalTax - runningTax);
            }
            else
            {
                taxAmount = bill.PayableAmount > 0
                    ? BillMath.RoundCurrency(totalTax * (roundedAmount / bill.PayableAmount))
                    : 0m;
                runningTax += taxAmount;
            }

            allocations.Add(new BillAllocation(
                id: Guid.NewGuid(),
                billId: bill.Id,
                ownerType: AllocationOwnerType.Amount,
                ownerReference: owner,
                allocatedAmount: roundedAmount,
                taxAmount: taxAmount,
                createdBy: createdBy));
        }

        return allocations;
    }

    /// <summary>
    /// Calculates an item-based split where bill items or partial item quantities are assigned to owners.
    /// Enforces that no bill item is over-allocated (<c>sum(allocated_quantity) &lt;= item.quantity</c>).
    /// </summary>
    public static IReadOnlyList<BillAllocation> CreateItemSplit(
        Bill bill,
        IReadOnlyList<ItemSplitTarget> targets,
        Guid? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(targets);

        if (targets.Count == 0)
            throw new ArgumentException("Item split requires at least one target assignment.", nameof(targets));

        foreach (var target in targets)
        {
            if (target.BillItemId == Guid.Empty)
                throw new ArgumentException("Target bill item id cannot be empty.", nameof(targets));
            if (string.IsNullOrWhiteSpace(target.OwnerReference))
                throw new ArgumentException("Target owner reference cannot be empty.", nameof(targets));
            if (target.Quantity <= 0)
                throw new ArgumentException($"Allocated quantity for owner '{target.OwnerReference}' must be positive.", nameof(targets));
        }

        var itemsById = bill.Items.ToDictionary(i => i.Id);

        // Group targets by bill item ID to validate against over-allocation
        var groupedByItem = targets.GroupBy(t => t.BillItemId);

        var allocations = new List<BillAllocation>(targets.Count);

        foreach (var group in groupedByItem)
        {
            if (!itemsById.TryGetValue(group.Key, out var billItem))
            {
                throw new InvalidOperationException(
                    $"Bill item {group.Key} does not exist in Bill {bill.Id}.");
            }

            if (billItem.GrossAmount <= 0m)
            {
                throw new InvalidOperationException(
                    $"Cannot split item '{billItem.ProductNameSnapshot}' because its gross amount is {billItem.GrossAmount}. Only items with positive payable amount can be allocated.");
            }

            var groupList = group.ToList();
            var totalAllocatedQty = groupList.Sum(t => t.Quantity);

            if (totalAllocatedQty > billItem.Quantity)
            {
                throw new InvalidOperationException(
                    $"Total allocated quantity ({totalAllocatedQty}) for item '{billItem.ProductNameSnapshot}' exceeds item quantity ({billItem.Quantity}).");
            }

            var isFullyAllocated = totalAllocatedQty == billItem.Quantity;
            var runningItemGross = 0m;
            var runningItemTax = 0m;

            for (var i = 0; i < groupList.Count; i++)
            {
                var target = groupList[i];
                var isLastTargetForThisItem = i == groupList.Count - 1;

                decimal targetGross;
                decimal targetTax;

                if (isLastTargetForThisItem && isFullyAllocated)
                {
                    // Remainder distribution so sum of targets equals exact item totals
                    targetGross = BillMath.RoundCurrency(billItem.GrossAmount - runningItemGross);
                    targetTax = BillMath.RoundCurrency(billItem.TaxAmount - runningItemTax);
                }
                else
                {
                    var fraction = target.Quantity / billItem.Quantity;
                    targetGross = BillMath.RoundCurrency(billItem.GrossAmount * fraction);
                    targetTax = BillMath.RoundCurrency(billItem.TaxAmount * fraction);

                    runningItemGross += targetGross;
                    runningItemTax += targetTax;
                }

                allocations.Add(new BillAllocation(
                    id: Guid.NewGuid(),
                    billId: bill.Id,
                    ownerType: AllocationOwnerType.Item,
                    ownerReference: target.OwnerReference,
                    allocatedAmount: targetGross,
                    taxAmount: targetTax,
                    billItemId: billItem.Id,
                    allocatedQuantity: target.Quantity,
                    createdBy: createdBy));
            }
        }

        return allocations;
    }
}

/// <summary>
/// Parameter record for item-based split assignments.
/// </summary>
public sealed record ItemSplitTarget(
    Guid BillItemId,
    string OwnerReference,
    decimal Quantity);
