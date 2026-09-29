using ALKAROS.CustomerAccounts.AccountPayments;
using ALKAROS.CustomerAccounts.AccountReceipts.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.CustomerAccounts.AccountReceipts.Tests;

/// <summary>V14-ACC-009 against real PostgreSQL: one receipt per verified account payment, never for an unverified one.</summary>
public sealed class PostgresAccountReceiptServiceTests : IClassFixture<AccountReceiptTestDatabase>
{
    private readonly AccountReceiptTestDatabase _database;
    private readonly PostgresAccountPaymentRepository _payments;
    private readonly PostgresAccountReceiptService _receipts;

    public PostgresAccountReceiptServiceTests(AccountReceiptTestDatabase database)
    {
        _database = database;
        _payments = new PostgresAccountPaymentRepository(database.DataSource);
        _receipts = new PostgresAccountReceiptService(_payments, database.DataSource);
    }

    [Fact]
    public async Task AnApprovedPaymentGetsOneNumberedReceipt()
    {
        var payment = await ApprovedAsync(amount: 75m);

        var result = await _receipts.IssueAsync(payment.Id, Key(), issuedBy: null);

        Assert.False(result.WasReplayed);
        Assert.StartsWith("CT-", result.Receipt.ReceiptNumber);
        Assert.Equal(11, result.Receipt.ReceiptNumber.Length);
        Assert.Equal(75m, result.Receipt.Amount);
        Assert.Equal("TRY", result.Receipt.CurrencyCode);
        Assert.Equal(payment.CustomerId, result.Receipt.CustomerId);
        Assert.Equal(result.Receipt, await _receipts.GetByPaymentAsync(payment.Id));
    }

    [Fact]
    public async Task RetryingTheSameKeyReturnsTheSameReceipt()
    {
        var payment = await ApprovedAsync();
        var key = Key();

        var first = await _receipts.IssueAsync(payment.Id, key, null);
        var retry = await _receipts.IssueAsync(payment.Id, key, null);

        Assert.True(retry.WasReplayed);
        Assert.Equal(first.Receipt.Id, retry.Receipt.Id);
        Assert.Single(await _receipts.GetByCustomerAsync(payment.CustomerId));
    }

    [Fact]
    public async Task ASecondRequestForTheSamePaymentWithANewKeyReturnsTheExistingReceipt()
    {
        var payment = await ApprovedAsync();

        var first = await _receipts.IssueAsync(payment.Id, Key(), null);
        var again = await _receipts.IssueAsync(payment.Id, Key(), null);

        Assert.True(again.WasReplayed);
        Assert.Equal(first.Receipt.Id, again.Receipt.Id);
    }

    [Fact]
    public async Task ConcurrentRequestsForOnePaymentIssueOneReceipt()
    {
        var payment = await ApprovedAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _receipts.IssueAsync(payment.Id, Key(), null)));

        Assert.Single(results.Select(r => r.Receipt.Id).Distinct());
        Assert.Single(await _receipts.GetByCustomerAsync(payment.CustomerId));
    }

    [Fact]
    public async Task AKeyThatReceiptedAnotherPaymentIsRefused()
    {
        var key = Key();
        await _receipts.IssueAsync((await ApprovedAsync()).Id, key, null);

        await Assert.ThrowsAsync<AccountReceiptIdempotencyKeyReusedException>(
            async () => await _receipts.IssueAsync((await ApprovedAsync()).Id, key, null));
    }

    [Theory]
    [InlineData(AccountPaymentStatus.Requested)]
    [InlineData(AccountPaymentStatus.Unknown)]
    [InlineData(AccountPaymentStatus.Declined)]
    public async Task AnUnverifiedPaymentIsNotReceiptedAndYieldsReconciliationEvidence(AccountPaymentStatus status)
    {
        var payment = await _payments.RequestAsync(Cash(40m));
        if (status == AccountPaymentStatus.Unknown)
            payment = await _payments.SaveTransitionAsync(payment.MarkUnknown(DateTimeOffset.UtcNow), payment.RowVersion, null, null);
        else if (status == AccountPaymentStatus.Declined)
            payment = await _payments.SaveTransitionAsync(payment.Decline(DateTimeOffset.UtcNow), payment.RowVersion, null, null);

        var refused = await Assert.ThrowsAsync<AccountReceiptPaymentNotVerifiedException>(
            () => _receipts.IssueAsync(payment.Id, Key(), null));

        Assert.Equal(payment.Id, refused.Evidence.AccountPaymentId);
        Assert.Equal(status, refused.Evidence.PaymentStatus);
        Assert.Equal(40m, refused.Evidence.Amount);
        Assert.Null(await _receipts.GetByPaymentAsync(payment.Id));
    }

    [Fact]
    public async Task AnUnknownPaymentIdIsNotFound()
    {
        await Assert.ThrowsAsync<AccountPaymentNotFoundException>(() => _receipts.IssueAsync(Guid.NewGuid(), Key(), null));
    }

    [Fact]
    public async Task ReceiptsAreAppendOnly()
    {
        var receipt = (await _receipts.IssueAsync((await ApprovedAsync()).Id, Key(), null)).Receipt;

        var update = _database.ExecuteAsync(
            "UPDATE customer_account.account_receipts SET amount = 1 WHERE account_receipt_id = @id;", ("id", receipt.Id));
        await Assert.ThrowsAsync<PostgresException>(() => update);
        var delete = _database.ExecuteAsync(
            "DELETE FROM customer_account.account_receipts WHERE account_receipt_id = @id;", ("id", receipt.Id));
        await Assert.ThrowsAsync<PostgresException>(() => delete);
    }

    private static string Key() => Guid.NewGuid().ToString();

    private static AccountPayment Cash(decimal amount) =>
        AccountPayment.Request(Guid.NewGuid(), AccountPaymentMethod.Cash, amount, Key(), null, DateTimeOffset.UtcNow);

    private async Task<AccountPayment> ApprovedAsync(decimal amount = 100m)
    {
        var requested = await _payments.RequestAsync(Cash(amount));
        return await _payments.SaveTransitionAsync(
            requested.Approve(AccountPaymentEvidence.ForCashTransaction(Guid.NewGuid()), DateTimeOffset.UtcNow),
            requested.RowVersion, null, null);
    }
}
