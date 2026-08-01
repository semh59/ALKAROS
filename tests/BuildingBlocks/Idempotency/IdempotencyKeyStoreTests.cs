using ALKAROS.Idempotency;
using ALKAROS.Idempotency.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

public sealed class IdempotencyKeyStoreTests : IClassFixture<StoreTestDatabase>
{
    private readonly StoreTestDatabase _database;

    public IdempotencyKeyStoreTests(StoreTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task RegisterOrReplayFirstUseReturnsCreatedAndStoresEnvelope()
    {
        var store = new IdempotencyKeyStore(_database.DataSource);
        var key = new IdempotencyKey($"client-{Guid.NewGuid():N}", "op-1");
        var envelope = new byte[] { 1, 2, 3 };

        var outcome = await store.RegisterOrReplayAsync(key, "body"u8.ToArray(), envelope);

        Assert.Equal(IdempotencyStatus.Created, outcome.Status);
        Assert.False(outcome.IsReplay);
        Assert.Equal(envelope, outcome.ResponseEnvelope);
    }

    [Fact]
    public async Task RegisterOrReplaySameKeySameHashReturnsReplayWithOriginalEnvelope()
    {
        var store = new IdempotencyKeyStore(_database.DataSource);
        var key = new IdempotencyKey($"client-{Guid.NewGuid():N}", "op-1");
        var originalEnvelope = new byte[] { 1, 2, 3 };

        var created = await store.RegisterOrReplayAsync(key, "body"u8.ToArray(), originalEnvelope);
        var replayed = await store.RegisterOrReplayAsync(key, "body"u8.ToArray(), [9, 9, 9]);

        Assert.Equal(IdempotencyStatus.Created, created.Status);
        Assert.Equal(IdempotencyStatus.Replayed, replayed.Status);
        Assert.True(replayed.IsReplay);
        Assert.Equal(originalEnvelope, replayed.ResponseEnvelope);
    }

    [Fact]
    public async Task RegisterOrReplaySameKeyDifferentHashThrowsConflict()
    {
        var store = new IdempotencyKeyStore(_database.DataSource);
        var key = new IdempotencyKey($"client-{Guid.NewGuid():N}", "op-1");

        await store.RegisterOrReplayAsync(key, "body"u8.ToArray(), [1]);

        var exception = await Assert.ThrowsAsync<IdempotencyKeyConflictException>(
            () => store.RegisterOrReplayAsync(key, "different"u8.ToArray(), [2]));
        Assert.Equal(key, exception.Key);
    }

    [Fact]
    public async Task SweepExpiredAsyncRemovesOnlyExpiredRecords()
    {
        var store = new IdempotencyKeyStore(_database.DataSource);
        var clientId = $"client-{Guid.NewGuid():N}";
        var expiredKey = new IdempotencyKey(clientId, "expired");
        var freshKey = new IdempotencyKey(clientId, "fresh");

        await store.RegisterOrReplayAsync(expiredKey, "a"u8.ToArray(), [1]);
        await store.RegisterOrReplayAsync(freshKey, "b"u8.ToArray(), [2]);
        await _database.ForceExpiredAsync("idempotency_keys", "expired");

        var deleted = await store.SweepExpiredAsync();

        Assert.Equal(1, deleted);
        var remaining = await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM idempotency_keys WHERE client_id = '{clientId}';");
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task SweepExpiredAsyncWithNoExpiredRecordsDeletesNothing()
    {
        var store = new IdempotencyKeyStore(_database.DataSource);
        await store.RegisterOrReplayAsync(
            new IdempotencyKey($"client-{Guid.NewGuid():N}", "op-1"), "body"u8.ToArray(), [1]);

        var deleted = await store.SweepExpiredAsync();

        Assert.Equal(0, deleted);
    }
}
