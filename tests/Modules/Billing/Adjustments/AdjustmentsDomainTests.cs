using ALKAROS.Billing.BillFoundation;
using Xunit;

namespace ALKAROS.Billing.Adjustments.Tests;

/// <summary>
/// Domain unit tests for BillAdjustment and AdjustmentCalculator.
/// Verifies invariant enforcement, mandatory reason and authorization, percentage/fixed calculation, and non-negativity.
/// </summary>
public sealed class AdjustmentsDomainTests
{
    [Fact]
    public void BillAdjustmentConstructorValidatesInvariants()
    {
        var id = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // Empty ID
        Assert.Throws<ArgumentException>(() =>
            new BillAdjustment(Guid.Empty, billId, AdjustmentType.DiscountAmount, AdjustmentCalculationType.FixedAmount, 10m, 9.09m, 10m, "Reason", managerId));

        // Empty Bill ID
        Assert.Throws<ArgumentException>(() =>
            new BillAdjustment(id, Guid.Empty, AdjustmentType.DiscountAmount, AdjustmentCalculationType.FixedAmount, 10m, 9.09m, 10m, "Reason", managerId));

        // Non-positive amount
        Assert.Throws<ArgumentException>(() =>
            new BillAdjustment(id, billId, AdjustmentType.DiscountAmount, AdjustmentCalculationType.FixedAmount, 0m, 0m, 0m, "Reason", managerId));
        Assert.Throws<ArgumentException>(() =>
            new BillAdjustment(id, billId, AdjustmentType.DiscountAmount, AdjustmentCalculationType.FixedAmount, -5m, -4.5m, -5m, "Reason", managerId));

        // Empty reason (V0-DOM-006)
        Assert.Throws<ArgumentException>(() =>
            new BillAdjustment(id, billId, AdjustmentType.DiscountAmount, AdjustmentCalculationType.FixedAmount, 10m, 9.09m, 10m, "", managerId));

        // Empty authorizedBy (V0-DOM-006)
        Assert.Throws<ArgumentException>(() =>
            new BillAdjustment(id, billId, AdjustmentType.DiscountAmount, AdjustmentCalculationType.FixedAmount, 10m, 9.09m, 10m, "Reason", Guid.Empty));

        // Invalid rate
        Assert.Throws<ArgumentException>(() =>
            new BillAdjustment(id, billId, AdjustmentType.DiscountPercentage, AdjustmentCalculationType.Percentage, 10m, 9.09m, 10m, "Reason", managerId, rate: 0m));
        Assert.Throws<ArgumentException>(() =>
            new BillAdjustment(id, billId, AdjustmentType.DiscountPercentage, AdjustmentCalculationType.Percentage, 10m, 9.09m, 10m, "Reason", managerId, rate: 105m));
    }

    [Fact]
    public void CreateDiscountPercentageCalculatesNetAndTaxProperly()
    {
        var billId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // 10% discount on 200.00 TL base with 10% VAT
        var discount = BillAdjustment.CreateDiscountPercentage(
            id: Guid.NewGuid(),
            billId: billId,
            rate: 10m,
            baseGrossAmount: 200m,
            taxRate: 10m,
            reason: "VIP Customer",
            authorizedBy: managerId);

        Assert.Equal(AdjustmentType.DiscountPercentage, discount.AdjustmentType);
        Assert.Equal(AdjustmentCalculationType.Percentage, discount.CalculationType);
        Assert.Equal(10m, discount.Rate);
        Assert.Equal(20.00m, discount.GrossAmount);
        Assert.Equal(18.18m, discount.NetAmount);
        Assert.Equal(1.82m, discount.TaxAmount);
        Assert.True(discount.IsDeduction);
        Assert.Equal("VIP Customer", discount.Reason);
        Assert.Equal(managerId, discount.AuthorizedBy);
    }

    [Fact]
    public void CreateDiscountAmountCalculatesNetAndTaxProperly()
    {
        var billId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // Fixed 50.00 TL discount with 10% VAT
        var discount = BillAdjustment.CreateDiscountAmount(
            id: Guid.NewGuid(),
            billId: billId,
            discountAmount: 50m,
            taxRate: 10m,
            reason: "Birthday Discount",
            authorizedBy: managerId);

        Assert.Equal(AdjustmentType.DiscountAmount, discount.AdjustmentType);
        Assert.Equal(50.00m, discount.GrossAmount);
        Assert.Equal(45.45m, discount.NetAmount);
        Assert.Equal(4.55m, discount.TaxAmount);
        Assert.True(discount.IsDeduction);
    }

    [Fact]
    public void AdjustmentTaxIsRoundedFirstLikeEveryOtherLine()
    {
        // 10.05 at 20%: the contained tax is 1.675 -> 1.68 (half-up), net is the rest, 8.37 (net-first would give 8.38 / 1.67).
        var discount = BillAdjustment.CreateDiscountAmount(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            discountAmount: 10.05m,
            taxRate: 20m,
            reason: "Rounding",
            authorizedBy: Guid.NewGuid());

        Assert.Equal(1.68m, discount.TaxAmount);
        Assert.Equal(8.37m, discount.NetAmount);
        Assert.Equal(10.05m, discount.GrossAmount);
    }

