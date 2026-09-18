using ALKAROS.Payments.Allocations.Persistence;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Payments.Allocations.RefundIntents.Tests;

/// <summary>
/// Pure (no database) tests for <see cref="RefundIntentFactory"/> and
/// <see cref="RefundIntent"/>'s own constructor/transition invariants.
/// </summary>
public sealed class RefundIntentFactoryTests
{
    [Fact]
    public void CreateRejectsAZeroOrNegativeAmount()
    {
        var allocation = MakeAllocation(80m);

        var actZero = () => RefundIntentFactory.Create(allocation, allocation.PaymentId, 0m, alreadyPendingForAllocation: 0m, "key-1");
        actZero.Should().Throw<InvalidRefundIntentAmountException>();

        var actNegative = () => RefundIntentFactory.Create(allocation, allocation.PaymentId, -5m, alreadyPendingForAllocation: 0m, "key-2");
        actNegative.Should().Throw<InvalidRefundIntentAmountException>();
    }

    [Fact]
    public void CreateRejectsAnIntentTargetingAnAllocationBelongingToADifferentPayment()
    {
        var allocation = MakeAllocation(80m);
        var otherPaymentId = Guid.NewGuid();

        var act = () => RefundIntentFactory.Create(allocation, otherPaymentId, 10m, alreadyPendingForAllocation: 0m, "key-3");

        act.Should().Throw<RefundIntentCrossPaymentException>()
            .Where(ex => ex.PaymentId == otherPaymentId && ex.AllocationPaymentId == allocation.PaymentId);
    }

    [Fact]
    public void CreateAllowsARequestExactlyMatchingTheRemainingEligibility()
    {
        // The task's own Acceptance example: payment 100, 20 already
        // requested (Pending), 80 more is still exactly eligible.
        var allocation = MakeAllocation(100m);

        var intent = RefundIntentFactory.Create(allocation, allocation.PaymentId, 80m, alreadyPendingForAllocation: 20m, "key-4");

        intent.RequestedAmount.Should().Be(80m);
        intent.Status.Should().Be(RefundIntentStatus.Pending);
        intent.PaymentAllocationId.Should().Be(allocation.Id);
    }

    [Fact]
    public void CreateRejectsARequestThatWouldExceedTheRemainingEligibility()
    {
        // 100 payment, 20 already requested, this request of 81 would push
        // the cumulative total to 101 > 100 - the task's own "100 üzeri
        // talep ... reddedilir" acceptance example.
        var allocation = MakeAllocation(100m);

        var act = () => RefundIntentFactory.Create(allocation, allocation.PaymentId, 81m, alreadyPendingForAllocation: 20m, "key-5");

        act.Should().Throw<RefundIntentOverLimitException>()
            .Where(ex => ex.PaymentAllocationId == allocation.Id && ex.RemainingEligible == 80m && ex.RequestedAmount == 81m);
    }

    [Fact]
    public void RejectTransitionsAPendingIntentAndRecordsWhyAndWhen()
    {
        var allocation = MakeAllocation(100m);
        var intent = RefundIntentFactory.Create(allocation, allocation.PaymentId, 30m, alreadyPendingForAllocation: 0m, "key-6");

        var rejected = intent.Reject("Customer withdrew the request");

        rejected.Status.Should().Be(RefundIntentStatus.Rejected);
        rejected.RejectionReason.Should().Be("Customer withdrew the request");
        rejected.RejectedAt.Should().NotBeNull();
    }

    [Fact]
    public void RejectRefusesAnAlreadyRejectedIntent()
    {
        var allocation = MakeAllocation(100m);
        var intent = RefundIntentFactory.Create(allocation, allocation.PaymentId, 30m, alreadyPendingForAllocation: 0m, "key-7")
            .Reject("first rejection");

        var act = () => intent.Reject("second rejection");

        act.Should().Throw<InvalidRefundIntentTransitionException>();
    }

    private static PaymentAllocation MakeAllocation(decimal amount)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount, "TRY", "allocation-key-" + Guid.NewGuid().ToString("N")[..8]);
}
