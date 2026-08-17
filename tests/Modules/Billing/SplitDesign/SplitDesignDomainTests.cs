using ALKAROS.Billing.BillFoundation;
using Xunit;

namespace ALKAROS.Billing.SplitDesign.Tests;

/// <summary>
/// Domain unit tests for SplitEngine and BillAllocation entities.
/// Verifies invariant enforcement, deterministic kuruş remainder allocation, item over-allocation prevention, and losslessness.
/// </summary>
public sealed class SplitDesignDomainTests
{
    [Fact]
    public void BillAllocationConstructorValidatesAllInvariants()
    {
        var id = Guid.NewGuid();
        var billId = Guid.NewGuid();

        // Empty ID
        Assert.Throws<ArgumentException>(() =>
            new BillAllocation(Guid.Empty, billId, AllocationOwnerType.Person, "Person 1", 50m));

        // Empty Bill ID
        Assert.Throws<ArgumentException>(() =>
            new BillAllocation(id, Guid.Empty, AllocationOwnerType.Person, "Person 1", 50m));

        // Empty Owner Reference
        Assert.Throws<ArgumentException>(() =>
            new BillAllocation(id, billId, AllocationOwnerType.Person, "", 50m));

        // Zero / Negative Amount
        Assert.Throws<ArgumentException>(() =>
            new BillAllocation(id, billId, AllocationOwnerType.Person, "Person 1", 0m));
        Assert.Throws<ArgumentException>(() =>
            new BillAllocation(id, billId, AllocationOwnerType.Person, "Person 1", -10m));

        // Negative Tax Amount
        Assert.Throws<ArgumentException>(() =>
            new BillAllocation(id, billId, AllocationOwnerType.Person, "Person 1", 50m, taxAmount: -1m));

        // Zero / Negative Quantity
        Assert.Throws<ArgumentException>(() =>
            new BillAllocation(id, billId, AllocationOwnerType.Item, "Person 1", 50m, allocatedQuantity: 0m));
        Assert.Throws<ArgumentException>(() =>
            new BillAllocation(id, billId, AllocationOwnerType.Item, "Person 1", 50m, allocatedQuantity: -2m));

        // Valid
        var valid = new BillAllocation(
            id,
            billId,
            AllocationOwnerType.Person,
            "Person 1",
            50m,
            taxAmount: 5m,
            allocatedQuantity: 1m);
        Assert.Equal(50m, valid.AllocatedAmount);
        Assert.Equal(5m, valid.TaxAmount);
        Assert.Equal(1m, valid.AllocatedQuantity);
    }

    [Fact]
    public void EqualSplitDistributesPayableAmountAndTaxesLosslesslyWithRemainder()
    {
        // 100.00 TRY total payable with 10% tax (Net 90.91, Tax 9.09, Payable 100.00)
        // Split among 3 persons -> 33.33, 33.33, 33.34
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Test Item",
            quantity: 1,
            unitPrice: 90.91m,
            taxRate: 10m,
            taxAmount: 9.09m,
            netAmount: 90.91m,
            grossAmount: 100.00m);

        var bill = new Bill(item.BillId, "BILL-001", new[] { item });

        var allocations = SplitEngine.CreateEqualSplit(bill, personCount: 3);

        Assert.Equal(3, allocations.Count);
        Assert.Equal("Person 1", allocations[0].OwnerReference);
        Assert.Equal(33.33m, allocations[0].AllocatedAmount);

        Assert.Equal("Person 2", allocations[1].OwnerReference);
        Assert.Equal(33.33m, allocations[1].AllocatedAmount);

        // Last person receives the remainder kuruş (33.34)
        Assert.Equal("Person 3", allocations[2].OwnerReference);
        Assert.Equal(33.34m, allocations[2].AllocatedAmount);

