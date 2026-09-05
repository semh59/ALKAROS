using FluentAssertions;
using Xunit;

namespace ALKAROS.Purchasing.OrdersAndReceipts.Tests;

public sealed class ReceiptVariancePolicyTests
{
    [Fact]
    public void ExactDeliveryReturnsNoVarianceAndAutoAccepts()
    {
        var res = ReceiptVariancePolicy.Evaluate(
            orderedQuantity: 100m,
            deliveredQuantity: 100m,
            isManagerApproved: false,
            varianceReason: null);

        res.AcceptedQuantity.Should().Be(100m);
        res.RejectedQuantity.Should().Be(0m);
        res.VarianceQuantity.Should().Be(0m);
        res.RequiresManagerApproval.Should().BeFalse();
    }

    [Fact]
    public void ShortDeliveryWithoutReasonThrowsVarianceReasonRequiredException()
    {
        var act = () => ReceiptVariancePolicy.Evaluate(
            orderedQuantity: 100m,
            deliveredQuantity: 95m,
            isManagerApproved: false,
            varianceReason: null);

        act.Should().Throw<VarianceReasonRequiredException>()
            .WithMessage("*mandatory variance reason*");
    }

    [Fact]
    public void ShortDeliveryWithReasonAcceptsDeliveredQuantityWithoutApproval()
    {
        var res = ReceiptVariancePolicy.Evaluate(
            orderedQuantity: 100m,
            deliveredQuantity: 95m,
            isManagerApproved: false,
            varianceReason: "Supplier out of stock for remainder");

        res.AcceptedQuantity.Should().Be(95m);
        res.RejectedQuantity.Should().Be(0m);
        res.VarianceQuantity.Should().Be(-5m);
        res.RequiresManagerApproval.Should().BeFalse();
        res.VarianceReason.Should().Be("Supplier out of stock for remainder");
    }

    [Fact]
    public void WithinToleranceOverReceiptAutoAcceptsFullQuantity()
    {
        // 100 ordered, 103 delivered (3% excess <= 5% tolerance)
        var res = ReceiptVariancePolicy.Evaluate(
            orderedQuantity: 100m,
            deliveredQuantity: 103m,
            isManagerApproved: false,
            varianceReason: "Supplier packaging variation");

        res.AcceptedQuantity.Should().Be(103m);
        res.RejectedQuantity.Should().Be(0m);
        res.VarianceQuantity.Should().Be(3m);
        res.RequiresManagerApproval.Should().BeFalse();
        res.VarianceReason.Should().Be("Supplier packaging variation");
    }

    [Fact]
    public void AboveToleranceOverReceiptWithoutManagerApprovalCappedToOrderedAndRejectsExcess()
    {
        // 100 ordered, 108 delivered (8% excess > 5% tolerance), unapproved
        var res = ReceiptVariancePolicy.Evaluate(
            orderedQuantity: 100m,
            deliveredQuantity: 108m,
            isManagerApproved: false,
            varianceReason: "Excess crates delivered");

        res.AcceptedQuantity.Should().Be(100m);
        res.RejectedQuantity.Should().Be(8m);
        res.VarianceQuantity.Should().Be(8m);
        res.RequiresManagerApproval.Should().BeTrue();
        res.IsApproved.Should().BeFalse();
    }

    [Fact]
    public void AboveToleranceOverReceiptWithManagerApprovalAcceptsFullQuantity()
    {
        // 100 ordered, 108 delivered (8% excess > 5% tolerance), manager approved
        var res = ReceiptVariancePolicy.Evaluate(
            orderedQuantity: 100m,
            deliveredQuantity: 108m,
            isManagerApproved: true,
            varianceReason: "Manager approved extra crates for upcoming banquet");

        res.AcceptedQuantity.Should().Be(108m);
        res.RejectedQuantity.Should().Be(0m);
        res.VarianceQuantity.Should().Be(8m);
        res.RequiresManagerApproval.Should().BeTrue();
        res.IsApproved.Should().BeTrue();
    }
}
