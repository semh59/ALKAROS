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

        var transaction = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), type, 50m, correctDirection, paymentId, notes: "reason");
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

    [Theory]
    [InlineData("0.001")]
    [InlineData("10.555")]
    [InlineData("10000000000000000")]
    public void ConstructorRejectsAnAmountTheLedgerColumnCannotStoreExactly(string amount)
    {
        var act = () => new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashIn, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            CashTransactionDirection.In);

        act.Should().Throw<InvalidCashTransactionAmountException>();
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("12.30")]
    [InlineData("9999999999999999.99")]
    public void ConstructorAcceptsTwoDecimalAmountsUpToTheColumnLimit(string amount)
    {
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        var transaction = new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashIn, value, CashTransactionDirection.In, notes: "reason");

        transaction.Amount.Should().Be(value);
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

    [Theory]
    [InlineData(CashTransactionType.CashIn, CashTransactionDirection.In, null)]
    [InlineData(CashTransactionType.CashIn, CashTransactionDirection.In, "  \t")]
    [InlineData(CashTransactionType.CashOut, CashTransactionDirection.Out, null)]
    [InlineData(CashTransactionType.CashOut, CashTransactionDirection.Out, "")]
    public void ManualCashMovementsRequireAReason(CashTransactionType type, CashTransactionDirection direction, string? notes)
    {
        var act = () => new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), type, 10m, direction, notes: notes);

        act.Should().Throw<InvalidCashMovementReasonException>();
    }

    [Theory]
    [InlineData(CashTransactionType.Opening, CashTransactionDirection.In)]
    [InlineData(CashTransactionType.ClosingDifference, CashTransactionDirection.Out)]
    public void OnlyManualCashMovementsNeedAReason(CashTransactionType type, CashTransactionDirection direction)
    {
        var transaction = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), type, 10m, direction);

        transaction.Notes.Should().BeNull();
    }

    [Fact]
    public void SaleAndRefundNeedNoReasonEither()
    {
        var sale = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.Sale, 10m, CashTransactionDirection.In, Guid.NewGuid());
        var refund = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.Refund, 10m, CashTransactionDirection.Out, Guid.NewGuid());

        sale.Notes.Should().BeNull();
        refund.Notes.Should().BeNull();
    }

    [Fact]
    public void AMovementReasonIsStoredTrimmed()
    {
        var transaction = new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashOut, 10m, CashTransactionDirection.Out, notes: "  gider \t");

        transaction.Notes.Should().Be("gider");
    }

    [Fact]
    public void AMovementReasonIsKeptUpToTheLimitAndRefusedBeyondIt()
    {
        var atLimit = new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashOut, 10m, CashTransactionDirection.Out,
            notes: new string('x', CashTransaction.MaxReasonLength));
        var beyond = () => new CashTransaction(
            Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashOut, 10m, CashTransactionDirection.Out,
            notes: new string('x', CashTransaction.MaxReasonLength + 1));

        atLimit.Notes.Should().HaveLength(CashTransaction.MaxReasonLength);
        beyond.Should().Throw<InvalidCashMovementReasonException>();
    }

    [Fact]
    public void SignedAmountReflectsDirection()
    {
        var inflow = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashIn, 30m, CashTransactionDirection.In, notes: "reason");
        inflow.SignedAmount.Should().Be(30m);

        var outflow = new CashTransaction(Guid.NewGuid(), Guid.NewGuid(), CashTransactionType.CashOut, 30m, CashTransactionDirection.Out, notes: "reason");
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
