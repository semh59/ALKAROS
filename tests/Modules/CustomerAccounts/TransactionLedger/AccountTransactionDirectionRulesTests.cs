using Xunit;

namespace ALKAROS.CustomerAccounts.TransactionLedger.Tests;

public sealed class AccountTransactionDirectionRulesTests
{
    [Theory]
    [InlineData(AccountTransactionType.Charge, AccountTransactionDirection.Debit)]
    [InlineData(AccountTransactionType.Invoice, AccountTransactionDirection.Debit)]
    [InlineData(AccountTransactionType.Debit, AccountTransactionDirection.Debit)]
    [InlineData(AccountTransactionType.Payment, AccountTransactionDirection.Credit)]
    [InlineData(AccountTransactionType.Credit, AccountTransactionDirection.Credit)]
    [InlineData(AccountTransactionType.Refund, AccountTransactionDirection.Credit)]
    public void EachFixedTypeHasItsOwnDirection(AccountTransactionType type, AccountTransactionDirection expected)
    {
        Assert.True(AccountTransactionDirectionRules.HasFixedDirection(type));
        Assert.Equal(expected, AccountTransactionDirectionRules.FixedDirectionFor(type));
        Assert.Equal(expected, AccountTransactionDirectionRules.DirectionFor(type, amount: 42));
    }

    [Fact]
    public void AdjustmentHasNoFixedDirection()
    {
        Assert.False(AccountTransactionDirectionRules.HasFixedDirection(AccountTransactionType.Adjustment));
        Assert.Throws<ArgumentException>(() => AccountTransactionDirectionRules.FixedDirectionFor(AccountTransactionType.Adjustment));
    }

    [Theory]
    [InlineData(20, AccountTransactionDirection.Debit)]
    [InlineData(0, AccountTransactionDirection.Debit)]
    [InlineData(-20, AccountTransactionDirection.Credit)]
    public void AdjustmentDirectionFollowsTheSignOfItsOwnAmount(decimal amount, AccountTransactionDirection expected)
    {
        Assert.Equal(expected, AccountTransactionDirectionRules.DirectionForAdjustment(amount));
        Assert.Equal(expected, AccountTransactionDirectionRules.DirectionFor(AccountTransactionType.Adjustment, amount));
    }
}
