using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Billing.PaymentClosure.Tests;

/// <summary>
/// Pure (no database) tests for <see cref="BillPaymentClosureCalculator"/>.
/// </summary>
public sealed class BillPaymentClosureCalculatorTests
{
    [Fact]
    public void ComputeReportsNotFullyAllocatedAndZeroTotalsWithNoPayments()
    {
        var bill = MakeBill(payable: 100m);

        var projection = BillPaymentClosureCalculator.Compute(bill, payments: [], allocations: [], adjustments: []);

        projection.PaymentSatisfied.Should().BeFalse();
        projection.AllocatedTotal.Should().Be(0m);
        projection.PaidTotal.Should().Be(0m);
        projection.ChangeTotal.Should().Be(0m);
        projection.Blockers.Should().ContainSingle().Which.Should().Be(BillClosureBlocker.NotFullyAllocated);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Declined)]
    [InlineData(PaymentStatus.Cancelled)]
    [InlineData(PaymentStatus.Unknown)]
    public void ComputeNeverMarksSatisfiedForANonApprovedPayment(PaymentStatus status)
    {
        var bill = MakeBill(payable: 100m);
        var payment = MakeNonApprovedPayment(bill.Id, 100m, status);

        var projection = BillPaymentClosureCalculator.Compute(bill, [payment], allocations: [], adjustments: []);

        projection.PaymentSatisfied.Should().BeFalse();
        projection.AllocatedTotal.Should().Be(0m);
        projection.PaidTotal.Should().Be(0m);
    }

    [Fact]
    public void ComputeReportsHasPendingPaymentBlockerForPendingOrReconciliationRequired()
    {
        var bill = MakeBill(payable: 100m);
        var pending = MakeNonApprovedPayment(bill.Id, 100m, PaymentStatus.Pending);

        var projection = BillPaymentClosureCalculator.Compute(bill, [pending], allocations: [], adjustments: []);

        projection.Blockers.Should().Contain(BillClosureBlocker.HasPendingPayment);
    }

    [Fact]
    public void ComputeReportsHasUnknownPaymentBlockerForAnUnresolvedProviderTimeout()
    {
        var bill = MakeBill(payable: 100m);
        var unknown = MakeNonApprovedPayment(bill.Id, 100m, PaymentStatus.Unknown);

        var projection = BillPaymentClosureCalculator.Compute(bill, [unknown], allocations: [], adjustments: []);

        projection.Blockers.Should().Contain(BillClosureBlocker.HasUnknownPayment);
    }

    [Fact]
    public void ComputeMarksSatisfiedWhenAnApprovedPaymentsAllocationExactlyCoversThePayable()
    {
        var bill = MakeBill(payable: 80m);
        var payment = MakeApprovedPayment(bill.Id, requested: 80m, tendered: 100m, approved: 80m);
        var allocation = MakeAllocation(payment, bill, amount: 80m);

        var projection = BillPaymentClosureCalculator.Compute(bill, [payment], [allocation], adjustments: []);

        projection.PaymentSatisfied.Should().BeTrue();
        projection.AllocatedTotal.Should().Be(80m);
        projection.PaidTotal.Should().Be(80m);
        projection.ChangeTotal.Should().Be(20m);
        projection.Blockers.Should().BeEmpty();
    }

    [Fact]
    public void ComputeReportsAPartialAllocationAsNotYetSatisfied()
    {
        var bill = MakeBill(payable: 100m);
        var payment = MakeApprovedPayment(bill.Id, requested: 100m, tendered: 40m, approved: 40m);
        var allocation = MakeAllocation(payment, bill, amount: 40m);

        var projection = BillPaymentClosureCalculator.Compute(bill, [payment], [allocation], adjustments: []);

        projection.PaymentSatisfied.Should().BeFalse();
        projection.AllocatedTotal.Should().Be(40m);
        projection.Blockers.Should().ContainSingle().Which.Should().Be(BillClosureBlocker.NotFullyAllocated);
    }

    [Fact]
    public void ComputeExcludesAnAllocationWhosePaymentIsNotApproved()
    {
        // Defensive cross-check (this task's own Goal: "authoritative Payment
        // kayıtlarından"): an allocation tied to a payment that is not (or
        // no longer) Approved must never count, regardless of what the
        // allocation row itself says.
        var bill = MakeBill(payable: 80m);
        var payment = MakeNonApprovedPayment(bill.Id, 80m, PaymentStatus.Pending);
        var allocation = new PaymentAllocation(Guid.NewGuid(), payment.Id, bill.Id, 80m, "TRY", "key-orphan");

        var projection = BillPaymentClosureCalculator.Compute(bill, [payment], [allocation], adjustments: []);

        projection.AllocatedTotal.Should().Be(0m);
        projection.PaymentSatisfied.Should().BeFalse();
    }

    /// <summary>
    /// V1-RMD-298 (independent 2026-09-26 audit, finding K1): a bill discounted below its original payable
    /// amount must be able to close on the discounted (adjusted) total, not the ORIGINAL, never-updated
    /// <see cref="Bill.PayableAmount"/> - <c>billing.bill_adjustments</c> never mutates that field
    /// (V0-DOM-004), so this is the only place the discount actually takes effect for closure purposes.
    /// </summary>
    [Fact]
    public void ComputeMarksSatisfiedOnTheDiscountAdjustedTotalNotTheOriginalPayable()
    {
        var bill = MakeBill(payable: 100m);
        var discount = BillAdjustment.CreateDiscountAmount(
            Guid.NewGuid(), bill.Id, discountAmount: 20m, taxRate: 0m, reason: "Test discount", authorizedBy: Guid.NewGuid());
        var payment = MakeApprovedPayment(bill.Id, requested: 80m, tendered: 80m, approved: 80m);
        var allocation = MakeAllocation(payment, bill, amount: 80m);

        var projection = BillPaymentClosureCalculator.Compute(bill, [payment], [allocation], [discount]);

        projection.PayableAmount.Should().Be(80m);
        projection.PaymentSatisfied.Should().BeTrue();
        projection.Blockers.Should().BeEmpty();
    }

    private static Bill MakeBill(decimal payable)
    {
        var billId = Guid.NewGuid();
        var billItem = new BillItem(
            Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), "Test Item",
            quantity: 1, unitPrice: payable, taxRate: 0);
        return new Bill(billId, "BILL-" + Guid.NewGuid().ToString("N")[..8], items: [billItem], currencyCode: "TRY");
    }

    private static Payment MakeApprovedPayment(Guid billId, decimal requested, decimal tendered, decimal approved)
        => new Payment(Guid.NewGuid(), billId, requested, currencyCode: "TRY")
            .Tender(tendered)
            .Approve(approved);

    private static Payment MakeNonApprovedPayment(Guid billId, decimal requested, PaymentStatus status)
    {
        var payment = new Payment(Guid.NewGuid(), billId, requested, currencyCode: "TRY");
        return status switch
        {
            PaymentStatus.Pending => payment.Tender(requested),
            PaymentStatus.Declined => payment.Tender(requested).Decline(),
            PaymentStatus.Cancelled => payment.Tender(requested).Cancel(),
            PaymentStatus.Unknown => payment.Tender(requested).MarkUnknown(),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported status for this helper."),
        };
    }

    private static PaymentAllocation MakeAllocation(Payment payment, Bill bill, decimal amount)
        => new(Guid.NewGuid(), payment.Id, bill.Id, amount, "TRY", "key-" + Guid.NewGuid().ToString("N")[..8]);
}
