using Xunit;

namespace ALKAROS.Host.Experience.WebPush.Tests;

/// <summary>
/// V1-RMD-201: <see cref="PushSubscriptionStore.GetByUserAsync"/> is the
/// narrower query <see cref="WebPushSender.SendToUserAsync"/> relies on to
/// reach only one waiter's devices instead of every registered one.
/// </summary>
[Collection("Web push PostgreSQL HTTP")]
public sealed class PushSubscriptionStoreTests : IAsyncLifetime
{
    private readonly WebPushTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ReturnsOnlyThatUsersSubscriptionsAndNobodyElses()
    {
        var store = _database.NewStore();
        var userA = await _database.SeedUserAsync("Waiter A");
        var userB = await _database.SeedUserAsync("Waiter B");
        var terminalId = Guid.NewGuid();

        var subscriptionA = new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/a", "p256dh-a", "auth-a", userA, terminalId);
        var subscriptionB1 = new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/b1", "p256dh-b1", "auth-b1", userB, terminalId);
        var subscriptionB2 = new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/b2", "p256dh-b2", "auth-b2", userB, terminalId);
        await store.SaveAsync(subscriptionA);
        await store.SaveAsync(subscriptionB1);
        await store.SaveAsync(subscriptionB2);

        var resultA = await store.GetByUserAsync(userA);
        var resultB = await store.GetByUserAsync(userB);

        Assert.Equal(subscriptionA.Endpoint, Assert.Single(resultA).Endpoint);
        Assert.Equal(2, resultB.Count);
        Assert.Contains(resultB, s => s.Endpoint == subscriptionB1.Endpoint);
        Assert.Contains(resultB, s => s.Endpoint == subscriptionB2.Endpoint);
    }

