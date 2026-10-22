using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.WebPush.Tests;

/// <summary>
/// V1-RMD-201/209: <see cref="WebPushSender.SendToUserOrBroadcastAsync"/>
/// must reach only one waiter's devices when they have any, and the
/// pre-existing <see cref="WebPushSender.BroadcastAsync"/> must keep
/// reaching everyone (used for events like V1-RMD-149's pending QR order,
/// an order with no serving waiter, or — V1-RMD-209 — a serving waiter with
/// no push subscription at all) — both send through the same refactored
/// <c>SendToSubscriptionsAsync</c> path, so both are covered here.
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
    public async Task SendToUserOrBroadcastOnlyPostsToThatUsersEndpointWhenTheyHaveOne()
    {
        var store = _database.NewStore();
        var userA = await _database.SeedUserAsync("Waiter A");
        var userB = await _database.SeedUserAsync("Waiter B");
        var terminalId = Guid.NewGuid();
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/a", ValidP256dh, ValidAuth, userA, terminalId));
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/b", ValidP256dh, ValidAuth, userB, terminalId));
        var handler = new RecordingHandler();
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);

        await sender.SendToUserOrBroadcastAsync(
            new WebPushMessage("Sipariş hazır", "Mercimek çorbası hazır", "alkaros-order-ready"), userA);

        var requested = Assert.Single(handler.RequestedEndpoints);
        Assert.Equal("https://push.example.net/a", requested);
    }

    [Fact]
    public async Task SendToUserOrBroadcastFallsBackToEveryoneWhenTheUserHasNoSubscription()
    {
        // V1-RMD-209: the whole point of the "or broadcast" half - a serving
        // waiter attributed to a Cashier/PosTerminal session (neither ever
        // registers for push) must not be a silent no-op.
        var store = _database.NewStore();
        var userWithNoSubscription = await _database.SeedUserAsync("Waiter C");
        var otherUser = await _database.SeedUserAsync("Waiter D");
        var terminalId = Guid.NewGuid();
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/d", ValidP256dh, ValidAuth, otherUser, terminalId));
        var handler = new RecordingHandler();
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);

        await sender.SendToUserOrBroadcastAsync(
            new WebPushMessage("Sipariş hazır", "Mercimek çorbası hazır", "alkaros-order-ready"),
            userWithNoSubscription);

        var requested = Assert.Single(handler.RequestedEndpoints);
        Assert.Equal("https://push.example.net/d", requested);
    }

    [Fact]
    public async Task BroadcastStillReachesEveryRegisteredEndpoint()
    {
        var store = _database.NewStore();
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

    [Theory]
    [InlineData("throw-http")]
    [InlineData("throw-other")]
    public async Task OneFailingDeviceDoesNotStopTheOthersFromBeingNotified(string failure)
    {
        var store = _database.NewStore();
        var user = await _database.SeedUserAsync("Garson");
        var terminalId = Guid.NewGuid();
        foreach (var name in new[] { "a", "bad", "c" })
            await store.SaveAsync(new PushSubscriptionRecord(
                Guid.NewGuid(), $"https://push.example.net/{name}", ValidP256dh, ValidAuth, user, terminalId));
        var handler = new RecordingHandler
        {
            Respond = request => request.RequestUri!.AbsolutePath == "/bad"
                ? throw (failure == "throw-http" ? new HttpRequestException("down") : new InvalidOperationException("boom"))
                : new HttpResponseMessage(HttpStatusCode.Created),
        };
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);

        await sender.BroadcastAsync(new WebPushMessage("Yeni sipariş", "Onay bekliyor", "alkaros-pending"));

        Assert.Equal(3, handler.RequestedEndpoints.Count);
        // Only a network failure counts against the subscription; an unexpected bug does not delete a device.
        Assert.Equal(failure == "throw-http" ? 1 : 0, await _database.FailureCountAsync("https://push.example.net/bad"));
    }

    [Fact]
    public async Task ADeviceThatKeepsFailingIsRemovedAfterTheConfiguredNumberOfFailures()
    {
        var store = _database.NewStore();
        var user = await _database.SeedUserAsync("Garson");
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/dead", ValidP256dh, ValidAuth, user, Guid.NewGuid()));
        var handler = new RecordingHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) };
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);
        var message = new WebPushMessage("Yeni sipariş", "Onay bekliyor", "alkaros-pending");

        Assert.Equal(10, WebPushSender.MaximumConsecutiveFailures);
        for (var attempt = 1; attempt < WebPushSender.MaximumConsecutiveFailures; attempt++)
            await sender.BroadcastAsync(message);
        Assert.Equal(1, await _database.SubscriptionCountAsync());

        await sender.BroadcastAsync(message);
        Assert.Equal(0, await _database.SubscriptionCountAsync());
    }

    [Fact]
    public async Task DevicesAreNotifiedConcurrentlyNotOneAfterTheOther()
    {
        var store = _database.NewStore();
        var user = await _database.SeedUserAsync("Garson");
        for (var index = 0; index < 4; index++)
            await store.SaveAsync(new PushSubscriptionRecord(
                Guid.NewGuid(), $"https://push.example.net/slow-{index}", ValidP256dh, ValidAuth, user, Guid.NewGuid()));
        var handler = new RecordingHandler { Delay = TimeSpan.FromMilliseconds(300) };
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);

        await sender.BroadcastAsync(new WebPushMessage("Yeni sipariş", "Onay bekliyor", "alkaros-pending"));

        Assert.Equal(4, handler.RequestedEndpoints.Count);
        Assert.True(handler.MaximumConcurrent > 1, "The sends ran strictly one after the other.");
    }

    [Fact]
    public async Task ADeactivatedUsersDeviceIsNotNotified()
    {
        var store = _database.NewStore();
        var leaver = await _database.SeedUserAsync("Ayrılan");
        var stayer = await _database.SeedUserAsync("Kalan");
        var terminalId = Guid.NewGuid();
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/leaver", ValidP256dh, ValidAuth, leaver, terminalId));
        await store.SaveAsync(new PushSubscriptionRecord(
            Guid.NewGuid(), "https://push.example.net/stayer", ValidP256dh, ValidAuth, stayer, terminalId));
        await _database.DeactivateUserAsync(leaver);
        var handler = new RecordingHandler();
        var sender = new WebPushSender(new HttpClient(handler), store, NullLogger<WebPushSender>.Instance);

        await sender.BroadcastAsync(new WebPushMessage("Yeni sipariş", "Onay bekliyor", "alkaros-pending"));

        Assert.Equal("https://push.example.net/stayer", Assert.Single(handler.RequestedEndpoints));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private int _concurrent;
        private int _maximumConcurrent;

        public ConcurrentQueue<string> RequestedEndpoints { get; } = new();

        public int MaximumConcurrent => _maximumConcurrent;

        public TimeSpan Delay { get; init; }

        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; init; }
            = _ => new HttpResponseMessage(HttpStatusCode.Created);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedEndpoints.Enqueue(request.RequestUri!.ToString());
            var now = Interlocked.Increment(ref _concurrent);
            int seen;
            while (now > (seen = Volatile.Read(ref _maximumConcurrent))
                   && Interlocked.CompareExchange(ref _maximumConcurrent, now, seen) != seen)
            {
            }

            try
            {
                if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
                return Respond(request);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }
    }
}
