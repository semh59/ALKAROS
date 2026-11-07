namespace ALKAROS.CustomerAccounts.BalanceProjection.Tests;

using ALKAROS.CustomerAccounts.Aging;
using ALKAROS.CustomerAccounts.BalanceProjection.Tests.Fixtures;
using ALKAROS.CustomerAccounts.TransactionLedger;
using Xunit;

public sealed class PostgresReceivablesAgingServiceTests : IAsyncLifetime
{
    private static readonly BusinessCalendar Istanbul = new("Europe/Istanbul", 6);
    private static readonly DateOnly AsOf = new(2026, 11, 7);

    private readonly AccountBalanceTestDatabase _database = new();
    private PostgresReceivablesAgingService _service = null!;
    private PostgresAccountTransactionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _service = new PostgresReceivablesAgingService(_database.DataSource);
        _ledger = new PostgresAccountTransactionLedger(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private async Task Record(Guid customerId, AccountTransactionType type, decimal amount, int ageDays)
        => await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, type, amount, "Test", Guid.NewGuid(), type == AccountTransactionType.Adjustment ? "Düzeltme" : null,
            Guid.NewGuid(), new DateTimeOffset(AsOf.AddDays(-ageDays).ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(3)).ToUniversalTime()));

    [Fact]
    public async Task EachCustomerIsAgedFromTheirOwnLedgerAndAgreesWithTheTriggerBalance()
    {
        var big = Guid.NewGuid();
        var small = Guid.NewGuid();
        var settled = Guid.NewGuid();
        var prepaid = Guid.NewGuid();
        await Record(big, AccountTransactionType.Charge, 300m, 95);
        await Record(big, AccountTransactionType.Charge, 100m, 45);
        await Record(big, AccountTransactionType.Payment, 120.50m, 20);
        await Record(big, AccountTransactionType.Adjustment, -10m, 15);
        await Record(small, AccountTransactionType.Charge, 40m, 3);
        await Record(settled, AccountTransactionType.Charge, 70m, 50);
        await Record(settled, AccountTransactionType.Payment, 70m, 49);
        await Record(prepaid, AccountTransactionType.Payment, 25m, 10);

        var report = await _service.GetAsync(AsOf, Istanbul);

        Assert.Equal(new[] { big, small, prepaid }, report.Select(row => row.CustomerId));
        var bigRow = report[0];
        Assert.Equal((0m, 100m, 0m, 169.50m, 0m), (bigRow.Days0To30, bigRow.Days31To60, bigRow.Days61To90, bigRow.Over90, bigRow.UnappliedCredit));
        Assert.Equal(40m, report[1].Days0To30);
        Assert.Equal(25m, report[2].UnappliedCredit);
        Assert.All(report, row => Assert.True(row.IsBalanced));
    }

    [Fact]
    public async Task ACustomerWhoseLedgerNetsToZeroButWhoseCachedBalanceDoesNotIsStillReported()
    {
        var customer = Guid.NewGuid();
        await Record(customer, AccountTransactionType.Charge, 100m, 5);
        await Record(customer, AccountTransactionType.Payment, 100m, 4);
        await using var tamper = _database.DataSource.CreateCommand(
            "UPDATE customer_account.balances SET current_balance = 5 WHERE customer_id = @id;");
        tamper.Parameters.AddWithValue("id", customer);
        await tamper.ExecuteNonQueryAsync();

        var row = Assert.Single(await _service.GetAsync(AsOf, Istanbul));

        Assert.False(row.IsBalanced);
    }

    [Fact]
    public async Task ACustomerWhoseCachedBalanceWasTamperedWithIsReportedAsNotBalanced()
    {
        var customer = Guid.NewGuid();
        await Record(customer, AccountTransactionType.Charge, 100m, 5);
        await using var tamper = _database.DataSource.CreateCommand(
            "UPDATE customer_account.balances SET current_balance = 90 WHERE customer_id = @id;");
        tamper.Parameters.AddWithValue("id", customer);
        await tamper.ExecuteNonQueryAsync();

        var row = Assert.Single(await _service.GetAsync(AsOf, Istanbul));

        Assert.False(row.IsBalanced);
    }
}
