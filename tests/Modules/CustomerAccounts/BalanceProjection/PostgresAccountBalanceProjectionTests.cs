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
        _balances = new PostgresAccountBalanceProjection(_database.DataSource, _ledger);
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

    [Fact]
    public async Task RebuildingACustomerWithNoTransactionsRemovesAnyStaleRowAndReturnsZero()
    {
        var customerId = Guid.NewGuid();

        var rebuilt = await _balances.RebuildAsync(customerId);

        Assert.Equal(0, rebuilt.CurrentBalance);
        Assert.Null(rebuilt.LastTransactionAt);
        Assert.Null(await _balances.GetBalanceAsync(customerId));
    }
}