    [Fact]
    public void CreateServiceFeeCalculatesGrossNetTaxAndSetsAddition()
    {
        var billId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // 30.00 TL Service Fee with 10% VAT
        var fee = BillAdjustment.CreateServiceFee(
            id: Guid.NewGuid(),
            billId: billId,
            amount: 30m,
            taxRate: 10m,
            reason: "Large Group Service Fee",
            authorizedBy: managerId);

        Assert.Equal(AdjustmentType.ServiceFee, fee.AdjustmentType);
        Assert.Equal(30.00m, fee.GrossAmount);
        Assert.Equal(27.27m, fee.NetAmount);
        Assert.Equal(2.73m, fee.TaxAmount);
        Assert.False(fee.IsDeduction);
    }

    [Fact]
    public void CreateTipCalculatesZeroTaxPassThrough()
    {
        var billId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // 25.00 TL Tip (0% VAT per V0-CMP-004)
        var tip = BillAdjustment.CreateTip(
            id: Guid.NewGuid(),
            billId: billId,
            amount: 25m,
            reason: "Waiter Tip",
            authorizedBy: managerId);

        Assert.Equal(AdjustmentType.Tip, tip.AdjustmentType);
        Assert.Equal(25.00m, tip.GrossAmount);
        Assert.Equal(25.00m, tip.NetAmount);
        Assert.Equal(0.00m, tip.TaxAmount);
        Assert.Equal(0.00m, tip.TaxRate);
        Assert.False(tip.IsDeduction);
    }

    [Fact]
    public void AdjustmentCalculatorComputesTotalsAccurately()
    {
        var billId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // Bill with 1 item: 500.00 TL gross (Net 454.55, Tax 45.45)
        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Steak Menu",
            quantity: 1,
            unitPrice: 454.55m,
            taxRate: 10m,
            taxAmount: 45.45m,
            netAmount: 454.55m,
            grossAmount: 500.00m);

        var bill = new Bill(billId, "BILL-ADJ-01", new[] { item });

        var discount = BillAdjustment.CreateDiscountAmount(
            Guid.NewGuid(), billId, 50.00m, 10m, "Discount", managerId);
        var serviceFee = BillAdjustment.CreateServiceFee(
            Guid.NewGuid(), billId, 40.00m, 10m, "Service Fee", managerId);
        var tip = BillAdjustment.CreateTip(
            Guid.NewGuid(), billId, 30.00m, "Tip", managerId);

        var summary = AdjustmentCalculator.Calculate(bill, new[] { discount, serviceFee, tip });

        Assert.Equal(billId, summary.BillId);
        Assert.Equal(500.00m, summary.OriginalPayableAmount);
        Assert.Equal(50.00m, summary.TotalDiscounts);
        Assert.Equal(40.00m, summary.TotalFees);
        Assert.Equal(30.00m, summary.TotalTips);

