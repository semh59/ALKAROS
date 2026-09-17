using ALKAROS.Cash.Contracts;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Cash.TransactionLedger.Tests;

/// <summary>Pure (no database) tests for <see cref="CashTransaction"/>'s own constructor invariants.</summary>
public sealed class CashTransactionTests
{
    [Theory]
    [InlineData(CashTransactionType.Opening, CashTransactionDirection.In)]
    [InlineData(CashTransactionType.Sale, CashTransactionDirection.In)]
    [InlineData(CashTransactionType.CashIn, CashTransactionDirection.In)]
    [InlineData(CashTransactionType.CashOut, CashTransactionDirection.Out)]
    [InlineData(CashTransactionType.Refund, CashTransactionDirection.Out)]
    public void FixedDirectionTypesAcceptOnlyTheirOwnDirection(CashTransactionType type, CashTransactionDirection correctDirection)
    {
        var paymentId = type is CashTransactionType.Sale or CashTransactionType.Refund ? Guid.NewGuid() : (Guid?)null;

        var transaction = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), type, 50m, correctDirection, paymentId);
        transaction.Direction.Should().Be(correctDirection);

        var wrongDirection = correctDirection == CashTransactionDirection.In ? CashTransactionDirection.Out : CashTransactionDirection.In;
        var act = () => new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), type, 50m, wrongDirection, paymentId);
        act.Should().Throw<InvalidCashTransactionDirectionException>();
    }

    [Theory]
    [InlineData(CashTransactionDirection.In)]
    [InlineData(CashTransactionDirection.Out)]
    public void CountAdjustmentAndClosingDifferenceAcceptEitherDirection(CashTransactionDirection direction)
    {
        var adjustment = new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CountAdjustment, 5m, direction, notes: "Sayim farki");
        adjustment.Direction.Should().Be(direction);

        var closing = new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.ClosingDifference, 5m, direction);
        closing.Direction.Should().Be(direction);
    }

    [Fact]
    public void ConstructorRejectsAZeroOrNegativeAmount()
    {
        var actZero = () => new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashIn, 0m, CashTransactionDirection.In);
        actZero.Should().Throw<InvalidCashTransactionAmountException>();

        var actNegative = () => new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashIn, -5m, CashTransactionDirection.In);
        actNegative.Should().Throw<InvalidCashTransactionAmountException>();
    }

    [Fact]
    public void SaleAndRefundRequireARelatedPaymentId()
    {
        var actSale = () => new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.Sale, 50m, CashTransactionDirection.In);
        actSale.Should().Throw<MissingRelatedPaymentException>();

        var actRefund = () => new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.Refund, 50m, CashTransactionDirection.Out);
        actRefund.Should().Throw<MissingRelatedPaymentException>();
    }

    [Fact]
    public void NonPaymentTypesRejectARelatedPaymentId()
    {
        var act = () => new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashIn, 50m, CashTransactionDirection.In,
            relatedPaymentId: Guid.NewGuid());

        act.Should().Throw<UnexpectedRelatedPaymentException>();
    }

    [Fact]
    public void CountAdjustmentRequiresAnExplicitReason()
    {
        var act = () => new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CountAdjustment, 5m, CashTransactionDirection.In);

        act.Should().Throw<MissingCountAdjustmentReasonException>();
    }

    [Fact]
    public void SignedAmountReflectsDirection()
    {
        var inflow = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashIn, 30m, CashTransactionDirection.In);
        inflow.SignedAmount.Should().Be(30m);

        var outflow = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashOut, 30m, CashTransactionDirection.Out);
        outflow.SignedAmount.Should().Be(-30m);
    }

    [Fact]
    public void OnlyCountAdjustmentAndClosingDifferenceAreExcludedFromExpectedCash()
    {
        var opening = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.Opening, 1m, CashTransactionDirection.In);
        var adjustment = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CountAdjustment, 1m, CashTransactionDirection.In, notes: "x");
        var closing = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.ClosingDifference, 1m, CashTransactionDirection.In);

        opening.ContributesToExpectedCash.Should().BeTrue();
        adjustment.ContributesToExpectedCash.Should().BeFalse();
        closing.ContributesToExpectedCash.Should().BeFalse();
    }
}
