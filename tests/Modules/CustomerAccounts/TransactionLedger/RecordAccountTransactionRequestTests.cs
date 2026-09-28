using Xunit;

namespace ALKAROS.CustomerAccounts.TransactionLedger.Tests;

public sealed class RecordAccountTransactionRequestTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid SourceId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CustomerIdMustNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new RecordAccountTransactionRequest(
            Guid.Empty, AccountTransactionType.Charge, 100, "Bill", SourceId, null, null, Now));
    }

    [Fact]
    public void SourceReferenceIdMustNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new RecordAccountTransactionRequest(
            CustomerId, AccountTransactionType.Charge, 100, "Bill", Guid.Empty, null, null, Now));
    }

    [Fact]
    public void ZeroAmountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecordAccountTransactionRequest(
            CustomerId, AccountTransactionType.Charge, 0, "Bill", SourceId, null, null, Now));
    }

    [Theory]
    [InlineData(AccountTransactionType.Charge)]
    [InlineData(AccountTransactionType.Payment)]
    [InlineData(AccountTransactionType.Invoice)]
    [InlineData(AccountTransactionType.Credit)]
    [InlineData(AccountTransactionType.Debit)]
    [InlineData(AccountTransactionType.Refund)]
    public void NegativeAmountIsRejectedForEveryTypeExceptAdjustment(AccountTransactionType type)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecordAccountTransactionRequest(
            CustomerId, type, -50, "Bill", SourceId, null, null, Now));
    }

    [Fact]
    public void ANegativeAdjustmentWithoutANoteIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new RecordAccountTransactionRequest(
            CustomerId, AccountTransactionType.Adjustment, -20, "Manual", SourceId, note: null, createdBy: Guid.NewGuid(), Now));
    }

    [Fact]
    public void ANegativeAdjustmentWithoutACreatedByIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new RecordAccountTransactionRequest(
            CustomerId, AccountTransactionType.Adjustment, -20, "Manual", SourceId, note: "waived", createdBy: null, Now));
    }

    [Fact]
    public void ANegativeAdjustmentWithBothNoteAndCreatedByIsAccepted()
    {
        var request = new RecordAccountTransactionRequest(
            CustomerId, AccountTransactionType.Adjustment, -20, "Manual", SourceId, note: "waived", createdBy: Guid.NewGuid(), Now);

        Assert.Equal(-20, request.Amount);
    }

    [Fact]
    public void APositiveAdjustmentNeedsNoNoteOrCreatedBy()
    {
        var exception = Record.Exception(() => new RecordAccountTransactionRequest(
            CustomerId, AccountTransactionType.Adjustment, 20, "Manual", SourceId, note: null, createdBy: null, Now));

        Assert.Null(exception);
    }

    [Fact]
    public void APositiveChargeConstructsCleanly()
    {
        var request = new RecordAccountTransactionRequest(
            CustomerId, AccountTransactionType.Charge, 150, "Bill", SourceId, null, null, Now);

        Assert.Equal(AccountTransactionType.Charge, request.TransactionType);
        Assert.Equal(150, request.Amount);
    }
}
