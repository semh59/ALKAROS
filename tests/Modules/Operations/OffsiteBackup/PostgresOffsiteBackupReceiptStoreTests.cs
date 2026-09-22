using ALKAROS.Operations.OffsiteBackup.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Operations.OffsiteBackup.Tests;

public sealed class PostgresOffsiteBackupReceiptStoreTests : IAsyncLifetime
{
    private readonly OffsiteBackupTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();
    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task RecordAsyncThenGetAllReturnsTheReceiptNewestFirst()
    {
        var store = new PostgresOffsiteBackupReceiptStore(_database.DataSource);
        var older = Receipt("artifact-a", DataClass.Settings, DateTimeOffset.UtcNow.AddHours(-1));
        var newer = Receipt("artifact-b", DataClass.Fiscal, DateTimeOffset.UtcNow);

        await store.RecordAsync(older);
        await store.RecordAsync(newer);

        var all = await store.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("artifact-b", all[0].ArtifactId);
        Assert.Equal("artifact-a", all[1].ArtifactId);
    }

    [Fact]
    public async Task RecordAsyncSameArtifactIdTwiceThrowsUniqueViolation()
    {
        var store = new PostgresOffsiteBackupReceiptStore(_database.DataSource);
        var receipt = Receipt("artifact-dup", DataClass.OrdersInventory, DateTimeOffset.UtcNow);
        await store.RecordAsync(receipt);

        await Assert.ThrowsAsync<PostgresException>(() => store.RecordAsync(receipt));
    }

    [Fact]
    public async Task GetByDataClassAsyncFiltersToOnlyThatClass()
    {
        var store = new PostgresOffsiteBackupReceiptStore(_database.DataSource);
        await store.RecordAsync(Receipt("fiscal-1", DataClass.Fiscal, DateTimeOffset.UtcNow));
        await store.RecordAsync(Receipt("settings-1", DataClass.Settings, DateTimeOffset.UtcNow));

        var fiscalOnly = await store.GetByDataClassAsync(DataClass.Fiscal);

        var single = Assert.Single(fiscalOnly);
        Assert.Equal("fiscal-1", single.ArtifactId);
    }

    private static OffsiteBackupReceipt Receipt(string artifactId, DataClass dataClass, DateTimeOffset uploadedAt) => new(
        artifactId,
        dataClass,
        new string('a', 64),
        "test-key",
        1,
        2048,
        "local://" + artifactId,
        uploadedAt);
}
