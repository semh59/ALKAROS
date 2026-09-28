namespace ALKAROS.CustomerAccounts.TransactionLedger.Tests;

using ALKAROS.CustomerAccounts.TransactionLedger.Tests.Fixtures;
using Npgsql;
using Xunit;

public sealed class PostgresAccountTransactionLedgerTests : IAsyncLifetime
{
    private readonly AccountTransactionTestDatabase _database = new();
    private PostgresAccountTransactionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _ledger = new PostgresAccountTransactionLedger(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static RecordAccountTransactionRequest ChargeRequest(Guid customerId, Guid billId, decimal amount = 100) =>
        new(customerId, AccountTransactionType.Charge, amount, "Bill", billId, null, null, DateTimeOffset.UtcNow);

    [Fact]
    public async Task GetOnAnUnknownTransactionReturnsNull()
    {
        Assert.Null(await _ledger.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RecordingAChargeRoundTrips()
    {
        var customerId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var recorded = await _ledger.RecordAsync(ChargeRequest(customerId, billId, 150));

        Assert.Equal(customerId, recorded.CustomerId);
        Assert.Equal(AccountTransactionType.Charge, recorded.TransactionType);
        Assert.Equal(150, recorded.Amount);
        Assert.Equal("Bill", recorded.SourceReferenceType);
        Assert.Equal(billId, recorded.SourceReferenceId);
    }

    [Fact]
    public async Task TheDatabaseItselfDerivesDirectionNeverTheApplication()
    {
        // Charge/Payment/Refund each prove a different branch of the
        // generated CASE expression actually runs in Postgres - the domain
        // layer never sends a "direction" value at all (see
        // PostgresAccountTransactionLedger.RecordAsync's own INSERT column
        // list).
        var customerId = Guid.NewGuid();
        var charge = await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 100, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        var payment = await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Payment, 80, "Payment", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        var negativeAdjustment = await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Adjustment, -20, "Manual", Guid.NewGuid(), "waived", Guid.NewGuid(), DateTimeOffset.UtcNow));
        var positiveAdjustment = await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Adjustment, 20, "Manual", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));

        Assert.Equal(AccountTransactionDirection.Debit, charge.Direction);
        Assert.Equal(AccountTransactionDirection.Credit, payment.Direction);
        Assert.Equal(AccountTransactionDirection.Credit, negativeAdjustment.Direction);
        Assert.Equal(AccountTransactionDirection.Debit, positiveAdjustment.Direction);
    }

    [Fact]
    public async Task RecordingTheSameSourceEventTwiceIsIdempotentNotADuplicate()
    {
        var customerId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var first = await _ledger.RecordAsync(ChargeRequest(customerId, billId));
        var retry = await _ledger.RecordAsync(ChargeRequest(customerId, billId));

        Assert.Equal(first.Id, retry.Id);

        var all = await _ledger.GetByCustomerAsync(customerId);
        Assert.Single(all);
    }

    [Fact]
    public async Task TheSameSourceIdWithADifferentTransactionTypeIsARealSeparateRow()
    {
        // The idempotency key includes transaction_type - a Refund
        // referencing the same PaymentId as an earlier Payment is a
        // genuinely different event, not a retry of it.
        var customerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Payment, 80, "Payment", paymentId, null, null, DateTimeOffset.UtcNow));
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Refund, 80, "Payment", paymentId, null, null, DateTimeOffset.UtcNow));

        var all = await _ledger.GetByCustomerAsync(customerId);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetByCustomerReturnsOldestFirstAndOnlyThatCustomersRows()
    {
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var early = DateTimeOffset.UtcNow.AddMinutes(-10);
        var late = DateTimeOffset.UtcNow;

        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 100, "Bill", Guid.NewGuid(), null, null, late));
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 50, "Bill", Guid.NewGuid(), null, null, early));
        await _ledger.RecordAsync(ChargeRequest(otherCustomerId, Guid.NewGuid()));

        var rows = await _ledger.GetByCustomerAsync(customerId);

        Assert.Equal(2, rows.Count);
        Assert.Equal(50, rows[0].Amount);
        Assert.Equal(100, rows[1].Amount);
    }

    [Fact]
    public async Task TheAppendOnlyTriggerRejectsARawUpdate()
    {
        var recorded = await _ledger.RecordAsync(ChargeRequest(Guid.NewGuid(), Guid.NewGuid()));

        await using var update = _database.DataSource.CreateCommand(
            "UPDATE customer_account.account_transactions SET amount = 999 WHERE id = @id;");
        update.Parameters.AddWithValue("id", recorded.Id);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Contains("append-only", exception.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheAppendOnlyTriggerRejectsARawDelete()
    {
        var recorded = await _ledger.RecordAsync(ChargeRequest(Guid.NewGuid(), Guid.NewGuid()));

        await using var delete = _database.DataSource.CreateCommand(
            "DELETE FROM customer_account.account_transactions WHERE id = @id;");
        delete.Parameters.AddWithValue("id", recorded.Id);

        await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task TheDatabaseRejectsANegativeAmountForAFixedDirectionTypeEvenBypassingTheDomainLayer()
    {
        // Defense in depth: RecordAccountTransactionRequest already blocks
        // this, but the CHECK constraint must hold even for a raw insert.
        await using var insert = _database.DataSource.CreateCommand(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (@id, @customer_id, 'Charge', -100, 'Bill', @source_id, now());
            """);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("customer_id", Guid.NewGuid());
        insert.Parameters.AddWithValue("source_id", Guid.NewGuid());

        await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task TheDatabaseRejectsANegativeAdjustmentWithNoNoteOrCreatedBy()
    {
        await using var insert = _database.DataSource.CreateCommand(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (@id, @customer_id, 'Adjustment', -20, 'Manual', @source_id, now());
            """);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("customer_id", Guid.NewGuid());
        insert.Parameters.AddWithValue("source_id", Guid.NewGuid());

        await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
    }
}