    [Fact]
    public async Task ReturnsEmptyForAUserWithNoSubscriptions()
    {
        var store = _database.NewStore();
        var userWithNoSubscription = await _database.SeedUserAsync("Waiter C");

        var result = await store.GetByUserAsync(userWithNoSubscription);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AnEndpointOwnedByAnotherUserOnAnotherTerminalIsNotTakenOver()
    {
        var store = _database.NewStore();
        var owner = await _database.SeedUserAsync("Sahibi");
        var intruder = await _database.SeedUserAsync("Başkası");
        const string endpoint = "https://push.example.net/owned";
        Assert.True(await store.SaveAsync(Record(endpoint, "owner-key", owner, Guid.NewGuid())));

        var saved = await store.SaveAsync(Record(endpoint, "intruder-key", intruder, Guid.NewGuid()));

        Assert.False(saved);
        Assert.Equal(owner, await _database.SubscriptionOwnerAsync(endpoint));
        Assert.Equal("owner-key", await _database.StoredKeyAsync(endpoint));
    }

    [Fact]
    public async Task ADeviceThatChangesHandsOnTheSameTerminalIsReassigned()
    {
        var store = _database.NewStore();
        var first = await _database.SeedUserAsync("Birinci");
        var second = await _database.SeedUserAsync("İkinci");
        var terminalId = Guid.NewGuid();
        const string endpoint = "https://push.example.net/shared-device";
        await store.SaveAsync(Record(endpoint, "key-1", first, terminalId));

        Assert.True(await store.SaveAsync(Record(endpoint, "key-2", second, terminalId)));

        Assert.Equal(second, await _database.SubscriptionOwnerAsync(endpoint));
        Assert.Equal(1, await _database.SubscriptionCountAsync());
    }

    [Fact]
    public async Task TheSameUserMayRefreshTheirEndpointFromAnotherTerminal()
    {
        var store = _database.NewStore();
        var user = await _database.SeedUserAsync("Garson");
        const string endpoint = "https://push.example.net/moved";
        await store.SaveAsync(Record(endpoint, "key-1", user, Guid.NewGuid()));

        Assert.True(await store.SaveAsync(Record(endpoint, "key-2", user, Guid.NewGuid())));

        Assert.Equal("key-2", await _database.StoredKeyAsync(endpoint));
    }

    [Fact]
    public async Task OnlyTheNewestSubscriptionsOfAUserAreKept()
    {
        var store = _database.NewStore();
        var user = await _database.SeedUserAsync("Çok Cihazlı");
        var terminalId = Guid.NewGuid();
        var total = PushSubscriptionStore.MaximumSubscriptionsPerUser + 2;
        for (var index = 0; index < total; index++)
            await store.SaveAsync(Record($"https://push.example.net/device-{index}", "key", user, terminalId));

        var kept = (await store.GetByUserAsync(user)).Select(subscription => subscription.Endpoint).ToList();

        Assert.Equal(PushSubscriptionStore.MaximumSubscriptionsPerUser, kept.Count);
        Assert.DoesNotContain("https://push.example.net/device-0", kept);
        Assert.DoesNotContain("https://push.example.net/device-1", kept);
        Assert.Contains($"https://push.example.net/device-{total - 1}", kept);
    }

    [Fact]
    public async Task ARefreshedSubscriptionCountsAsTheNewestWhenTheLimitIsApplied()
    {
        var store = _database.NewStore();
        var user = await _database.SeedUserAsync("Yenileyen");
        var terminalId = Guid.NewGuid();
        for (var index = 0; index < PushSubscriptionStore.MaximumSubscriptionsPerUser; index++)
            await store.SaveAsync(Record($"https://push.example.net/refresh-{index}", "key", user, terminalId));

        await store.SaveAsync(Record("https://push.example.net/refresh-0", "key-2", user, terminalId));
        await store.SaveAsync(Record("https://push.example.net/refresh-new", "key", user, terminalId));

        var kept = (await store.GetByUserAsync(user)).Select(subscription => subscription.Endpoint).ToList();
        Assert.Contains("https://push.example.net/refresh-0", kept);
        Assert.DoesNotContain("https://push.example.net/refresh-1", kept);
    }

    [Fact]
    public async Task TheLimitIsPerUserAndDoesNotRemoveAnotherUsersDevices()
    {
        var store = _database.NewStore();
        var busy = await _database.SeedUserAsync("Çok Cihazlı");
        var other = await _database.SeedUserAsync("Tek Cihazlı");
        var terminalId = Guid.NewGuid();
        await store.SaveAsync(Record("https://push.example.net/other", "key", other, terminalId));
        for (var index = 0; index < PushSubscriptionStore.MaximumSubscriptionsPerUser + 1; index++)
            await store.SaveAsync(Record($"https://push.example.net/busy-{index}", "key", busy, terminalId));

        Assert.Single(await store.GetByUserAsync(other));
    }

    [Fact]
    public async Task ADeactivatedUsersDevicesAreNeverReturned()
    {
        var store = _database.NewStore();
        var leaver = await _database.SeedUserAsync("Ayrılan");
        var stayer = await _database.SeedUserAsync("Kalan");
        var terminalId = Guid.NewGuid();
        await store.SaveAsync(Record("https://push.example.net/leaver", "key", leaver, terminalId));
        await store.SaveAsync(Record("https://push.example.net/stayer", "key", stayer, terminalId));

        await _database.DeactivateUserAsync(leaver);

        Assert.Empty(await store.GetByUserAsync(leaver));
        Assert.Equal("https://push.example.net/stayer", Assert.Single(await store.GetAllAsync()).Endpoint);
    }

    [Fact]
    public async Task ASubscriptionIsRemovedOnlyOnceTheFailuresInARowReachTheLimit()
    {
        var store = _database.NewStore();
        var user = await _database.SeedUserAsync("Garson");
        const string endpoint = "https://push.example.net/flaky";
        await store.SaveAsync(Record(endpoint, "key", user, Guid.NewGuid()));

        await store.MarkFailureAsync(endpoint, 3);
        await store.MarkFailureAsync(endpoint, 3);
        Assert.Equal(2, await _database.FailureCountAsync(endpoint));

        await store.MarkSuccessAsync(endpoint);
        Assert.Equal(0, await _database.FailureCountAsync(endpoint));

        await store.MarkFailureAsync(endpoint, 3);
        await store.MarkFailureAsync(endpoint, 3);
        await store.MarkFailureAsync(endpoint, 3);
        Assert.Equal(0, await _database.SubscriptionCountAsync());
    }

    [Fact]
    public async Task TheSigningKeyIsStoredOnlyAsAnEnvelopeAndStaysTheSameAcrossRestarts()
    {
        var secrets = WebPushTestDatabase.NewSecrets();
        var created = await _database.NewStore(secrets).GetOrCreateVapidKeysAsync("mailto:test@alkaros.local");

        Assert.False(await _database.VapidPrivateKeyIsPlaintextAsync());
        Assert.True(await _database.VapidEnvelopeExistsAsync());
        var reopened = await _database.NewStore(secrets).GetOrCreateVapidKeysAsync("mailto:test@alkaros.local");
        Assert.Equal(created.PrivateKey, reopened.PrivateKey);
        Assert.Equal(created.PublicKey, reopened.PublicKey);
    }

    [Fact]
    public async Task TheSigningKeyCannotBeReadWithAnotherMasterKey()
    {
        await _database.NewStore().GetOrCreateVapidKeysAsync("mailto:test@alkaros.local");

        await Assert.ThrowsAnyAsync<Exception>(
            () => _database.NewStore(WebPushTestDatabase.NewSecrets()).GetOrCreateVapidKeysAsync("mailto:test@alkaros.local"));
    }

    [Fact]
    public async Task AKeyLeftInPlaintextByAnEarlierVersionIsProtectedOnFirstRead()
    {
        var (publicKey, privateKey) = WebPushCrypto.CreateKeyPair();
        await _database.SeedPlaintextVapidAsync(publicKey, privateKey);

        var read = await _database.NewStore().GetOrCreateVapidKeysAsync("mailto:test@alkaros.local");

        Assert.Equal(privateKey, read.PrivateKey);
        Assert.Equal(publicKey, read.PublicKey);
        Assert.False(await _database.VapidPrivateKeyIsPlaintextAsync());
        Assert.True(await _database.VapidEnvelopeExistsAsync());
    }

    private static PushSubscriptionRecord Record(string endpoint, string key, Guid userId, Guid terminalId)
        => new(Guid.NewGuid(), endpoint, key, "auth", userId, terminalId);
}
