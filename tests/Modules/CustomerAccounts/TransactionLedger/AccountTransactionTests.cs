using Xunit;

namespace ALKAROS.CustomerAccounts.TransactionLedger.Tests;

public sealed class AccountTransactionTests
{
    private static AccountTransaction Make(AccountTransactionType type, AccountTransactionDirection direction, decimal amount) =>
        new(Guid.NewGuid(), Guid.NewGuid(), type, direction, amount, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow);

    [Fact]
    public void ChargeContributesItsFullPositiveAmount()
    {
        var transaction = Make(AccountTransactionType.Charge, AccountTransactionDirection.Debit, 100);
        Assert.Equal(100, transaction.SignedBalanceEffect);
    }

    [Fact]
    public void PaymentContributesTheNegativeOfItsMagnitude()
    {
        var transaction = Make(AccountTransactionType.Payment, AccountTransactionDirection.Credit, 80);
        Assert.Equal(-80, transaction.SignedBalanceEffect);
    }

    [Fact]
    public void RefundContributesTheNegativeOfItsMagnitude()
    {
        var transaction = Make(AccountTransactionType.Refund, AccountTransactionDirection.Credit, 30);
        Assert.Equal(-30, transaction.SignedBalanceEffect);
    }

    [Fact]
    public void APositiveAdjustmentContributesExactlyItsOwnAmount()
    {
        var transaction = Make(AccountTransactionType.Adjustment, AccountTransactionDirection.Debit, 20);
        Assert.Equal(20, transaction.SignedBalanceEffect);
    }

    [Fact]
    public void ANegativeAdjustmentContributesExactlyItsOwnAmountNotDoublyFlipped()
    {
        // Regression: Direction is Credit for a negative Adjustment, but the
        // generic "Credit means negate the stored magnitude" rule must NOT
        // apply here - amount is already signed, so the effect is amount
        // itself (-20), not -(-20) = 20.
        var transaction = Make(AccountTransactionType.Adjustment, AccountTransactionDirection.Credit, -20);
        Assert.Equal(-20, transaction.SignedBalanceEffect);
    }

    [Fact]
    public void WorkedExampleFromTheDecisionRecordReproducesTheDocumentedBalance()
    {
        // docs/domain/customer-credit-invoice-semantics.md, section 4:
        // charges 100+150, payment 80 -> receivable_balance = 170.
        var charge1 = Make(AccountTransactionType.Charge, AccountTransactionDirection.Debit, 100);
        var charge2 = Make(AccountTransactionType.Charge, AccountTransactionDirection.Debit, 150);
        var payment = Make(AccountTransactionType.Payment, AccountTransactionDirection.Credit, 80);

        var balance = charge1.SignedBalanceEffect + charge2.SignedBalanceEffect + payment.SignedBalanceEffect;

        Assert.Equal(170, balance);
    }

    [Fact]
    public void WorkedAdjustmentExampleFromTheDecisionRecordReproducesTheDocumentedBalance()
    {
        // Same doc, section 4: "Adjustment -20 on invoice 170: receivable_balance = 150."
        var priorBalance = 170m;
        var adjustment = Make(AccountTransactionType.Adjustment, AccountTransactionDirection.Credit, -20);

        Assert.Equal(150, priorBalance + adjustment.SignedBalanceEffect);
    }

    [Fact]
    public void WorkedRefundExampleFromTheDecisionRecordReproducesTheDocumentedBalance()
    {
        // Same doc, section 4: "Refund 30: receivable_balance = 120."
        var priorBalance = 150m;
        var refund = Make(AccountTransactionType.Refund, AccountTransactionDirection.Credit, 30);

        Assert.Equal(120, priorBalance + refund.SignedBalanceEffect);
    }
}
