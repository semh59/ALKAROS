using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using Xunit;

namespace ALKAROS.Billing.BillFoundation.Tests;

public sealed class BillDomainTests
{
    [Fact]
    public void BillCreationWithValidArgumentsInitializesCorrectly()
    {
        var billId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Adana Kebap",
            quantity: 2,
            unitPrice: 200m,
            taxRate: 10m);

        var bill = new Bill(
            id: billId,
            billNumber: "BILL-2026-0001",
            items: new[] { item },
            tableId: tableId,
            orderId: orderId);

        Assert.Equal(billId, bill.Id);
        Assert.Equal("BILL-2026-0001", bill.BillNumber);
        Assert.Equal(tableId, bill.TableId);
        Assert.Equal(orderId, bill.OrderId);
        Assert.Equal(BillState.Open, bill.Status);
        Assert.Equal("TRY", bill.CurrencyCode);
        Assert.Equal(1, bill.RowVersion);
        Assert.Single(bill.Items);

        // Tax-inclusive: 2 * 200 = 400 gross; the 10% tax contained in it is 400 * 10 / 110 = 36.36
        Assert.Equal(400m, bill.Subtotal);
        Assert.Equal(0m, bill.DiscountTotal);
        Assert.Equal(36.36m, bill.TaxTotal);
        Assert.Equal(400m, bill.PayableAmount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void BillCreationInvalidBillNumberThrows(string? billNumber)
    {
        Assert.Throws<ArgumentException>(() => new Bill(
            id: Guid.NewGuid(),
            billNumber: billNumber!));
    }

    [Fact]
    public void BillCreationEmptyIdThrows()
    {
        Assert.Throws<ArgumentException>(() => new Bill(
            id: Guid.Empty,
            billNumber: "BILL-001"));
    }

    [Fact]
    public void BillCreationDuplicateOrderItemInSameBillThrows()
    {
        var billId = Guid.NewGuid();
        var duplicateOrderItemId = Guid.NewGuid();

        var item1 = new BillItem(Guid.NewGuid(), billId, duplicateOrderItemId, Guid.NewGuid(), "Item 1", 1, 100m, 10m);
        var item2 = new BillItem(Guid.NewGuid(), billId, duplicateOrderItemId, Guid.NewGuid(), "Item 2", 1, 100m, 10m);

        Assert.Throws<InvalidOperationException>(() => new Bill(
            id: billId,
            billNumber: "BILL-001",
            items: new[] { item1, item2 }));
    }

    [Fact]
    public void BillItemCreationInvalidArgumentsThrows()
    {
        var billId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Empty ID
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.Empty, billId, orderItemId, productId, "P", 1, 10m, 10m));
        // Empty bill ID
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), Guid.Empty, orderItemId, productId, "P", 1, 10m, 10m));
        // Empty order item ID
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, Guid.Empty, productId, "P", 1, 10m, 10m));
        // Empty product ID
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, Guid.Empty, "P", 1, 10m, 10m));
        // Empty product name
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, productId, "", 1, 10m, 10m));
        // Non-positive quantity
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, productId, "P", 0, 10m, 10m));
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, productId, "P", -1, 10m, 10m));
        // Negative unit price
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, productId, "P", 1, -5m, 10m));
        // Negative tax rate
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, productId, "P", 1, 10m, -1m));
        // Negative discount amount
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, productId, "P", 1, 10m, 10m, discountAmount: -5m));
        // Discount greater than the line subtotal
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, productId, "P", 1, 100m, 10m, discountAmount: 150m));
        // Persisted negative monetary snapshots
        Assert.Throws<ArgumentException>(() => new BillItem(Guid.NewGuid(), billId, orderItemId, productId, "P", 1, 100m, 10m, netAmount: -1m));
    }

    [Fact]
    public void BillItemFromOrderItemPreservesFrozenSnapshots()
    {
        var orderId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var orderItem = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: Guid.NewGuid(),
            productNameSnapshot: "Iskender",
            quantity: 3,
            unitPrice: 250m,
            taxRate: 10m,
            skuSnapshot: "ISK-01",
            discountAmount: 50m,
            notes: "Extra tereyag");

        var billItem = BillItem.FromOrderItem(billId, orderItem);

        Assert.Equal(billId, billItem.BillId);
        Assert.Equal(orderItem.Id, billItem.OrderItemId);
        Assert.Equal(orderItem.ProductId, billItem.ProductId);
        Assert.Equal("Iskender", billItem.ProductNameSnapshot);
        Assert.Equal(3, billItem.Quantity);
        Assert.Equal(250m, billItem.UnitPrice);
        Assert.Equal(50m, billItem.DiscountAmount);
        Assert.Equal(10m, billItem.TaxRate);
        Assert.Equal(BillLineType.Sale, billItem.LineType);
        Assert.Equal("Extra tereyag", billItem.Notes);

        // 3 * 250 = 750; gross = 750 - 50 = 700; tax contained = 700 * 10 / 110 = 63.64; net = 636.36
        Assert.Equal(636.36m, billItem.NetAmount);
        Assert.Equal(63.64m, billItem.TaxAmount);
        Assert.Equal(700m, billItem.GrossAmount);
    }

    [Fact]
    public void BillItemComplimentaryLineHasZeroTaxableBase()
    {
        var orderId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var orderItem = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: Guid.NewGuid(),
            productNameSnapshot: "Cay",
            quantity: 4,
            unitPrice: 20m,
            taxRate: 10m,
            status: OrderItemState.Complimentary);

        var billItem = BillItem.FromOrderItem(billId, orderItem);

        Assert.Equal(BillLineType.Complimentary, billItem.LineType);
        Assert.Equal(0m, billItem.NetAmount);
        Assert.Equal(0m, billItem.TaxAmount);
        Assert.Equal(0m, billItem.GrossAmount);
        // V1-RMD-228: the payable total is 0, but the real gross value (4 *
        // 20 = 80) must stay visible as a 100% discount for fiscal
        // reporting — not silently discarded.
        Assert.Equal(80m, billItem.DiscountAmount);
        Assert.Equal(80m, billItem.LineSubtotal);
    }

    [Fact]
    public void ComplimentaryLineConstructedDirectlyRejectsAPartialDiscount()
    {
        var ex = Assert.Throws<ArgumentException>(() => new BillItem(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Ikram Kahve",
            quantity: 1,
            unitPrice: 50m,
            taxRate: 10m,
            discountAmount: 30m,
            lineType: BillLineType.Complimentary));

        Assert.Contains("fully discounted", ex.Message);
    }

    [Fact]
    public void ComplimentaryFromOrderItemRejectsADiscountThatDoesNotMatchTheTrueGross()
    {
        // Simulates a reloaded OrderItem where a modifier's price delta
        // (50) was somehow left out of NetAmount when the item was last
        // persisted (netAmount explicitly 100 instead of the real 150 =
        // 100 unit price + 50 modifier). Before V1-RMD-231, FromOrderItem's
        // Complimentary branch derived its own "lineSubtotal" from
        // NetAmount + DiscountAmount, so this inconsistency was invisible
        // -- the check compared DiscountAmount to itself. Now it is cross-
        // checked against OrderItem.LineSubtotalValue (quantity * unit
        // price + modifiers), an independent value, so it must throw.
        var modifier = new OrderItemModifier(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Extra Cheese", priceDelta: 50m, quantity: 1);
        var orderItem = new OrderItem(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Burger",
            quantity: 1,
            unitPrice: 100m,
            taxRate: 10m,
            modifiers: new[] { modifier },
            status: OrderItemState.Complimentary,
            netAmount: 100m,
            taxAmount: 10m,
            grossAmount: 110m);

        var ex = Assert.Throws<ArgumentException>(() => BillItem.FromOrderItem(Guid.NewGuid(), orderItem));
        Assert.Contains("fully discounted", ex.Message);
    }

    [Fact]
    public void MixedSaleAndComplimentaryBillTotalsPayableExcludesTheCompLine()
    {
        var billId = Guid.NewGuid();
        var saleItem = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Iskender",
            quantity: 1,
            unitPrice: 100m,
            taxRate: 0m);
        var compItem = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Cay",
            quantity: 1,
            unitPrice: 50m,
            taxRate: 0m,
            discountAmount: 50m,
            netAmount: 0m,
            lineType: BillLineType.Complimentary);

        var bill = new Bill(billId, "BILL-MIXED-01", new[] { saleItem, compItem });

        Assert.Equal(150m, bill.Subtotal);
        Assert.Equal(50m, bill.DiscountTotal);
        Assert.Equal(100m, bill.PayableAmount);
    }

    [Fact]
    public void MoneyTaxRoundingMatchesV0Cmp002KurusInvariant()
    {
        var billId = Guid.NewGuid();

        // 1 line priced 9.99 tax-inclusive at 10%: tax = 9.99 * 10 / 110 = 0.908 -> 0.91, net = 9.08
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Su",
            quantity: 1,
            unitPrice: 9.99m,
            taxRate: 10m);

        Assert.Equal(9.08m, item.NetAmount);
        Assert.Equal(0.91m, item.TaxAmount);
        Assert.Equal(9.99m, item.GrossAmount);

        var bill = new Bill(billId, "BILL-ROUND-01", new[] { item });
        Assert.Equal(9.99m, bill.Subtotal);
        Assert.Equal(0.91m, bill.TaxTotal);
        Assert.Equal(9.99m, bill.PayableAmount);
    }

    [Fact]
    public void CanonicalTransitionMatrixValidTransitionsSucceed()
    {
        var bill = new Bill(Guid.NewGuid(), "BILL-TRANS-01");
        Assert.Equal(BillState.Open, bill.Status);

        // Open -> PartiallyAllocated
        var partiallyAllocated = bill.TransitionTo(BillState.PartiallyAllocated);
        Assert.Equal(BillState.PartiallyAllocated, partiallyAllocated.Status);

        // PartiallyAllocated -> Allocated
        var allocated = partiallyAllocated.TransitionTo(BillState.Allocated);
        Assert.Equal(BillState.Allocated, allocated.Status);

        // Allocated -> PartiallyPaid
        var partiallyPaid = allocated.TransitionTo(BillState.PartiallyPaid);
        Assert.Equal(BillState.PartiallyPaid, partiallyPaid.Status);

        // PartiallyPaid -> Paid
        var paid = partiallyPaid.TransitionTo(BillState.Paid);
        Assert.Equal(BillState.Paid, paid.Status);
        Assert.NotNull(paid.ClosedAt);

        // Paid -> Reopened (V0-DOM-001 explicit audited action)
        var reopened = paid.Reopen();
        Assert.Equal(BillState.Reopened, reopened.Status);
        Assert.NotNull(reopened.ReopenedAt);
    }

    [Fact]
    public void CancellationFromOpenOrAllocatedSucceeds()
    {
        var bill = new Bill(Guid.NewGuid(), "BILL-CANCEL-01");
        var cancelled = bill.Cancel();
        Assert.Equal(BillState.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CancelledAt);

        // Cancelled -> Reopened
        var reopened = cancelled.Reopen();
        Assert.Equal(BillState.Reopened, reopened.Status);
    }

    [Theory]
    [InlineData(BillState.Open, BillState.Paid)] // Skip not allowed
    [InlineData(BillState.Open, BillState.PartiallyPaid)] // Skip not allowed
    [InlineData(BillState.Open, BillState.Reopened)] // Cannot reopen unclosed bill
    [InlineData(BillState.Paid, BillState.Open)] // Silent reopen forbidden
    [InlineData(BillState.Cancelled, BillState.Allocated)] // Cannot transition cancelled to allocated
    public void CanonicalTransitionMatrixForbiddenTransitionsThrow(BillState initial, BillState target)
    {
        var bill = new Bill(Guid.NewGuid(), "BILL-FAIL", status: initial);
        Assert.False(bill.CanTransitionTo(target));
        Assert.Throws<InvalidOperationException>(() => bill.TransitionTo(target));
    }

    [Theory]
    [InlineData(BillState.PartiallyAllocated)]
    [InlineData(BillState.Allocated)]
    [InlineData(BillState.Cancelled)]
    public void ReopenedBillCanTransitionToActiveAndTerminalStates(BillState target)
    {
        var bill = new Bill(Guid.NewGuid(), "BILL-REOPENED", status: BillState.Reopened);
        Assert.True(bill.CanTransitionTo(target));
        var transitioned = bill.TransitionTo(target);
        Assert.Equal(target, transitioned.Status);
    }

    [Fact]
    public void BillItemFromOrderItemWithModifiersAndDiscountCalculatesAccurately()
    {
        var modifier = new OrderItemModifier(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Extra Cheese", priceDelta: 50m, quantity: 1);
        var orderItem = new OrderItem(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Burger",
            quantity: 1,
            unitPrice: 100m,
            taxRate: 10m,
            discountAmount: 30m,
            modifiers: new[] { modifier });

        var billItem = BillItem.FromOrderItem(Guid.NewGuid(), orderItem);

        Assert.Equal(109.09m, billItem.NetAmount);
        Assert.Equal(10.91m, billItem.TaxAmount);
        Assert.Equal(120m, billItem.GrossAmount);
        Assert.Equal(150m, billItem.LineSubtotal);
    }

    [Fact]
    public void AddItemWhenOpenOrReopenedSucceeds()
    {
        var bill = new Bill(Guid.NewGuid(), "BILL-ADD-01");
        var item1 = new BillItem(Guid.NewGuid(), bill.Id, Guid.NewGuid(), Guid.NewGuid(), "Item 1", 1, 50m, 10m);

        var updated = bill.AddItem(item1);
        Assert.Single(updated.Items);
        Assert.Equal(50m, updated.PayableAmount);

        // Duplicate item throws
        Assert.Throws<InvalidOperationException>(() => updated.AddItem(item1));
    }

    [Fact]
    public void AddItemWhenPaidOrCancelledThrows()
    {
        var paidBill = new Bill(Guid.NewGuid(), "BILL-PAID", status: BillState.Paid);
        var item = new BillItem(Guid.NewGuid(), paidBill.Id, Guid.NewGuid(), Guid.NewGuid(), "Item 1", 1, 50m, 10m);

        Assert.Throws<InvalidOperationException>(() => paidBill.AddItem(item));

        var cancelledBill = new Bill(Guid.NewGuid(), "BILL-CANC", status: BillState.Cancelled);
        Assert.Throws<InvalidOperationException>(() => cancelledBill.AddItem(item));
    }

    [Fact]
    public void RemoveItemWhenOpenSucceeds()
    {
        var billId = Guid.NewGuid();
        var item1 = new BillItem(Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), "Item 1", 1, 50m, 10m);
        var item2 = new BillItem(Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), "Item 2", 1, 100m, 10m);

        var bill = new Bill(billId, "BILL-REM", new[] { item1, item2 });
        Assert.Equal(2, bill.Items.Count);

        var afterRemove = bill.RemoveItem(item1.Id);
        Assert.Single(afterRemove.Items);
        Assert.Equal(item2.Id, afterRemove.Items[0].Id);
    }

    [Fact]
    public void BillFromOrderCreatesBillWithActiveItemsOnly()
    {
        var orderId = Guid.NewGuid();
        var activeItem = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Active Item", 2, 100m, 10m, status: OrderItemState.Active);
        var cancelledItem = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Cancelled Item", 1, 50m, 10m, status: OrderItemState.Cancelled);

        var order = new Order(
            id: orderId,
            source: OrderSource.Waiter,
            orderNumber: "ORD-001",
            items: new[] { activeItem, cancelledItem },
            tableId: Guid.NewGuid());

        var bill = Bill.FromOrder(Guid.NewGuid(), "BILL-ORD-01", order);

        Assert.Equal(BillState.Open, bill.Status);
        Assert.Equal(order.TableId, bill.TableId);
        Assert.Equal(order.Id, bill.OrderId);
        Assert.Single(bill.Items);
        Assert.Equal(activeItem.Id, bill.Items[0].OrderItemId);
        Assert.Equal(200m, bill.PayableAmount);
    }

    [Fact]
    public void BillSourceOperationsCreateMergedBillCombinesMultipleOrders()
    {
        var order1Item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Manti", 1, 150m, 10m);
        var order2Item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ayran", 2, 25m, 10m);

        var billId = Guid.NewGuid();
        var mergedBill = BillSourceOperations.CreateMergedBill(
            billId: billId,
            billNumber: "BILL-MERGE-01",
            orderItems: new[] { order1Item, order2Item });

        Assert.Equal(2, mergedBill.Items.Count);
        Assert.Null(mergedBill.OrderId); // Merged bill has no single origin order dominance

        // Tax-inclusive: 150 + 2 * 25 = 200
        Assert.Equal(200m, mergedBill.PayableAmount);
    }

    [Fact]
    public void BillSourceOperationsCreateSplitBillsExactDisjointPartition()
    {
        var orderId = Guid.NewGuid();
        var item1 = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Corba", 1, 80m, 10m);
        var item2 = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Pide", 1, 180m, 10m);
        var item3 = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Kola", 1, 40m, 10m);

        var order = new Order(
            id: orderId,
            source: OrderSource.Waiter,
            orderNumber: "ORD-SPLIT-01",
            items: new[] { item1, item2, item3 });

        var splitPartitions = new[]
        {
            (Guid.NewGuid(), "BILL-SPLIT-A", (IReadOnlyList<Guid>)new[] { item1.Id }),
            (Guid.NewGuid(), "BILL-SPLIT-B", (IReadOnlyList<Guid>)new[] { item2.Id, item3.Id })
        };

        var bills = BillSourceOperations.CreateSplitBills(order, splitPartitions);

        Assert.Equal(2, bills.Count);
        Assert.Single(bills[0].Items);
        Assert.Equal(2, bills[1].Items.Count);

        // Sum of split bills payable equals original order total
        // Tax-inclusive: item1 = 80; item2 + item3 = 180 + 40 = 220; total = 300
        Assert.Equal(80m, bills[0].PayableAmount);
        Assert.Equal(220m, bills[1].PayableAmount);
        Assert.Equal(order.Total, bills[0].PayableAmount + bills[1].PayableAmount);
    }

    [Fact]
    public void BillSourceOperationsCreateSplitBillsUnassignedOrDuplicateItemThrows()
    {
        var orderId = Guid.NewGuid();
        var item1 = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Item 1", 1, 100m, 10m);
        var item2 = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Item 2", 1, 100m, 10m);

        var order = new Order(
            id: orderId,
            source: OrderSource.Waiter,
            orderNumber: "ORD-SPLIT-ERR",
            items: new[] { item1, item2 });

        // Duplicate item assignment across partitions
        var duplicatePartitions = new[]
        {
            (Guid.NewGuid(), "BILL-1", (IReadOnlyList<Guid>)new[] { item1.Id }),
            (Guid.NewGuid(), "BILL-2", (IReadOnlyList<Guid>)new[] { item1.Id })
        };
        Assert.Throws<InvalidOperationException>(() => BillSourceOperations.CreateSplitBills(order, duplicatePartitions));

        // Missing item partition
        var missingPartitions = new[]
        {
            (Guid.NewGuid(), "BILL-1", (IReadOnlyList<Guid>)new[] { item1.Id }),
            (Guid.NewGuid(), "BILL-2", (IReadOnlyList<Guid>)new[] { Guid.NewGuid() }) // unknown item
        };
        Assert.Throws<InvalidOperationException>(() => BillSourceOperations.CreateSplitBills(order, missingPartitions));
    }
}
