using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Orders.Integration;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace ALKAROS.Host.Experience.PendingOrderNotifications.Tests;

/// <summary>
/// V1-RMD-202: a brand-new QR order has no serving waiter yet, so
/// <see cref="SignalRPendingOrderAnnouncer"/> has to pick one — among users
/// holding orders.send with a live device session, whoever carries the
/// fewest open orders, ties broken by whoever has gone longest without a
/// new one. No candidate at all falls back to the original flat broadcast.
/// </summary>
[Collection("Pending order notifications PostgreSQL")]
public sealed class SignalRPendingOrderAnnouncerTests : IAsyncLifetime
{
    private readonly PendingOrderNotificationsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task TargetsTheOpenSessionWaiterWithTheFewestActiveOrders()
    {
        var busyWaiter = await _database.SeedWaiterAsync("Meşgul Garson", hasOpenSession: true);
        var freeWaiter = await _database.SeedWaiterAsync("Boş Garson", hasOpenSession: true);
        await _database.SeedOrderAsync(busyWaiter, "Preparing", DateTimeOffset.UtcNow.AddMinutes(-5));
        await _database.SeedOrderAsync(busyWaiter, "Ready", DateTimeOffset.UtcNow.AddMinutes(-3));

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(busyWaiter);
        presence.Connected(freeWaiter);
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource));

        await announcer.AnnounceAsync(NewAnnouncement());

        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(freeWaiter), group);
        Assert.Equal(0, hub.Clients.AllCalls);
    }

    [Fact]
    public async Task FallsBackToBroadcastWhenTheResolvedWaiterHasNoLiveHubConnection()
    {
        // V1-RMD-203: the regression this guards against - a Cashier/
        // PosTerminal session satisfies the SQL candidacy check (a live
        // device_sessions row) but neither client ever connects to this
        // hub, so targeting them would silently reach nobody.
        var waiter = await _database.SeedWaiterAsync("Oturumu Açık Ama Hub'a Bağlı Değil", hasOpenSession: true);
        _ = waiter;

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource));

        await announcer.AnnounceAsync(NewAnnouncement());

        Assert.Equal(1, hub.Clients.AllCalls);
        Assert.Empty(hub.Clients.GroupCalls);
    }

    [Fact]
    public async Task NeverTargetsAWaiterWithoutAnOpenSession()
    {
        var loggedOutButIdle = await _database.SeedWaiterAsync("Oturumu Kapalı", hasOpenSession: false);
        var loggedInButBusier = await _database.SeedWaiterAsync("Oturumu Açık", hasOpenSession: true);
        await _database.SeedOrderAsync(loggedInButBusier, "Accepted", DateTimeOffset.UtcNow.AddMinutes(-1));
        _ = loggedOutButIdle;

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(loggedInButBusier);
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource));

        await announcer.AnnounceAsync(NewAnnouncement());

        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(loggedInButBusier), group);
    }

    [Fact]
    public async Task ATieOnActiveLoadGoesToWhoeverWentLongestWithoutANewOrder()
    {
        var recentlyAssigned = await _database.SeedWaiterAsync("Yeni Atanan", hasOpenSession: true);
        var longIdle = await _database.SeedWaiterAsync("Uzun Süredir Beklemede", hasOpenSession: true);
        // Both carry exactly one open order, so active_load ties at 1 - the
        // tiebreak (oldest last-assigned) must decide, not insertion order.
        await _database.SeedOrderAsync(recentlyAssigned, "Preparing", DateTimeOffset.UtcNow.AddMinutes(-1));
        await _database.SeedOrderAsync(longIdle, "Preparing", DateTimeOffset.UtcNow.AddHours(-3));

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(recentlyAssigned);
        presence.Connected(longIdle);
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource));

        await announcer.AnnounceAsync(NewAnnouncement());

        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(longIdle), group);
    }

    [Fact]
    public async Task ANeverAssignedWaiterOutranksOneWithAnyOpenOrder()
    {
        var everAssigned = await _database.SeedWaiterAsync("Bir Kere Atandı", hasOpenSession: true);
        var neverAssigned = await _database.SeedWaiterAsync("Hiç Atanmadı", hasOpenSession: true);
        await _database.SeedOrderAsync(everAssigned, "Served", DateTimeOffset.UtcNow.AddDays(-1));
        _ = neverAssigned;

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(everAssigned);
        presence.Connected(neverAssigned);
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource));

        await announcer.AnnounceAsync(NewAnnouncement());

        // Both have active_load 0 (the one order is terminal/Served); the
        // never-assigned waiter's null last-assigned-at must sort first.
        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(neverAssigned), group);
    }

    [Fact]
    public async Task FallsBackToBroadcastWhenNobodyQualifies()
    {
        await _database.SeedWaiterAsync("Oturumu Kapalı", hasOpenSession: false);

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource));

        await announcer.AnnounceAsync(NewAnnouncement());

        Assert.Equal(1, hub.Clients.AllCalls);
        Assert.Empty(hub.Clients.GroupCalls);
    }

    private static PendingOrderAnnouncement NewAnnouncement() => new(
        Guid.NewGuid(), Guid.NewGuid(), "7", 2, 250.00m, DateTimeOffset.UtcNow);
}

[CollectionDefinition("Pending order notifications PostgreSQL", DisableParallelization = true)]
public sealed class PendingOrderNotificationsPostgresqlDefinition;

internal sealed class RecordingHubContext : IHubContext<WaiterOrderStatusHub>
{
    public RecordingClients Clients { get; } = new();

    IHubClients IHubContext<WaiterOrderStatusHub>.Clients => Clients;

    public IGroupManager Groups => throw new NotSupportedException();
}

internal sealed class RecordingClients : IHubClients
{
    public int AllCalls { get; private set; }

    public List<string> GroupCalls { get; } = [];

    public IClientProxy All
    {
        get
        {
            AllCalls++;
            return new RecordingProxy();
        }
    }

    public IClientProxy Group(string groupName)
    {
        GroupCalls.Add(groupName);
        return new RecordingProxy();
    }

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

internal sealed class RecordingProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
