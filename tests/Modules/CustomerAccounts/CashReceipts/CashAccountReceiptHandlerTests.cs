using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.CustomerAccounts.AccountPayments;
using ALKAROS.CustomerAccounts.CashReceipts.Tests.Fixtures;
using ALKAROS.CustomerAccounts.TransactionLedger;
using Xunit;

namespace ALKAROS.CustomerAccounts.CashReceipts.Tests;

/// <summary>V14-ACC-005 against real PostgreSQL: the receipt's three records commit together or not at all.</summary>
public sealed class CashAccountReceiptHandlerTests : IClassFixture<CashReceiptTestDatabase>
{
    private readonly CashReceiptTestDatabase _database;
    private readonly PostgresAccountPaymentRepository _payments;
    private readonly PostgresCashTransactionLedgerRepository _cashLedger;
    private readonly PostgresAccountTransactionLedger _accountLedger;
    private readonly CashAccountReceiptHandler _handler;

    public CashAccountReceiptHandlerTests(CashReceiptTestDatabase database)
    {
        _database = database;
        _payments = new PostgresAccountPaymentRepository(database.DataSource);
        _cashLedger = new PostgresCashTransactionLedgerRepository(database.DataSource);
        _accountLedger = new PostgresAccountTransactionLedger(database.DataSource);
        _handler = new CashAccountReceiptHandler(_payments, _cashLedger, _accountLedger, database.DataSource);
    }

