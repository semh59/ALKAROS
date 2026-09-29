using Xunit;

namespace ALKAROS.CustomerAccounts.AccountPayments.Tests;

/// <summary>V14-ACC-004: the AccountPayment transition matrix and its evidence rules, without a database.</summary>
public sealed class AccountPaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private static AccountPayment Cash() =>
        AccountPayment.Request(Guid.NewGuid(), AccountPaymentMethod.Cash, 100m, Guid.NewGuid().ToString(), null, Now);

    [Fact]
    public void ANewPaymentIsRequested()
    {
        var payment = Cash();

        Assert.Equal(AccountPaymentStatus.Requested, payment.Status);
        Assert.Null(payment.Evidence);
        Assert.False(payment.IsTerminal);
    }

    [Fact]
    public void ApprovalCarriesTheMethodSpecificEvidence()
    {
        var cashTransactionId = Guid.NewGuid();

        var approved = Cash().Approve(AccountPaymentEvidence.ForCashTransaction(cashTransactionId), Now);

        Assert.Equal(AccountPaymentStatus.Approved, approved.Status);
        Assert.Equal(new AccountPaymentEvidence(AccountPaymentEvidenceType.CashTransaction, cashTransactionId.ToString("D")), approved.Evidence);
        Assert.True(approved.IsTerminal);
    }

    [Fact]
    public void ACashPaymentCannotBeApprovedWithCardEvidence()
    {
        Assert.Throws<InvalidAccountPaymentTransitionException>(
            () => Cash().Approve(new AccountPaymentEvidence(AccountPaymentEvidenceType.CardProviderReference, "AUTH-1"), Now));
    }

    [Fact]
    public void ApprovalWithoutAReferenceIsRejected()
    {
        Assert.Throws<InvalidAccountPaymentTransitionException>(
            () => Cash().Approve(new AccountPaymentEvidence(AccountPaymentEvidenceType.CashTransaction, " "), Now));
    }

    [Fact]
    public void AnApprovedPaymentCannotBeConstructedWithoutEvidence()
    {
        Assert.Throws<InvalidAccountPaymentTransitionException>(() => new AccountPayment(
            Guid.NewGuid(), Guid.NewGuid(), AccountPaymentMethod.Cash, 10m, "key", Now, status: AccountPaymentStatus.Approved));
    }

    [Fact]
    public void UnknownIsNotSuccessAndCanStillResolve()
    {
        var unknown = AccountPayment.Request(Guid.NewGuid(), AccountPaymentMethod.BankCard, 50m, "k", null, Now).MarkUnknown(Now);

        Assert.Equal(AccountPaymentStatus.Unknown, unknown.Status);
        Assert.False(unknown.IsTerminal);
        Assert.Equal(AccountPaymentStatus.Approved,
            unknown.Approve(new AccountPaymentEvidence(AccountPaymentEvidenceType.CardProviderReference, "AUTH-9"), Now).Status);
        Assert.Equal(AccountPaymentStatus.Declined, unknown.Decline(Now).Status);
    }

    [Theory]
    [InlineData(AccountPaymentStatus.Approved, AccountPaymentStatus.Declined)]
    [InlineData(AccountPaymentStatus.Approved, AccountPaymentStatus.Unknown)]
    [InlineData(AccountPaymentStatus.Declined, AccountPaymentStatus.Approved)]
    [InlineData(AccountPaymentStatus.Declined, AccountPaymentStatus.Unknown)]
    [InlineData(AccountPaymentStatus.Unknown, AccountPaymentStatus.Unknown)]
    [InlineData(AccountPaymentStatus.Requested, AccountPaymentStatus.Requested)]
    public void TerminalAndRepeatedTransitionsAreRejected(AccountPaymentStatus from, AccountPaymentStatus to)
    {
        Assert.False(AccountPayment.CanTransition(from, to));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.001)]
    public void AmountMustBePositiveWithTwoDecimals(double amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AccountPayment.Request(Guid.NewGuid(), AccountPaymentMethod.Cash, (decimal)amount, "key", null, Now));
    }
}
