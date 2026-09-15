using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.WebPush.Tests;

/// <summary>
/// V1-RMD-201: <see cref="WebPushSender.SendToUserAsync"/> must reach only
/// one waiter's devices, and the pre-existing <see cref="WebPushSender.BroadcastAsync"/>
/// must keep reaching everyone (used for events like V1-RMD-149's pending QR
/// order, or an order with no serving waiter) — both send through the same
/// refactored <c>SendToSubscriptionsAsync</c> path, so both are covered here.
/// </summary>
[Collection("Web push PostgreSQL HTTP")]
public sealed class WebPushSenderTests : IAsyncLifetime
{
    // RFC 8291 §5, verbatim (same vector WebPushCryptoTests uses) — a real
    // uncompressed P-256 point and 16-byte auth secret, so Encrypt() does
    // not throw on the way to a request actually being sent.
    private const string ValidP256dh = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string ValidAuth = "BTBZMqHH6r4Tts7J_aSIgg";

    private readonly WebPushTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task SendToUserOnlyPostsToThatUsersEndpoint()
    {
        var store = new PushSubscriptionStore(_database.DataSource);
        var userA = await _database.SeedUserAsync("Waiter A");
        var userB = await _database.SeedUserAsync("Waiter B");
        var terminalId = Guid.NewGuid();
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/a", ValidP256dh, ValidAuth, userA, terminalId));
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/b", ValidP256dh, ValidAuth, userB, terminalId));
        var handler = new RecordingHandler();
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);

        await sender.SendToUserAsync(
            new WebPushMessage("Sipariş hazır", "Mercimek çorbası hazır", "alkaros-order-ready"), userA);

        var requested = Assert.Single(handler.RequestedEndpoints);
        Assert.Equal("https://push.example.net/a", requested);
    }

    [Fact]
    public async Task SendToUserDoesNothingForAUserWithNoSubscription()
    {
        var store = new PushSubscriptionStore(_database.DataSource);
        var userWithNoSubscription = await _database.SeedUserAsync("Waiter C");
        var handler = new RecordingHandler();
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);

        await sender.SendToUserAsync(
            new WebPushMessage("Sipariş hazır", "Mercimek çorbası hazır", "alkaros-order-ready"),
            userWithNoSubscription);

        Assert.Empty(handler.RequestedEndpoints);
    }

    [Fact]
    public async Task BroadcastStillReachesEveryRegisteredEndpoint()
    {
        var store = new PushSubscriptionStore(_database.DataSource);
        var userA = await _database.SeedUserAsync("Waiter A");
        var userB = await _database.SeedUserAsync("Waiter B");
        var terminalId = Guid.NewGuid();
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/a2", ValidP256dh, ValidAuth, userA, terminalId));
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/b2", ValidP256dh, ValidAuth, userB, terminalId));
        var handler = new RecordingHandler();
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);

        await sender.BroadcastAsync(new WebPushMessage("Yeni sipariş", "Onay bekliyor", "alkaros-pending"));

        var ordered = handler.RequestedEndpoints.OrderBy(endpoint => endpoint, StringComparer.Ordinal).ToList();
        Assert.Equal(2, ordered.Count);
        Assert.Equal("https://push.example.net/a2", ordered[0]);
        Assert.Equal("https://push.example.net/b2", ordered[1]);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> RequestedEndpoints { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedEndpoints.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        }
    }
}
