namespace ALKAROS.CustomerAccounts.BalanceProjection.Tests;

using ALKAROS.CustomerAccounts.BalanceProjection.Tests.Fixtures;
using ALKAROS.CustomerAccounts.TransactionLedger;
using Xunit;

public sealed class PostgresBalanceSnapshotStoreTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 9, 28);
    private static readonly DateTimeOffset EndOfDay = new(2026, 9, 29, 6, 0, 0, TimeSpan.FromHours(3));

    private readonly AccountBalanceTestDatabase _database = new();
    private PostgresBalanceSnapshotStore _store = null!;
    private PostgresAccountTransactionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _store = new PostgresBalanceSnapshotStore(_database.DataSource);
        _ledger = new PostgresAccountTransactionLedger(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private async Task Record(Guid customerId, AccountTransactionType type, decimal amount, DateTimeOffset at)
        => await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, type, amount, "Test", Guid.NewGuid(), type == AccountTransactionType.Adjustment ? "Düzeltme" : null,
            Guid.NewGuid(), at.ToUniversalTime()));

    [Fact]
    public async Task TheClosingSnapshotIsTheLedgerSumBeforeTheCutoffWithTheTriggersSignRule()
    {
        var customer = Guid.NewGuid();
        var before = EndOfDay.AddHours(-5);
        await Record(customer, AccountTransactionType.Charge, 100m, before);
        await Record(customer, AccountTransactionType.Payment, 30.25m, before);
        await Record(customer, AccountTransactionType.Adjustment, -10m, before);
        await Record(customer, AccountTransactionType.Adjustment, 5m, before);
        await Record(customer, AccountTransactionType.Charge, 999m, EndOfDay);

        var written = await _store.TakeClosingSnapshotsAsync(Day, EndOfDay);

        Assert.Equal(1, written);
        var snapshot = Assert.Single(await _store.GetSnapshotsOnAsync([Day]));
        Assert.Equal((customer, Day, 64.75m), (snapshot.CustomerId, snapshot.SnapshotDate, snapshot.Balance));
    }

    [Fact]
    public async Task ARerunKeepsTheFirstSnapshotEvenWhenARowIsBackDatedIntoTheDay()
    {
        var customer = Guid.NewGuid();
        await Record(customer, AccountTransactionType.Charge, 100m, EndOfDay.AddHours(-2));
        await _store.TakeClosingSnapshotsAsync(Day, EndOfDay);

        await Record(customer, AccountTransactionType.Charge, 50m, EndOfDay.AddHours(-1));
        var rewritten = await _store.TakeClosingSnapshotsAsync(Day, EndOfDay);

        Assert.Equal(0, rewritten);
        Assert.Equal(100m, Assert.Single(await _store.GetSnapshotsOnAsync([Day])).Balance);
    }

    [Fact]
    public async Task ReadingReturnsEveryCustomerForTheRequestedDatesOnly()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await Record(first, AccountTransactionType.Charge, 10m, EndOfDay.AddDays(-1).AddHours(-1));
        await Record(second, AccountTransactionType.Charge, 20m, EndOfDay.AddHours(-1));
        await _store.TakeClosingSnapshotsAsync(Day.AddDays(-1), EndOfDay.AddDays(-1));
        await _store.TakeClosingSnapshotsAsync(Day, EndOfDay);

        var onlyDay = await _store.GetSnapshotsOnAsync([Day]);
        var both = await _store.GetSnapshotsOnAsync([Day, Day.AddDays(-1)]);

        Assert.Equal(new[] { first, second }.Order(), onlyDay.Select(snapshot => snapshot.CustomerId).Order());
        Assert.Equal(3, both.Count);
        Assert.Empty(await _store.GetSnapshotsOnAsync([Day.AddDays(7)]));
    }

    [Fact]
    public async Task SnapshotDatesListOnlyDatesFromTheGivenDayOn()
    {
        var customer = Guid.NewGuid();
        await Record(customer, AccountTransactionType.Charge, 10m, EndOfDay.AddDays(-3));
        foreach (var offset in new[] { -2, -1, 0 })
            await _store.TakeClosingSnapshotsAsync(Day.AddDays(offset), EndOfDay.AddDays(offset));

        var dates = await _store.GetSnapshotDatesAsync(Day.AddDays(-1));

        Assert.Equal(new[] { Day.AddDays(-1), Day }, dates.Order());
    }
}
