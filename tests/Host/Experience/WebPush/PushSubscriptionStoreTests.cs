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
        var store = new PushSubscriptionStore(_database.DataSource);
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
        var store = new PushSubscriptionStore(_database.DataSource);
        var userWithNoSubscription = await _database.SeedUserAsync("Waiter C");

        var result = await store.GetByUserAsync(userWithNoSubscription);

        Assert.Empty(result);
    }
}
