namespace ALKAROS.CustomerAccounts.BalanceProjection.Tests;

using ALKAROS.CustomerAccounts.BalanceProjection.Tests.Fixtures;
using ALKAROS.CustomerAccounts.TransactionLedger;
using Xunit;

/// <summary>
/// Real Postgres end-to-end: proves the database's own
/// apply_transaction_to_balance trigger (migration 162) keeps
/// customer_account.balances current atomically with every ledger insert -
/// no application code ever writes to that table directly except RebuildAsync.
/// </summary>
public sealed class PostgresAccountBalanceProjectionTests : IAsyncLifetime
{
    private readonly AccountBalanceTestDatabase _database = new();
    private PostgresAccountTransactionLedger _ledger = null!;
    private PostgresAccountBalanceProjection _balances = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _ledger = new PostgresAccountTransactionLedger(_database.DataSource);
        _balances = new PostgresAccountBalanceProjection(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ACustomerWithNoTransactionsHasNoBalanceRowAtAll()
    {
        Assert.Null(await _balances.GetBalanceAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RecordingASingleChargeAtomicallyCreatesTheBalanceRowViaTheTrigger()
    {
        var customerId = Guid.NewGuid();

        // No call into IAccountBalanceProjection at all here - only the ledger.
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 100, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));

        var balance = await _balances.GetBalanceAsync(customerId);

        Assert.NotNull(balance);
        Assert.Equal(100, balance!.CurrentBalance);
    }

    [Fact]
    public async Task MixedDebitAndCreditTransactionsAccumulateToTheExpectedBalance()
    {
        // docs/domain/customer-credit-invoice-semantics.md example: charges
        // 100+150, payment 80 -> 170; then a -20 adjustment -> 150; then a
        // 30 refund -> 120.
        var customerId = Guid.NewGuid();

        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 100, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 150, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Payment, 80, "Payment", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        Assert.Equal(170, (await _balances.GetBalanceAsync(customerId))!.CurrentBalance);

        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Adjustment, -20, "Manual", Guid.NewGuid(), "waived", Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Equal(150, (await _balances.GetBalanceAsync(customerId))!.CurrentBalance);

        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Refund, 30, "Payment", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        Assert.Equal(120, (await _balances.GetBalanceAsync(customerId))!.CurrentBalance);
    }

    [Fact]
    public async Task LastTransactionAtNeverMovesBackwardEvenIfAnOlderEventArrivesLate()
    {
        var customerId = Guid.NewGuid();
        var early = DateTimeOffset.UtcNow.AddDays(-1);
        var late = DateTimeOffset.UtcNow;

        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 100, "Bill", Guid.NewGuid(), null, null, late));
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 50, "Bill", Guid.NewGuid(), null, null, early));

        var balance = await _balances.GetBalanceAsync(customerId);
        // Postgres TIMESTAMPTZ has microsecond precision; .NET DateTimeOffset
        // has finer tick resolution, so an exact round-trip comparison is not
        // meaningful here - a sub-millisecond difference is not the "moved
        // backward" bug this test actually guards against.
        Assert.True((late - balance!.LastTransactionAt!.Value).Duration() < TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task RebuildingFromScratchReproducesTheSameBalanceTheTriggerAlreadyComputed()
    {
        var customerId = Guid.NewGuid();
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 100, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Payment, 40, "Payment", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        var beforeRebuild = await _balances.GetBalanceAsync(customerId);

        var rebuilt = await _balances.RebuildAsync(customerId);

        Assert.Equal(beforeRebuild!.CurrentBalance, rebuilt.CurrentBalance);
        Assert.Equal(60, rebuilt.CurrentBalance);
    }

    [Fact]
    public async Task RebuildHandlesAdjustmentsAndRefundsLikeTheTrigger()
    {
        var customerId = Guid.NewGuid();
        var latest = DateTimeOffset.UtcNow;
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            Guid.NewGuid(), AccountTransactionType.Charge, 999, "Manual", Guid.NewGuid(), null, null, latest));
        foreach (var (type, amount, note, by, at) in new (AccountTransactionType, decimal, string?, Guid?, DateTimeOffset)[]
                 {
                     (AccountTransactionType.Charge, 200, null, null, latest.AddHours(-1)),
                     (AccountTransactionType.Adjustment, -20, "waived", Guid.NewGuid(), latest.AddHours(-3)),
                     (AccountTransactionType.Adjustment, 5, null, null, latest.AddHours(-2)),
                     (AccountTransactionType.Refund, 30, null, null, latest.AddHours(-4)),
                 })
            await _ledger.RecordAsync(new RecordAccountTransactionRequest(
                customerId, type, amount, "Manual", Guid.NewGuid(), note, by, at));
        var byTrigger = (await _balances.GetBalanceAsync(customerId))!.CurrentBalance;

        var rebuilt = await _balances.RebuildAsync(customerId);

        Assert.Equal(byTrigger, rebuilt.CurrentBalance);
        Assert.Equal(155, rebuilt.CurrentBalance);
        Assert.True((latest.AddHours(-1) - rebuilt.LastTransactionAt!.Value).Duration() < TimeSpan.FromMilliseconds(1));
        var stored = (await _balances.GetBalanceAsync(customerId))!;
        Assert.Equal(rebuilt.LastTransactionAt, stored.LastTransactionAt);
    }

    [Fact]
    public async Task RebuildingRecoversFromAManuallyCorruptedProjectionRow()
    {
        // Simulate the exact scenario the projection contract exists for: the
        // cached row drifted from the authoritative ledger. Deleting/
        // recreating it must reproduce the correct value (Acceptance
        // evidence's own wording).
        var customerId = Guid.NewGuid();
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 100, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));

        await using (var corrupt = _database.DataSource.CreateCommand(
            "UPDATE customer_account.balances SET current_balance = 999999 WHERE customer_id = @id;"))
        {
            corrupt.Parameters.AddWithValue("id", customerId);
            await corrupt.ExecuteNonQueryAsync();
        }
        Assert.Equal(999999, (await _balances.GetBalanceAsync(customerId))!.CurrentBalance);

        var rebuilt = await _balances.RebuildAsync(customerId);

        Assert.Equal(100, rebuilt.CurrentBalance);
        Assert.Equal(100, (await _balances.GetBalanceAsync(customerId))!.CurrentBalance);
    }

    private async Task WaitUntilASessionWaitsOnALockAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var probe = _database.DataSource.CreateCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock';");
            if ((long)(await probe.ExecuteScalarAsync())! > 0)
                return;
            await Task.Delay(50);
        }

        Assert.Fail("No session ever waited on a lock.");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(0)]
    public async Task RebuildWaitsForALedgerWriteInFlightAndIncludesIt(decimal alreadyRecorded)
    {
        var customerId = Guid.NewGuid();
        if (alreadyRecorded > 0)
            await _ledger.RecordAsync(new RecordAccountTransactionRequest(
                customerId, AccountTransactionType.Charge, alreadyRecorded, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        await using var connection = await _database.DataSource.OpenConnectionAsync();
        await using var inFlight = await connection.BeginTransactionAsync();
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 50, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow),
            connection, inFlight);

        var rebuild = _balances.RebuildAsync(customerId);
        await WaitUntilASessionWaitsOnALockAsync();
        Assert.False(rebuild.IsCompleted);
        await inFlight.CommitAsync();
        var rebuilt = await rebuild;

        Assert.Equal(alreadyRecorded + 50, rebuilt.CurrentBalance);
        Assert.Equal(alreadyRecorded + 50, (await _balances.GetBalanceAsync(customerId))!.CurrentBalance);
    }

    [Fact]
    public async Task RebuildWaitsForAnotherTransactionHoldingTheBalanceRowLock()
    {
        var customerId = Guid.NewGuid();
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, AccountTransactionType.Charge, 100, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        await using var connection = await _database.DataSource.OpenConnectionAsync();
        await using var holder = await connection.BeginTransactionAsync();
        await using (var hold = new Npgsql.NpgsqlCommand(
            "SELECT 1 FROM customer_account.balances WHERE customer_id = @id FOR UPDATE;", connection, holder))
        {
            hold.Parameters.AddWithValue("id", customerId);
            await hold.ExecuteScalarAsync();
        }

        var rebuild = _balances.RebuildAsync(customerId);
        await WaitUntilASessionWaitsOnALockAsync();
        Assert.False(rebuild.IsCompleted);
        await holder.CommitAsync();

        Assert.Equal(100, (await rebuild).CurrentBalance);
    }

    [Fact]
    public async Task ConcurrentLedgerWritesAndRebuildsLeaveTheBalanceEqualToTheLedgerSum()
    {
        var customerId = Guid.NewGuid();
        var writes = Enumerable.Range(0, 100).Select(index => Task.Run(() => _ledger.RecordAsync(
            new RecordAccountTransactionRequest(
                customerId, AccountTransactionType.Charge, index + 1, "Bill", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow))));
        var rebuilds = Enumerable.Range(0, 50).Select(_ => Task.Run(() => _balances.RebuildAsync(customerId)));

        await Task.WhenAll(writes.Cast<Task>().Concat(rebuilds));

        var ledgerSum = (await _ledger.GetByCustomerAsync(customerId)).Sum(entry => entry.SignedBalanceEffect);
        Assert.Equal(ledgerSum, (await _balances.GetBalanceAsync(customerId))!.CurrentBalance);
    }

    [Fact]
    public async Task RebuildingACustomerWithNoTransactionsRemovesAnyStaleRowAndReturnsZero()
    {
        var customerId = Guid.NewGuid();
        await using (var stale = _database.DataSource.CreateCommand(
            "INSERT INTO customer_account.balances VALUES (@id, 70, now(), now());"))
        {
            stale.Parameters.AddWithValue("id", customerId);
            await stale.ExecuteNonQueryAsync();
        }

        var rebuilt = await _balances.RebuildAsync(customerId);

        Assert.Equal(0, rebuilt.CurrentBalance);
        Assert.Null(rebuilt.LastTransactionAt);
        Assert.Null(await _balances.GetBalanceAsync(customerId));
    }
}
