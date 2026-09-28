namespace ALKAROS.CustomerAccounts.BalanceProjection.Tests;

using ALKAROS.CustomerAccounts.BalanceProjection.Tests.Fixtures;
using Xunit;

public sealed class PostgresBalanceSnapshotStoreTests : IAsyncLifetime
{
    private readonly AccountBalanceTestDatabase _database = new();
    private PostgresBalanceSnapshotStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _store = new PostgresBalanceSnapshotStore(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task GetOnAnUnknownSnapshotReturnsNull()
    {
        Assert.Null(await _store.GetSnapshotAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public async Task TakingASnapshotRoundTrips()
    {
        var customerId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 28);

        var snapshot = await _store.TakeSnapshotAsync(customerId, date, 170);

        Assert.Equal(customerId, snapshot.CustomerId);
        Assert.Equal(date, snapshot.SnapshotDate);
        Assert.Equal(170, snapshot.Balance);

        var fetched = await _store.GetSnapshotAsync(customerId, date);
        Assert.Equal(snapshot.Id, fetched!.Id);
    }

    [Fact]
    public async Task RetakingTheSameCustomerAndDateIsIdempotentAndKeepsTheFirstBalance()
    {
        var customerId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 28);

        var first = await _store.TakeSnapshotAsync(customerId, date, 170);
        // A second attempt with a DIFFERENT balance must not silently
        // overwrite the first - a snapshot is a fixed point-in-time fact.
        var retry = await _store.TakeSnapshotAsync(customerId, date, 999);

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(170, retry.Balance);
    }

    [Fact]
    public async Task DifferentDatesForTheSameCustomerAreGenuinelySeparateSnapshots()
    {
        var customerId = Guid.NewGuid();

        await _store.TakeSnapshotAsync(customerId, new DateOnly(2026, 9, 27), 100);
        await _store.TakeSnapshotAsync(customerId, new DateOnly(2026, 9, 28), 150);

        var all = await _store.GetSnapshotsAsync(customerId);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetSnapshotsReturnsOldestFirstAndOnlyThatCustomersRows()
    {
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();

        await _store.TakeSnapshotAsync(customerId, new DateOnly(2026, 9, 28), 150);
        await _store.TakeSnapshotAsync(customerId, new DateOnly(2026, 9, 27), 100);
        await _store.TakeSnapshotAsync(otherCustomerId, new DateOnly(2026, 9, 27), 500);

        var rows = await _store.GetSnapshotsAsync(customerId);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2026, 9, 27), rows[0].SnapshotDate);
        Assert.Equal(new DateOnly(2026, 9, 28), rows[1].SnapshotDate);
    }
}
