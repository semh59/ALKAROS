using Xunit;

namespace ALKAROS.Payments.PaymentAggregate.Tests;

/// <summary>
/// Pure domain tests for the Payment aggregate — no database. Covers the
/// canonical transition matrix (V0-DOM-001) and the requested/tendered/
/// approved/change money invariants (V0-DOM-004, V0-CMP-002).
/// </summary>
public sealed class PaymentDomainTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public void ConstructorRejectsEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new Payment(Guid.Empty, NewId(), 100m));
    }

    [Fact]
    public void ConstructorRejectsEmptyBillId()
    {
        Assert.Throws<ArgumentException>(() => new Payment(NewId(), Guid.Empty, 100m));
    }

    [Fact]
    public void ConstructorRejectsEmptyCurrencyCode()
    {
        Assert.Throws<ArgumentException>(() => new Payment(NewId(), NewId(), 100m, currencyCode: " "));
    }

    [Fact]
    public void ConstructorRejectsZeroRequestedAmount()
    {
        Assert.Throws<InvalidPaymentAmountException>(() => new Payment(NewId(), NewId(), 0m));
    }

    [Fact]
    public void ConstructorRejectsNegativeRequestedAmount()
    {
        Assert.Throws<InvalidPaymentAmountException>(() => new Payment(NewId(), NewId(), -10m));
    }

    [Fact]
    public void ConstructorRejectsTenderedAmountWhileStillInitiated()
    {
        Assert.Throws<InvalidPaymentAmountException>(
            () => new Payment(NewId(), NewId(), 100m, tenderedAmount: 100m, status: PaymentStatus.Initiated));
    }

    [Fact]
    public void ConstructorRejectsPendingWithoutATenderedAmount()
    {
        Assert.Throws<InvalidPaymentAmountException>(
            () => new Payment(NewId(), NewId(), 100m, status: PaymentStatus.Pending));
    }

    [Fact]
    public void ConstructorRejectsApprovedAmountGreaterThanTendered()
    {
        Assert.Throws<InvalidPaymentAmountException>(() => new Payment(
            NewId(), NewId(), 80m,
            tenderedAmount: 100m, approvedAmount: 120m, changeAmount: -20m,
            status: PaymentStatus.Approved));
    }

    [Fact]
    public void ConstructorRejectsChangeAmountThatDoesNotReconcile()
    {
        Assert.Throws<InvalidPaymentAmountException>(() => new Payment(
            NewId(), NewId(), 80m,
            tenderedAmount: 100m, approvedAmount: 80m, changeAmount: 5m,
            status: PaymentStatus.Approved));
    }

    [Fact]
    public void ConstructorRejectsChangeAmountBeforeApproval()
    {
        Assert.Throws<InvalidPaymentAmountException>(() => new Payment(
            NewId(), NewId(), 80m,
            tenderedAmount: 100m, changeAmount: 20m,
            status: PaymentStatus.Pending));
    }

    [Fact]
    public void ConstructorAcceptsAValidApprovedPaymentWithOverpaymentChange()
    {
        var payment = new Payment(
            NewId(), NewId(), requestedAmount: 80m,
            tenderedAmount: 100m, approvedAmount: 80m, changeAmount: 20m,
            status: PaymentStatus.Approved);

        Assert.Equal(80m, payment.RequestedAmount);
        Assert.Equal(100m, payment.TenderedAmount);
        Assert.Equal(80m, payment.ApprovedAmount);
        Assert.Equal(20m, payment.ChangeAmount);
    }

    [Fact]
    public void TenderRejectsZeroAmount()
    {
        var payment = new Payment(NewId(), NewId(), 80m);
        Assert.Throws<InvalidPaymentAmountException>(() => payment.Tender(0m));
    }

    [Fact]
    public void TenderRejectsNegativeAmount()
    {
        var payment = new Payment(NewId(), NewId(), 80m);
        Assert.Throws<InvalidPaymentAmountException>(() => payment.Tender(-5m));
    }

    [Fact]
    public void TenderTransitionsFromInitiatedToPendingAndReturnsANewInstance()
    {
        var payment = new Payment(NewId(), NewId(), 80m);
        var tendered = payment.Tender(100m);

        Assert.Equal(PaymentStatus.Initiated, payment.Status);
        Assert.Null(payment.TenderedAmount);
        Assert.Equal(PaymentStatus.Pending, tendered.Status);
        Assert.Equal(100m, tendered.TenderedAmount);
        Assert.Single(tendered.History);
        Assert.Equal(PaymentStatus.Initiated, tendered.History[0].OldStatus);
        Assert.Equal(PaymentStatus.Pending, tendered.History[0].NewStatus);
    }

    [Fact]
    public void TenderFromAnythingOtherThanInitiatedIsRejected()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(100m);
        Assert.Throws<InvalidPaymentTransitionException>(() => payment.Tender(100m));
    }

    [Fact]
    public void ApproveComputesChangeAsTenderedMinusApproved()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(100m);
        var approved = payment.Approve(80m);

        Assert.Equal(PaymentStatus.Approved, approved.Status);
        Assert.Equal(80m, approved.ApprovedAmount);
        Assert.Equal(20m, approved.ChangeAmount);
    }

    [Fact]
    public void ApproveWithNoOverpaymentLeavesZeroChange()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(80m);
        var approved = payment.Approve(80m);

        Assert.Equal(0m, approved.ChangeAmount);
    }

    [Fact]
    public void ApproveMoreThanTenderedIsRejected()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(80m);
        Assert.Throws<InvalidPaymentAmountException>(() => payment.Approve(120m));
    }

    [Fact]
    public void ApproveBeforeTenderingIsRejected()
    {
        var payment = new Payment(NewId(), NewId(), 80m);
        Assert.Throws<InvalidPaymentTransitionException>(() => payment.Approve(80m));
    }

    [Fact]
    public void DeclineFromPendingSucceeds()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(80m);
        var declined = payment.Decline("Kart reddedildi");

        Assert.Equal(PaymentStatus.Declined, declined.Status);
    }

    [Fact]
    public void DeclineFromApprovedIsRejected()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(80m).Approve(80m);
        Assert.Throws<InvalidPaymentTransitionException>(() => payment.Decline());
    }

    [Fact]
    public void CancelFromPendingSucceeds()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(80m);
        var cancelled = payment.Cancel();

        Assert.Equal(PaymentStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void CancelFromApprovedIsRejected()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(80m).Approve(80m);
        Assert.Throws<InvalidPaymentTransitionException>(() => payment.Cancel());
    }

    [Fact]
    public void MarkUnknownFromPendingSucceeds()
    {
        var payment = new Payment(NewId(), NewId(), 80m).Tender(80m);
        var unknown = payment.MarkUnknown("Provider timeout");

        Assert.Equal(PaymentStatus.Unknown, unknown.Status);
    }

    [Fact]
    public void MarkUnknownBeforeTenderingIsRejected()
    {
        var payment = new Payment(NewId(), NewId(), 80m);
        Assert.Throws<InvalidPaymentTransitionException>(() => payment.MarkUnknown());
    }

    [Fact]
    public void TimeoutRecoveryFlowGoesThroughUnknownAndReconciliationBeforeApproval()
    {
        var payment = new Payment(NewId(), NewId(), 80m)
            .Tender(80m)
            .MarkUnknown("Provider timeout after 3 retries")
            .RequestReconciliation("Operator verified with terminal receipt");
        Assert.Equal(PaymentStatus.ReconciliationRequired, payment.Status);

        var approved = payment.Approve(80m);
        Assert.Equal(PaymentStatus.Approved, approved.Status);
        Assert.Equal(4, approved.History.Count);
    }

    [Fact]
    public void ReconciliationRequiredCanAlsoDeclineOrCancel()
    {
        var reconciling = new Payment(NewId(), NewId(), 80m)
            .Tender(80m)
            .MarkUnknown()
            .RequestReconciliation();

        Assert.Equal(PaymentStatus.Declined, reconciling.Decline().Status);
        Assert.Equal(PaymentStatus.Cancelled, reconciling.Cancel().Status);
    }

    [Fact]
    public void NoImplicitTimeoutOutcomeUnknownCannotBeApprovedDirectly()
    {
        // CORR:C29: Unknown must route through ReconciliationRequired first —
        // it can never resolve straight to Approved/Declined/Cancelled.
        var unknown = new Payment(NewId(), NewId(), 80m).Tender(80m).MarkUnknown();

        Assert.False(unknown.CanTransitionTo(PaymentStatus.Approved));
        Assert.False(unknown.CanTransitionTo(PaymentStatus.Declined));
        Assert.False(unknown.CanTransitionTo(PaymentStatus.Cancelled));
        Assert.Throws<InvalidPaymentTransitionException>(() => unknown.Approve(80m));
    }

    [Fact]
    public void ApprovedIsATerminalStateForThisAggregateNoOpOrBackwardTransitionsAreForbidden()
    {
        var approved = new Payment(NewId(), NewId(), 80m).Tender(80m).Approve(80m);

        Assert.False(approved.CanTransitionTo(PaymentStatus.Approved));
        Assert.False(approved.CanTransitionTo(PaymentStatus.Pending));
        Assert.False(approved.CanTransitionTo(PaymentStatus.Initiated));
    }

    [Fact]
    public void RefundedAndPartiallyRefundedAreNeverReachableThroughThisAggregate()
    {
        // Out of scope for V13-PAY-001 (V0-DOM-003/V13-ALC-003/V13-ALC-004's
        // job) even though both are members of the canonical enum.
        var approved = new Payment(NewId(), NewId(), 80m).Tender(80m).Approve(80m);

        Assert.False(approved.CanTransitionTo(PaymentStatus.Refunded));
        Assert.False(approved.CanTransitionTo(PaymentStatus.PartiallyRefunded));
    }
}