        // Adjusted payable = 500 - 50 + 40 + 30 = 520.00 TL
        Assert.Equal(520.00m, summary.AdjustedPayableAmount);
        Assert.Equal(50.00m, summary.AdjustedDiscountTotal);
    }

    [Fact]
    public void AdjustmentCalculatorRejectsExcessiveDiscount()
    {
        var billId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var item = new BillItem(
            id: Guid.NewGuid(),
            billId: billId,
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productNameSnapshot: "Item",
            quantity: 1,
            unitPrice: 100m,
            taxRate: 10m); // Gross = 110.00

        var bill = new Bill(billId, "BILL-ADJ-02", new[] { item });

        // Discount of 150.00 exceeds bill payable 110.00
        var discount = BillAdjustment.CreateDiscountAmount(
            Guid.NewGuid(), billId, 150.00m, 10m, "Excessive Discount", managerId);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AdjustmentCalculator.Calculate(bill, new[] { discount }));
        Assert.Contains("cannot exceed bill base payable", ex.Message);
    }

    [Fact]
    public void AdjustmentCalculatorRejectsCrossBillAdjustment()
    {
        var bill = new Bill(Guid.NewGuid(), "BILL-ADJ-03");

        var foreignAdjustment = BillAdjustment.CreateTip(
            Guid.NewGuid(), Guid.NewGuid(), 20m, "Tip", Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            AdjustmentCalculator.Calculate(bill, new[] { foreignAdjustment }));
    }

    [Fact]
    public void ADiscountOnAMixedRateBillTakesOffTheTaxInTheBillsOwnProportion()
    {
        var billId = Guid.NewGuid();
        BillItem Item(decimal price, decimal rate) => new(
            Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), "Item", 1, price, rate);
        var bill = new Bill(billId, "BILL-MIX", new[] { Item(1000m, 1m), Item(1000m, 20m) });

        var discount = BillAdjustment.CreateDiscountPercentage(
            Guid.NewGuid(), billId, 50m, bill.PayableAmount, bill.EffectiveTaxRatePercent,
            "Promo", Guid.NewGuid());

        // 50% of 2000 = 1000; its tax is half of the bill's 176.57 tax. A rate rounded to 2 decimals gave 88.26.
        Assert.Equal(1000m, discount.GrossAmount);
        Assert.Equal(BillMath.RoundCurrency(1000m * bill.TaxTotal / bill.PayableAmount), discount.TaxAmount);
        Assert.Equal(88.29m, discount.TaxAmount);
        Assert.Equal(discount.GrossAmount, discount.NetAmount + discount.TaxAmount);
    }

    [Fact]
    public void TheTaxBreakdownAddsUpToTheAdjustedTotalsPerRateWithTipsLeftOut()
    {
        var billId = Guid.NewGuid();
        BillItem Item(decimal price, decimal rate) => new(
            Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), "Item", 1, price, rate);
        var bill = new Bill(billId, "BILL-BRK", new[] { Item(1000m, 1m), Item(1000m, 20m), Item(333.33m, 10m) });
        var adjustments = new[]
        {
            BillAdjustment.CreateDiscountPercentage(
                Guid.NewGuid(), billId, 33m, bill.PayableAmount, bill.EffectiveTaxRatePercent, "Promo", Guid.NewGuid()),
            BillAdjustment.CreateTip(Guid.NewGuid(), billId, 50m, "Tip", Guid.NewGuid()),
        };
        var summary = AdjustmentCalculator.Calculate(bill, adjustments);

        var lines = TaxBreakdown.Build(bill, summary);

        Assert.Equal([1m, 10m, 20m], lines.Select(line => line.RatePercent));
        Assert.Equal(summary.AdjustedPayableAmount - summary.TotalTips, lines.Sum(line => line.Gross));
        Assert.Equal(summary.AdjustedTaxTotal, lines.Sum(line => line.Tax));
        Assert.All(lines, line => Assert.Equal(line.Gross, line.Net + line.Tax));
    }

    private static Bill BillOfOneItem(Guid billId, decimal unitPrice, decimal lineDiscount = 0m)
    {
        var item = new BillItem(
            id: Guid.NewGuid(), billId: billId, orderItemId: Guid.NewGuid(), productId: Guid.NewGuid(),
            productNameSnapshot: "Item", quantity: 1, unitPrice: unitPrice, taxRate: 10m, discountAmount: lineDiscount);
        return new Bill(billId, "BILL-ADJ-T", new[] { item });
    }

    [Fact]
    public void TheAdjustedTotalsAddTheAdjustmentsToWhatTheBillAlreadyCarries()
    {
        var billId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var bill = BillOfOneItem(billId, 200m, lineDiscount: 20m);
        var discount = BillAdjustment.CreateDiscountAmount(Guid.NewGuid(), billId, 55m, 10m, "Discount", managerId);
        var fee = BillAdjustment.CreateServiceFee(Guid.NewGuid(), billId, 22m, 10m, "Fee", managerId);
        Assert.True(bill.DiscountTotal > 0m && bill.TaxTotal > 0m && discount.TaxAmount > 0m && fee.TaxAmount > 0m);

        var summary = AdjustmentCalculator.Calculate(bill, new[] { discount, fee });

        Assert.Equal(bill.DiscountTotal + 55m, summary.AdjustedDiscountTotal);
        Assert.Equal(bill.TaxTotal - discount.TaxAmount + fee.TaxAmount, summary.AdjustedTaxTotal);
    }

    [Fact]
    public void ADiscountThatEqualsThePayableIsAllowedAndTheAdjustedTaxNeverGoesBelowZero()
    {
        var billId = Guid.NewGuid();
        var bill = BillOfOneItem(billId, 100m);
        // 110.00 payable; a 20% tax rate on the discount takes more tax out (18.33) than the bill carries (10.00).
        var discount = BillAdjustment.CreateDiscountAmount(Guid.NewGuid(), billId, bill.PayableAmount, 20m, "Full", Guid.NewGuid());
        Assert.True(discount.TaxAmount > bill.TaxTotal);

        var summary = AdjustmentCalculator.Calculate(bill, new[] { discount });

        Assert.Equal(0m, summary.AdjustedPayableAmount);
        Assert.Equal(0m, summary.AdjustedTaxTotal);
    }

    [Fact]
    public void ADiscountOfExactlyOneHundredPercentIsAccepted()
    {
        var managerId = Guid.NewGuid();

        var created = BillAdjustment.CreateDiscountPercentage(Guid.NewGuid(), Guid.NewGuid(), 100m, 80m, 10m, "Comp", managerId);
        var constructed = new BillAdjustment(
            Guid.NewGuid(), Guid.NewGuid(), AdjustmentType.DiscountPercentage, AdjustmentCalculationType.Percentage,
            10m, 9.09m, 10m, "Reason", managerId, rate: 100m);

        Assert.Equal(80m, created.GrossAmount);
        Assert.Equal(100m, constructed.Rate);
    }
}