        // Sum of allocations matches bill.PayableAmount exactly
        Assert.Equal(bill.PayableAmount, allocations.Sum(a => a.AllocatedAmount));
        // Sum of taxes matches bill.TaxTotal exactly
        Assert.Equal(bill.TaxTotal, allocations.Sum(a => a.TaxAmount));
    }

    [Fact]
    public void EqualSplitWithLargeNumbersAndPrimeDivisorWorksLosslessly()
    {
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Large Feast",
            quantity: 1,
            unitPrice: 909090.90m,
            taxRate: 10m,
            taxAmount: 90909.09m,
            netAmount: 909090.90m,
            grossAmount: 999999.99m);

        var bill = new Bill(item.BillId, "BILL-LARGE", new[] { item });

        var allocations = SplitEngine.CreateEqualSplit(bill, personCount: 7);
        Assert.Equal(7, allocations.Count);
        Assert.Equal(bill.PayableAmount, allocations.Sum(a => a.AllocatedAmount));
        Assert.Equal(bill.TaxTotal, allocations.Sum(a => a.TaxAmount));
    }

    [Fact]
    public void EqualSplitWithSubKurusAmountThrowsInformativeException()
    {
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Candy",
            quantity: 1,
            unitPrice: 0.02m,
            taxRate: 0m,
            taxAmount: 0m,
            netAmount: 0.02m,
            grossAmount: 0.02m);

        var bill = new Bill(item.BillId, "BILL-TINY", new[] { item });

        // 0.02 TRY divided by 3 results in < 0.01 per person
        var ex = Assert.Throws<InvalidOperationException>(() => SplitEngine.CreateEqualSplit(bill, 3));
        Assert.Contains("less than 0.01 TRY", ex.Message);
    }

    [Fact]
    public void EqualSplitAppliesCustomLabelsWhenProvided()
    {
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Doner",
            quantity: 1,
            unitPrice: 100m,
            taxRate: 10m);

        var bill = new Bill(item.BillId, "BILL-002", new[] { item });

        var labels = new[] { "Ahmet", "Mehmet" };
        var allocations = SplitEngine.CreateEqualSplit(bill, 2, labels);

        Assert.Equal("Ahmet", allocations[0].OwnerReference);
        Assert.Equal("Mehmet", allocations[1].OwnerReference);
        Assert.Equal(item.GrossAmount / 2, allocations[0].AllocatedAmount);
        Assert.Equal(item.GrossAmount / 2, allocations[1].AllocatedAmount);
    }

    [Fact]
    public void EqualSplitRejectsInvalidPersonCountOrZeroPayable()
    {
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Test",
            quantity: 1,
            unitPrice: 100m,
            taxRate: 10m);

        var bill = new Bill(item.BillId, "BILL-003", new[] { item });

        Assert.Throws<ArgumentException>(() => SplitEngine.CreateEqualSplit(bill, 1));
        Assert.Throws<ArgumentException>(() => SplitEngine.CreateEqualSplit(bill, 0));
        Assert.Throws<ArgumentException>(() => SplitEngine.CreateEqualSplit(bill, -2));
    }

    [Fact]
    public void AmountSplitValidatesTotalSumMatchesBillPayable()
    {
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Kebab",
            quantity: 1,
            unitPrice: 200m,
            taxRate: 10m); // Gross = 220.00

        var bill = new Bill(item.BillId, "BILL-004", new[] { item });

        // Sum matches 220.00
        var validTargets = new[]
        {
            ("Guest 1", 100.00m),
            ("Guest 2", 120.00m)
        };
        var allocations = SplitEngine.CreateAmountSplit(bill, validTargets);
        Assert.Equal(2, allocations.Count);
        Assert.Equal(220.00m, allocations.Sum(a => a.AllocatedAmount));
        Assert.Equal(bill.TaxTotal, allocations.Sum(a => a.TaxAmount));

        // Sum does not match (210 != 220)
        var invalidTargets = new[]
        {
            ("Guest 1", 100.00m),
            ("Guest 2", 110.00m)
        };
        Assert.Throws<InvalidOperationException>(() => SplitEngine.CreateAmountSplit(bill, invalidTargets));

        // Less than 2 targets
        Assert.Throws<ArgumentException>(() =>
            SplitEngine.CreateAmountSplit(bill, new[] { ("Guest 1", 220m) }));
    }

    [Fact]
    public void AmountSplitRejectsNonPositiveOrEmptyTargetAmounts()
    {
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Kebab",
            quantity: 1,
            unitPrice: 200m,
            taxRate: 10m); // Gross = 220.00

        var bill = new Bill(item.BillId, "BILL-004B", new[] { item });

        // Target with 0 amount
        Assert.Throws<ArgumentException>(() =>
            SplitEngine.CreateAmountSplit(bill, new[] { ("Guest 1", 0m), ("Guest 2", 220m) }));

        // Target with negative amount
        Assert.Throws<ArgumentException>(() =>
            SplitEngine.CreateAmountSplit(bill, new[] { ("Guest 1", -10m), ("Guest 2", 230m) }));

        // Target with empty name
        Assert.Throws<ArgumentException>(() =>
            SplitEngine.CreateAmountSplit(bill, new[] { ("", 110m), ("Guest 2", 110m) }));
    }

    [Fact]
    public void ItemSplitDistributesGrossAndTaxAcrossOwnersLosslessly()
    {
        var billId = Guid.NewGuid();

        // Item 1: Pizza, Qty 2, UnitPrice 100, TaxRate 10% (Gross = 220, Tax = 20)
        var item1 = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Pizza",
            quantity: 2,
            unitPrice: 100m,
            taxRate: 10m);

        // Item 2: Kola, Qty 1, UnitPrice 40, TaxRate 10% (Gross = 44, Tax = 4)
        var item2 = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Kola",
            quantity: 1,
            unitPrice: 40m,
            taxRate: 10m);

        var bill = new Bill(billId, "BILL-005", new[] { item1, item2 });

        // Person A gets 1 Pizza and 1 Kola
        // Person B gets 1 Pizza
        var targets = new[]
        {
            new ItemSplitTarget(item1.Id, "Person A", 1),
            new ItemSplitTarget(item1.Id, "Person B", 1),
            new ItemSplitTarget(item2.Id, "Person A", 1)
        };

        var allocations = SplitEngine.CreateItemSplit(bill, targets);

        Assert.Equal(3, allocations.Count);

        var personAAllocations = allocations.Where(a => a.OwnerReference == "Person A").ToList();
        var personBAllocations = allocations.Where(a => a.OwnerReference == "Person B").ToList();

        Assert.Equal(2, personAAllocations.Count);
        Assert.Single(personBAllocations);

        // Person A total = 110 (half pizza) + 44 (kola) = 154
        Assert.Equal(154.00m, personAAllocations.Sum(a => a.AllocatedAmount));
        // Person B total = 110 (half pizza)
        Assert.Equal(110.00m, personBAllocations.Sum(a => a.AllocatedAmount));

        // Total matches bill
        Assert.Equal(bill.PayableAmount, allocations.Sum(a => a.AllocatedAmount));
        Assert.Equal(bill.TaxTotal, allocations.Sum(a => a.TaxAmount));
    }

    [Fact]
    public void ItemSplitWithFractionalQuantitiesDistributesLosslessly()
    {
        var billId = Guid.NewGuid();

        // 0.750 kg meat at 800 TRY/kg, 10% tax (Net 600.00, Tax 60.00, Gross 660.00)
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Dana Antrikot",
            quantity: 0.750m,
            unitPrice: 800m,
            taxRate: 10m);

        var bill = new Bill(billId, "BILL-MEAT", new[] { item });

        // Person 1 takes 0.250 kg, Person 2 takes 0.500 kg
        var targets = new[]
        {
            new ItemSplitTarget(item.Id, "Person 1", 0.250m),
            new ItemSplitTarget(item.Id, "Person 2", 0.500m)
        };

        var allocations = SplitEngine.CreateItemSplit(bill, targets);
        Assert.Equal(2, allocations.Count);

        // Person 1 = 1/3 of 660 = 220.00
        Assert.Equal(220.00m, allocations[0].AllocatedAmount);
        Assert.Equal(20.00m, allocations[0].TaxAmount);

        // Person 2 = 2/3 of 660 = 440.00
        Assert.Equal(440.00m, allocations[1].AllocatedAmount);
        Assert.Equal(40.00m, allocations[1].TaxAmount);

        Assert.Equal(item.GrossAmount, allocations.Sum(a => a.AllocatedAmount));
        Assert.Equal(item.TaxAmount, allocations.Sum(a => a.TaxAmount));
    }

    [Fact]
    public void ItemSplitRejectsComplimentaryZeroAmountItem()
    {
        var billId = Guid.NewGuid();
        var compItem = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Ikram Cay",
            quantity: 1,
            unitPrice: 20m,
            taxRate: 10m,
            lineType: BillLineType.Complimentary); // Gross = 0

        var bill = new Bill(billId, "BILL-COMP", new[] { compItem });

        var targets = new[]
        {
            new ItemSplitTarget(compItem.Id, "Person 1", 1)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => SplitEngine.CreateItemSplit(bill, targets));
        Assert.Contains("gross amount is 0", ex.Message);
    }

    [Fact]
    public void ItemSplitRejectsOverAllocation()
    {
        var billId = Guid.NewGuid();
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Lahmacun",
            quantity: 2,
            unitPrice: 80m,
            taxRate: 10m);

        var bill = new Bill(billId, "BILL-006", new[] { item });

        // Targets request 1.5 + 1.0 = 2.5 (item quantity is 2.0)
        var targets = new[]
        {
            new ItemSplitTarget(item.Id, "Person A", 1.5m),
            new ItemSplitTarget(item.Id, "Person B", 1.0m)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => SplitEngine.CreateItemSplit(bill, targets));
        Assert.Contains("exceeds item quantity", ex.Message);
    }

    [Fact]
    public void ItemSplitRejectsNonExistentBillItem()
    {
        var bill = new Bill(Guid.NewGuid(), "BILL-007");

        var targets = new[]
        {
            new ItemSplitTarget(Guid.NewGuid(), "Person A", 1)
        };

        Assert.Throws<InvalidOperationException>(() => SplitEngine.CreateItemSplit(bill, targets));
    }

    [Fact]
    public void ItemSplitRejectsNonPositiveOrInvalidTarget()
    {
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Kola",
            quantity: 1,
            unitPrice: 40m,
            taxRate: 10m);

        var bill = new Bill(item.BillId, "BILL-ITEM-VAL", new[] { item });

        // Empty owner
        Assert.Throws<ArgumentException>(() =>
            SplitEngine.CreateItemSplit(bill, new[] { new ItemSplitTarget(item.Id, "", 1) }));

        // Zero / negative quantity
        Assert.Throws<ArgumentException>(() =>
            SplitEngine.CreateItemSplit(bill, new[] { new ItemSplitTarget(item.Id, "Person A", 0) }));
        Assert.Throws<ArgumentException>(() =>
            SplitEngine.CreateItemSplit(bill, new[] { new ItemSplitTarget(item.Id, "Person A", -1) }));
    }
}
