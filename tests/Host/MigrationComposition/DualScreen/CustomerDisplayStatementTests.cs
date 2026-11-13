using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Host.DualScreen;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

public sealed class CustomerDisplayStatementTests
{
    private static readonly Guid Staff = Guid.NewGuid();

    private static CustomerDisplaySnapshotDto Snapshot(string state = "Active", bool editable = false) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4, state, editable, "POS-4",
        [new CustomerDisplayLineDto(Guid.NewGuid(), "Sipariş kalemi", 1, 10, 10, 0)],
        10m, 0m, 1m, 10m, "TRY", DateTimeOffset.UtcNow, "Siparişiniz alındı.");

    private static BillItem Sale(Guid billId, string name, decimal quantity, decimal unitPrice, decimal discount = 0m)
        => new(Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), name, quantity, unitPrice, 10m,
            discountAmount: discount, grossAmount: quantity * unitPrice - discount);

    private static BillItem Comp(Guid billId, string name, decimal price)
        => new(Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), name, 1, price, 10m,
            discountAmount: price, grossAmount: 0m, lineType: BillLineType.Complimentary);

    private static Bill Bill(Guid billId, BillState status, params Func<Guid, BillItem>[] items)
        => new(billId, "BILL-" + billId.ToString("N")[..8], items.Select(item => item(billId)).ToList(), status: status);

    private static BillAdjustment Discount(Guid billId, decimal amount)
        => BillAdjustment.CreateDiscountAmount(Guid.NewGuid(), billId, amount, 10m, "PromotionalOffer", Staff);

    private static BillAdjustment Fee(Guid billId, decimal amount)
        => BillAdjustment.CreateServiceFee(Guid.NewGuid(), billId, amount, 10m, "Test", Staff);

    private static BillAdjustment Tip(Guid billId, decimal amount)
        => BillAdjustment.CreateTip(Guid.NewGuid(), billId, amount, "VoluntaryTip", Staff);

    private static BillStatement Statement(Bill bill, decimal allocated, params BillAdjustment[] adjustments)
    {
        var payable = AdjustmentCalculator.Calculate(bill, adjustments).AdjustedPayableAmount;
        return new BillStatement(
            bill, adjustments,
            new BillPaymentClosureProjection(bill.Id, "TRY", payable, allocated, allocated, 0m, allocated >= payable, []));
    }

    private static void AssertBalanced(CustomerDisplaySnapshotDto snapshot)
    {
        Assert.Equal(snapshot.Lines.Sum(line => line.LineTotal), snapshot.Subtotal);
        Assert.Equal(snapshot.Subtotal - snapshot.DiscountTotal + snapshot.FeeTotal + snapshot.TipTotal, snapshot.Total);
        Assert.Equal(snapshot.Total, snapshot.PaidTotal + snapshot.RemainingTotal);
    }

    [Fact]
    public void ABillWithoutAdjustmentsShowsItsOwnLinesAndTotals()
    {
        var id = Guid.NewGuid();
        var bill = Bill(id, BillState.Open, b => Sale(b, "Adana", 2, 130m), b => Sale(b, "Ayran", 1, 25m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(bill, 0m)]);

        Assert.Equal(["Adana", "Ayran"], result.Lines.Select(line => line.Name));
        Assert.Equal(285m, result.Subtotal);
        Assert.Equal(0m, result.DiscountTotal);
        Assert.Equal(0m, result.TipTotal);
        Assert.Equal(285m, result.Total);
        Assert.Equal(285m, result.RemainingTotal);
        Assert.Equal("Siparişiniz alındı.", result.Message);
        AssertBalanced(result);
    }

    [Fact]
    public void ABillPaidInFullKeepsTheOrderMessageNotThePartialOne()
    {
        var bill = Bill(Guid.NewGuid(), BillState.Paid, b => Sale(b, "Adana", 1, 100m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(bill, 100m)]);

        Assert.Equal("Siparişiniz alındı.", result.Message);
        Assert.Equal(0m, result.RemainingTotal);
        AssertBalanced(result);
    }

    [Fact]
    public void AFeeAdjustmentIsShownAsItsOwnFigureSoTheStatementStillAddsUp()
    {
        var id = Guid.NewGuid();
        var bill = Bill(id, BillState.Open, b => Sale(b, "Adana", 1, 100m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(bill, 0m, Fee(id, 12m))]);

        Assert.Equal(12m, result.FeeTotal);
        Assert.Equal(112m, result.Total);
        AssertBalanced(result);
    }

    [Fact]
    public void ADiscountedLineKeepsItsFullPriceAndShowsItsOwnDiscount()
    {
        var id = Guid.NewGuid();
        var bill = Bill(id, BillState.Open, b => Sale(b, "Adana", 2, 130m, discount: 26m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(bill, 0m)]);

        var line = Assert.Single(result.Lines);
        Assert.Equal(260m, line.LineTotal);
        Assert.Equal(26m, line.DiscountAmount);
        Assert.Equal(26m, result.DiscountTotal);
        Assert.Equal(234m, result.Total);
        AssertBalanced(result);
    }

    [Fact]
    public void AComplimentaryLineIsShownAtItsFullPriceAsFullyDiscounted()
    {
        var id = Guid.NewGuid();
        var bill = Bill(id, BillState.Open, b => Sale(b, "Adana", 1, 100m), b => Comp(b, "Baklava", 40m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(bill, 0m)]);

        var comp = result.Lines.Single(line => line.Name == "Baklava");
        Assert.Equal(40m, comp.LineTotal);
        Assert.Equal(40m, comp.DiscountAmount);
        Assert.Equal(140m, result.Subtotal);
        Assert.Equal(100m, result.Total);
        AssertBalanced(result);
    }

    [Fact]
    public void ABillDiscountAndATipAreBothShownAndTheFiguresAddUp()
    {
        var id = Guid.NewGuid();
        var bill = Bill(id, BillState.Open, b => Sale(b, "Adana", 3, 100m, discount: 20m), b => Sale(b, "Ayran", 2, 25m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(bill, 0m, Discount(id, 30m), Tip(id, 15m))]);

        Assert.Equal(350m, result.Subtotal);
        Assert.Equal(50m, result.DiscountTotal);
        Assert.Equal(15m, result.TipTotal);
        Assert.Equal(315m, result.Total);
        Assert.Equal(315m, result.RemainingTotal);
        AssertBalanced(result);
    }

    [Fact]
    public void APartialPaymentLeavesTheStatementBalancedAndSaysSo()
    {
        var id = Guid.NewGuid();
        var bill = Bill(id, BillState.PartiallyPaid, b => Sale(b, "Adana", 2, 130m, discount: 10m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(bill, 100m, Discount(id, 20m), Tip(id, 10m))]);

        Assert.Equal(100m, result.PaidTotal);
        Assert.Equal(140m, result.RemainingTotal);
        Assert.Equal(CustomerDisplayPaymentProgress.PartiallyPaidMessage, result.Message);
        AssertBalanced(result);
    }

    [Fact]
    public void AnOverpaidBillNeverShowsANegativeRemainderOrHidesTheOtherBillsDebt()
    {
        var overpaid = Bill(Guid.NewGuid(), BillState.Open, b => Sale(b, "Adana", 1, 100m));
        var open = Bill(Guid.NewGuid(), BillState.Open, b => Sale(b, "Ayran", 1, 50m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(overpaid, 130m), Statement(open, 0m)]);

        Assert.Equal(50m, result.RemainingTotal);
        Assert.Equal(130m, result.PaidTotal);
    }

    [Fact]
    public void ATaxFollowsTheDiscountedBill()
    {
        var id = Guid.NewGuid();
        var bill = Bill(id, BillState.Open, b => Sale(b, "Adana", 1, 110m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(bill, 0m, Discount(id, 55m))]);

        Assert.Equal(5m, result.TaxTotal);
    }

    [Fact]
    public void ASettledBillOnACompletedOrderKeepsItsTotalsAndShowsEverythingPaid()
    {
        var id = Guid.NewGuid();
        var bill = Bill(id, BillState.Paid, b => Sale(b, "Adana", 2, 130m));
        var completed = Snapshot("Completed") with { Message = "Teşekkür ederiz." };

        var result = CustomerDisplayPaymentProgress.Apply(completed, [Statement(bill, 280m, Tip(id, 20m))]);

        Assert.Equal("Completed", result.State);
        Assert.Equal(280m, result.Total);
        Assert.Equal(280m, result.PaidTotal);
        Assert.Equal(0m, result.RemainingTotal);
        Assert.Equal("Teşekkür ederiz.", result.Message);
    }

    [Fact]
    public void ACompletedOrderKeepsItsThankYouEvenWithSomethingStillLeftToPay()
    {
        var bill = Bill(Guid.NewGuid(), BillState.PartiallyPaid, b => Sale(b, "Adana", 1, 100m));
        var completed = Snapshot("Completed") with { Message = "Teşekkür ederiz." };

        var result = CustomerDisplayPaymentProgress.Apply(completed, [Statement(bill, 40m)]);

        Assert.Equal("Teşekkür ederiz.", result.Message);
        Assert.Equal(60m, result.RemainingTotal);
    }

    [Fact]
    public void AnOrderWhoseBillsAreAllCancelledReturnsToIdleWithoutItsLinesOrAmounts()
    {
        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [], anyBillCancelled: true);

        Assert.Equal("Idle", result.State);
        Assert.Equal(CustomerDisplayPaymentProgress.BillCancelledMessage, result.Message);
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.Subtotal + result.DiscountTotal + result.TipTotal + result.TaxTotal + result.Total + result.PaidTotal);
        Assert.Null(result.RemainingTotal);
    }

    [Fact]
    public void ACancelledBillNextToALiveOneIsIgnored()
    {
        var live = Bill(Guid.NewGuid(), BillState.Open, b => Sale(b, "Adana", 1, 100m));

        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Statement(live, 0m)], anyBillCancelled: true);

        Assert.Equal("Active", result.State);
        Assert.Equal(["Adana"], result.Lines.Select(line => line.Name));
        Assert.Equal(100m, result.Total);
    }

    [Fact]
    public void ACancelledBillClearsTheDisplayEvenWhileTheOrderStillHasUnsentItems()
    {
        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(editable: true), [], anyBillCancelled: true);

        Assert.Equal("Idle", result.State);
        Assert.Equal(CustomerDisplayPaymentProgress.BillCancelledMessage, result.Message);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void AnOrderBeingEditedWithoutAnyCancelledBillIsLeftAlone()
    {
        var editing = Snapshot(editable: true);

        Assert.Equal(editing, CustomerDisplayPaymentProgress.Apply(editing, []));
    }

    [Fact]
    public void SeveralLiveBillsOfOneOrderAreSummedLineByLine()
    {
        var first = Bill(Guid.NewGuid(), BillState.Open, b => Sale(b, "Adana", 1, 100m));
        var second = Bill(Guid.NewGuid(), BillState.Open, b => Sale(b, "Ayran", 2, 25m));

        var result = CustomerDisplayPaymentProgress.Apply(
            Snapshot(),
            [
                Statement(first, 40m, Tip(first.Id, 10m), Fee(first.Id, 12m)),
                Statement(second, 30m, Tip(second.Id, 5m), Fee(second.Id, 3m)),
            ]);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(180m, result.Total);
        Assert.Equal(70m, result.PaidTotal);
        Assert.Equal(110m, result.RemainingTotal);
        Assert.Equal(15m, result.TipTotal);
        Assert.Equal(15m, result.FeeTotal);
        AssertBalanced(result);
    }

    [Fact]
    public void DiscountsAndTaxOfSeveralBillsAreSummed()
    {
        var first = Bill(Guid.NewGuid(), BillState.Open, b => Sale(b, "Adana", 1, 110m));
        var second = Bill(Guid.NewGuid(), BillState.Open, b => Sale(b, "Ayran", 1, 55m, discount: 5m));

        var result = CustomerDisplayPaymentProgress.Apply(
            Snapshot(), [Statement(first, 0m, Discount(first.Id, 11m)), Statement(second, 0m, Discount(second.Id, 5m))]);

        Assert.Equal(21m, result.DiscountTotal);
        Assert.Equal(144m, result.Total);
        Assert.Equal(13.10m, result.TaxTotal);
        AssertBalanced(result);
    }

    [Fact]
    public void ACompletedOrderIsNotClearedByACancelledBill()
    {
        var completed = Snapshot("Completed");

        Assert.Equal(completed, CustomerDisplayPaymentProgress.Apply(completed, [], anyBillCancelled: true));
    }

    [Fact]
    public void AnIdleSnapshotIsNeverTouched()
    {
        var snapshot = Snapshot("Idle");
        var bill = Bill(Guid.NewGuid(), BillState.Open, b => Sale(b, "Adana", 1, 100m));

        Assert.Equal(snapshot, CustomerDisplayPaymentProgress.Apply(snapshot, [Statement(bill, 0m)]));
    }

    [Fact]
    public void WithoutAnyBillTheSnapshotIsUnchanged()
    {
        var snapshot = Snapshot();

        Assert.Equal(snapshot, CustomerDisplayPaymentProgress.Apply(snapshot, []));
    }
}