    [Fact]
    public async Task AReceiptWritesTheApprovedAccountPaymentTheCashInAndTheAccountPaymentEntryTogether()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);

        var result = await _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 100m, Key()));

        Assert.False(result.WasReplayed);
        Assert.Equal(50m, result.BalanceAfter);
        Assert.Equal(50m, await BalanceAsync(customerId));

        var payment = Assert.Single(await _payments.GetByCustomerAsync(customerId));
        Assert.Equal(AccountPaymentStatus.Approved, payment.Status);
        Assert.Equal(AccountPaymentEvidence.ForCashTransaction(result.CashTransactionId), payment.Evidence);

        var cash = Assert.Single(await _cashLedger.GetBySessionIdAsync(sessionId), t => t.Type == CashTransactionType.CashIn);
        Assert.Equal(result.CashTransactionId, cash.Id);
        Assert.Equal(100m, cash.Amount);
        Assert.Equal($"account-payment:{payment.Id:D}", cash.IdempotencyKey);
        Assert.Equal(100m, await _cashLedger.ComputeExpectedCashAsync(sessionId));

        var entry = Assert.Single(await _accountLedger.GetByCustomerAsync(customerId), e => e.TransactionType == AccountTransactionType.Payment);
        Assert.Equal(result.AccountTransactionId, entry.Id);
        Assert.Equal("AccountPayment", entry.SourceReferenceType);
        Assert.Equal(payment.Id, entry.SourceReferenceId);
    }

    [Fact]
    public async Task RetryingTheSameKeyReplaysTheSameRecords()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);
        var request = new CashAccountReceiptRequest(customerId, sessionId, 100m, Key());

        var first = await _handler.ReceiveAsync(request);
        var retry = await _handler.ReceiveAsync(request);

        Assert.True(retry.WasReplayed);
        Assert.Equal(first.AccountPaymentId, retry.AccountPaymentId);
        Assert.Equal(first.CashTransactionId, retry.CashTransactionId);
        Assert.Equal(first.AccountTransactionId, retry.AccountTransactionId);
        Assert.Equal(50m, await BalanceAsync(customerId));
        Assert.Single(await _payments.GetByCustomerAsync(customerId));
    }

    [Fact]
    public async Task ConcurrentRetriesOfOneReceiptWriteItOnce()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);
        var request = new CashAccountReceiptRequest(customerId, sessionId, 100m, Key());

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _handler.ReceiveAsync(request)));

        Assert.Single(results.Select(r => r.AccountPaymentId).Distinct());
        Assert.Equal(50m, await BalanceAsync(customerId));
        Assert.Equal(100m, await _cashLedger.ComputeExpectedCashAsync(sessionId));
    }

    [Fact]
    public async Task TwoConcurrentReceiptsCannotTogetherPayMoreThanIsOwed()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);

        var attempts = await Task.WhenAll(
            TryReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 100m, Key())),
            TryReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 100m, Key())));

        Assert.Equal(1, attempts.Count(ok => ok));
        Assert.Equal(50m, await BalanceAsync(customerId));
    }

    [Fact]
    public async Task AReceiptLargerThanTheDebtIsRefusedAndChangesNothing()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 80m);

        var refused = await Assert.ThrowsAsync<CashAccountReceiptOverpaymentException>(
            () => _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 100m, Key())));

        Assert.Equal(80m, refused.Outstanding);
        await AssertNothingWrittenAsync(customerId, sessionId, balance: 80m);
    }

    [Theory]
    [InlineData("Counting")]
    [InlineData("Closed")]
    public async Task AReceiptIntoASessionThatIsNotOpenIsRefused(string status)
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);
        await _database.ExecuteAsync(
            "UPDATE cash.cash_sessions SET status = @status WHERE cash_session_id = @id;", ("status", status), ("id", sessionId));

        await Assert.ThrowsAsync<CashAccountReceiptSessionNotOpenException>(
            () => _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 100m, Key())));

        await AssertNothingWrittenAsync(customerId, sessionId, balance: 150m);
    }

    [Fact]
    public async Task AnUnknownOrAnonymizedCustomerIsRefused()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);
        await _database.ExecuteAsync("UPDATE customer_data.profiles SET anonymized = TRUE WHERE customer_id = @id;", ("id", customerId));

        await Assert.ThrowsAsync<CashAccountReceiptCustomerNotFoundException>(
            () => _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 100m, Key())));
        await Assert.ThrowsAsync<CashAccountReceiptCustomerNotFoundException>(
            () => _handler.ReceiveAsync(new CashAccountReceiptRequest(Guid.NewGuid(), sessionId, 100m, Key())));

        await AssertNothingWrittenAsync(customerId, sessionId, balance: 150m);
    }

    [Fact]
    public async Task InvalidAmountsAndCurrenciesAreRefusedBeforeAnyWrite()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 0m, Key())));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 10.005m, Key())));
        await Assert.ThrowsAsync<CashAccountReceiptCurrencyNotSupportedException>(
            () => _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 10m, Key(), CurrencyCode: "EUR")));

        await AssertNothingWrittenAsync(customerId, sessionId, balance: 150m);
    }

    [Fact]
    public async Task AKeyReusedForADifferentAmountIsRefused()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);
        var key = Key();
        await _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 100m, key));

        await Assert.ThrowsAsync<AccountPaymentIdempotencyKeyReusedException>(
            () => _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 40m, key)));
        Assert.Equal(50m, await BalanceAsync(customerId));
    }

    [Fact]
    public async Task AFailureAfterTheCashMovementRollsBackAllThreeRecords()
    {
        var (customerId, sessionId) = await SeedAsync(owed: 150m);
        await _database.ExecuteAsync(
            $$"""
            CREATE OR REPLACE FUNCTION customer_account.fail_payment_{{customerId:N}}() RETURNS TRIGGER AS $$
            BEGIN
                IF NEW.customer_id = '{{customerId:D}}' AND NEW.transaction_type = 'Payment' THEN
                    RAISE EXCEPTION 'simulated ledger failure';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER trg_fail_payment_{{customerId:N}} BEFORE INSERT ON customer_account.account_transactions
            FOR EACH ROW EXECUTE FUNCTION customer_account.fail_payment_{{customerId:N}}();
            """);
        try
        {
            await Assert.ThrowsAnyAsync<Npgsql.PostgresException>(
                () => _handler.ReceiveAsync(new CashAccountReceiptRequest(customerId, sessionId, 100m, Key())));
        }
        finally
        {
            await _database.ExecuteAsync($"DROP TRIGGER trg_fail_payment_{customerId:N} ON customer_account.account_transactions;");
        }

        await AssertNothingWrittenAsync(customerId, sessionId, balance: 150m);
    }

    private async Task<bool> TryReceiveAsync(CashAccountReceiptRequest request)
    {
        try
        {
            await _handler.ReceiveAsync(request);
            return true;
        }
        catch (CashAccountReceiptOverpaymentException)
        {
            return false;
        }
    }

    private async Task AssertNothingWrittenAsync(Guid customerId, Guid sessionId, decimal balance)
    {
        Assert.Empty(await _payments.GetByCustomerAsync(customerId));
        Assert.DoesNotContain(await _cashLedger.GetBySessionIdAsync(sessionId), t => t.Type == CashTransactionType.CashIn);
        Assert.DoesNotContain(await _accountLedger.GetByCustomerAsync(customerId), e => e.TransactionType == AccountTransactionType.Payment);
        Assert.Equal(balance, await BalanceAsync(customerId));
    }

    private static string Key() => Guid.NewGuid().ToString();

    private async Task<decimal> BalanceAsync(Guid customerId) =>
        await _database.ScalarAsync<decimal>(
            $"SELECT COALESCE((SELECT current_balance FROM customer_account.balances WHERE customer_id = '{customerId:D}'), 0);");

    /// <summary>A customer owing <paramref name="owed"/> on their account and an open cash session on a fresh terminal.</summary>
    private async Task<(Guid CustomerId, Guid SessionId)> SeedAsync(decimal owed)
    {
        var customerId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at) VALUES (@customer_id, '\x00'::bytea, now());
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@cashier_id, 'cashier-' || @cashier_id::text, 'unused', 'Kasiyer', TRUE);
            INSERT INTO cash.cash_sessions (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, opened_at, created_at, updated_at)
            VALUES (@session_id, @cashier_id, @terminal_id, 'Open', 0, now(), now(), now());
            """,
            ("customer_id", customerId), ("cashier_id", cashierId), ("session_id", sessionId), ("terminal_id", Guid.NewGuid()));
        await _accountLedger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, owed, "Test", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        return (customerId, sessionId);
    }
}
