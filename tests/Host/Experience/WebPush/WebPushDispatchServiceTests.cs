using System.Collections.Concurrent;
using System.Net;
using ALKAROS.Host.Experience.WaiterNotifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.WebPush.Tests;

[Collection("Web push PostgreSQL HTTP")]
public sealed class WebPushDispatchServiceTests : IAsyncLifetime, IDisposable
{
    private const string ValidP256dh = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string ValidAuth = "BTBZMqHH6r4Tts7J_aSIgg";
    private static readonly WebPushMessage Message = new("Yeni sipariş", "Onay bekliyor", "alkaros-pending");

    private readonly WebPushTestDatabase _database = new();
    private WebPushDispatchService? _service;

    public Task InitializeAsync() => _database.InitializeAsync();

    public void Dispose() => _service?.Dispose();

    public async Task DisposeAsync()
    {
        if (_service is not null) await _service.StopAsync(CancellationToken.None);
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task QueuingReturnsAtOnceWhileThePushServiceIsStillAnswering()
    {
        var user = await _database.SeedUserAsync("Garson");
        await SubscribeAsync(user, "https://push.example.net/slow");
        var handler = new GatedHandler();
        var service = await StartAsync(handler);

        service.Broadcast(Message);

        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(handler.Gate.Task.IsCompleted);
        handler.Gate.SetResult();
        await WaitUntilAsync(() => handler.Completed == 1);
    }

    [Fact]
    public async Task ToUserOrBroadcastReachesOnlyThatUsersDevices()
    {
        var target = await _database.SeedUserAsync("Hedef");
        var other = await _database.SeedUserAsync("Diğer");
        await SubscribeAsync(target, "https://push.example.net/target");
        await SubscribeAsync(other, "https://push.example.net/other");
        var handler = new GatedHandler();
        handler.Gate.SetResult();
        var service = await StartAsync(handler);

        service.ToUserOrBroadcast(Message, target);

        await WaitUntilAsync(() => handler.Completed == 1);
        await Task.Delay(200);
        Assert.Equal(["https://push.example.net/target"], handler.Requested.ToArray());
    }

    [Fact]
    public async Task AFailedJobIsLoggedAndTheNextOneStillRuns()
    {
        var user = await _database.SeedUserAsync("Garson");
        await SubscribeAsync(user, "https://push.example.net/after-failure");
        var handler = new GatedHandler();
        handler.Gate.SetResult();
        var logger = new CaptureLogger();
        var service = await StartAsync(handler, logger);

        service.Enqueue(_ => throw new InvalidOperationException("The job broke."));
        await WaitUntilAsync(() => logger.Warnings == 1);
        service.Broadcast(Message);

        await WaitUntilAsync(() => handler.Completed == 1);
        Assert.Equal(1, logger.Warnings);
    }

    [Fact]
    public async Task AFailingHubDoesNotKeepTheClosedAppDevicesFromBeingNotified()
    {
        var user = await _database.SeedUserAsync("Garson");
        await SubscribeAsync(user, "https://push.example.net/phone");
        var handler = new GatedHandler();
        handler.Gate.SetResult();
        var service = await StartAsync(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => WaiterNotificationDispatch.SendAsync(
            new ThrowingHubContext(), new WaiterPresenceTracker(), service, targetUserId: null,
            WaiterOrderStatusHub.OrderItemReady, new object(), Message, CancellationToken.None));

        await WaitUntilAsync(() => handler.Completed == 1);
    }

    [Fact]
    public async Task ADispatchToOneWaiterQueuesThePushForThatWaitersDevicesOnly()
    {
        var target = await _database.SeedUserAsync("Hedef");
        var other = await _database.SeedUserAsync("Diğer");
        await SubscribeAsync(target, "https://push.example.net/waiter-target");
        await SubscribeAsync(other, "https://push.example.net/waiter-other");
        var handler = new GatedHandler();
        handler.Gate.SetResult();
        var service = await StartAsync(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => WaiterNotificationDispatch.SendAsync(
            new ThrowingHubContext(), new WaiterPresenceTracker(), service, target,
            WaiterOrderStatusHub.OrderItemReady, new object(), Message, CancellationToken.None));

        await WaitUntilAsync(() => handler.Completed == 1);
        await Task.Delay(200);
        Assert.Equal(["https://push.example.net/waiter-target"], handler.Requested.ToArray());
    }

    private async Task SubscribeAsync(Guid userId, string endpoint)
        => Assert.True(await _database.NewStore().SaveAsync(
            new PushSubscriptionRecord(Guid.NewGuid(), endpoint, ValidP256dh, ValidAuth, userId, Guid.NewGuid())));

    private async Task<WebPushDispatchService> StartAsync(GatedHandler handler, ILogger<WebPushDispatchService>? logger = null)
    {
        var sender = new WebPushSender(new HttpClient(handler), _database.NewStore(), NullLogger<WebPushSender>.Instance);
        _service = new WebPushDispatchService(sender, logger ?? NullLogger<WebPushDispatchService>.Instance);
        await _service.StartAsync(CancellationToken.None);
        return _service;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The background delivery did not happen in time.");
            await Task.Delay(25);
        }
    }

    private sealed class GatedHandler : HttpMessageHandler
    {
        private int _completed;

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<string> Requested { get; } = new();

        public int Completed => Volatile.Read(ref _completed);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Enqueue(request.RequestUri!.ToString());
            Started.TrySetResult();
            await Gate.Task.WaitAsync(cancellationToken);
            Interlocked.Increment(ref _completed);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }

    private sealed class CaptureLogger : ILogger<WebPushDispatchService>
    {
        private int _warnings;

        public int Warnings => Volatile.Read(ref _warnings);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Interlocked.Increment(ref _warnings);
        }
    }

    private sealed class ThrowingHubContext : IHubContext<WaiterOrderStatusHub>
    {
        public IHubClients Clients { get; } = new ThrowingClients();

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class ThrowingClients : IHubClients
    {
        public IClientProxy All => new ThrowingProxy();

        public IClientProxy Group(string groupName) => new ThrowingProxy();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Client(string connectionId) => throw new NotSupportedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
            => throw new NotSupportedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IClientProxy OthersInGroup(string groupName) => throw new NotSupportedException();

        public IClientProxy User(string userId) => throw new NotSupportedException();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class ThrowingProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The hub is down.");
    }
}
