using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Identity.Authorization;
using ALKAROS.Orders.Integration;
using ALKAROS.Settings.TypedSettings;
using ALKAROS.Settings.WaiterMaxActiveTables;
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
    private SettingsService _settings = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _settings = new SettingsService(new PostgresSettingsRepository(_database.DataSource, new SettingValidator()));
    }

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
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

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
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

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
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

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
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

        await announcer.AnnounceAsync(NewAnnouncement());

        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(longIdle), group);
    }

    [Fact]
    public async Task ACandidateAtTheConfiguredCapIsExcludedFromThePoolEntirely()
    {
        // V1-SET-006: with the cap set to 2 and the only candidate's own
        // active_load already at 2, the resolver must find nobody at all
        // (not merely rank this waiter last) - same observable outcome as
        // FallsBackToBroadcastWhenNobodyQualifies, but caused by the cap
        // rather than by session/permission eligibility.
        var atCap = await _database.SeedWaiterAsync("Tavanda", hasOpenSession: true);
        await _database.SeedOrderAsync(atCap, "Preparing", DateTimeOffset.UtcNow.AddMinutes(-5));
        await _database.SeedOrderAsync(atCap, "Preparing", DateTimeOffset.UtcNow.AddMinutes(-4));

        await WaiterMaxActiveTablesSetting.EnsureRegisteredAsync(_settings);
        var settingRecord = await _settings.GetRecordAsync(WaiterMaxActiveTablesSetting.Key);
        await _settings.SetValueAsync(WaiterMaxActiveTablesSetting.Key, 2, settingRecord!.RowVersion);

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(atCap);
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

        await announcer.AnnounceAsync(NewAnnouncement());

        Assert.Equal(1, hub.Clients.AllCalls);
        Assert.Empty(hub.Clients.GroupCalls);
    }

    [Fact]
    public async Task ACandidateJustUnderTheCapIsStillSuggested()
    {
        // The mirror of the above: one order below the same cap must still
        // be a real candidate, proving the filter is a strict "< cap", not
        // an off-by-one that excludes everyone at or near it.
        var underCap = await _database.SeedWaiterAsync("Tavanın Altında", hasOpenSession: true);
        await _database.SeedOrderAsync(underCap, "Preparing", DateTimeOffset.UtcNow.AddMinutes(-5));

        await WaiterMaxActiveTablesSetting.EnsureRegisteredAsync(_settings);
        var settingRecord = await _settings.GetRecordAsync(WaiterMaxActiveTablesSetting.Key);
        await _settings.SetValueAsync(WaiterMaxActiveTablesSetting.Key, 2, settingRecord!.RowVersion);

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(underCap);
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

        await announcer.AnnounceAsync(NewAnnouncement());

        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(underCap), group);
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
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

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
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

        await announcer.AnnounceAsync(NewAnnouncement());

        Assert.Equal(1, hub.Clients.AllCalls);
        Assert.Empty(hub.Clients.GroupCalls);
    }

    [Fact]
    public async Task ATiedLoadGoesToWhoeverServedTheSameZoneMostRecently()
    {
        var gardenZone = await _database.SeedZoneAsync("Bahçe");
        var indoorZone = await _database.SeedZoneAsync("İç Mekân");
        var gardenTable = await _database.SeedTableAsync(gardenZone, "B-1");
        var anotherGardenTable = await _database.SeedTableAsync(gardenZone, "B-2");
        var indoorTable = await _database.SeedTableAsync(indoorZone, "I-1");

        var gardenWaiter = await _database.SeedWaiterAsync("Bahçeci Garson", hasOpenSession: true);
        var indoorWaiter = await _database.SeedWaiterAsync("İç Mekân Garsonu", hasOpenSession: true);
        // Both tied at active_load 1. Deliberately the OPPOSITE of what the
        // rotation tiebreak alone would pick - the indoor waiter's own order
        // is much older (so, ignoring zone, indoor would win "longest idle
        // wins ties") - so this only passes because the zone tier is
        // checked BEFORE that tiebreak, not because of it or insertion order.
        await _database.SeedOrderAsync(indoorWaiter, "Preparing", DateTimeOffset.UtcNow.AddHours(-3), indoorTable);
        await _database.SeedOrderAsync(gardenWaiter, "Preparing", DateTimeOffset.UtcNow.AddMinutes(-10), anotherGardenTable);

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(gardenWaiter);
        presence.Connected(indoorWaiter);
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

        // A NEW guest order from a garden table - the garden waiter's own
        // most recent open order is also in the garden zone.
        await announcer.AnnounceAsync(NewAnnouncement(gardenTable));

        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(gardenWaiter), group);
    }

    [Fact]
    public async Task ZoneIsAPreferenceNotAFilterTheOnlyCandidateStillWinsWithNoZoneMatch()
    {
        var gardenZone = await _database.SeedZoneAsync("Bahçe");
        var indoorZone = await _database.SeedZoneAsync("İç Mekân");
        var gardenTable = await _database.SeedTableAsync(gardenZone, "B-1");
        var indoorTable = await _database.SeedTableAsync(indoorZone, "I-1");

        // The only waiter on duty has only ever served the indoor zone.
        var onlyWaiter = await _database.SeedWaiterAsync("Tek Garson", hasOpenSession: true);
        await _database.SeedOrderAsync(onlyWaiter, "Preparing", DateTimeOffset.UtcNow.AddMinutes(-5), indoorTable);

        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(onlyWaiter);
        var announcer = new SignalRPendingOrderAnnouncer(hub, presence, new SuggestedWaiterResolver(_database.DataSource, new PostgresRoleRepository(_database.DataSource), _settings));

        // A garden-table order still must reach the only real candidate.
        await announcer.AnnounceAsync(NewAnnouncement(gardenTable));

        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(onlyWaiter), group);
    }

    private static PendingOrderAnnouncement NewAnnouncement(Guid? tableId = null) => new(
        Guid.NewGuid(), tableId ?? Guid.NewGuid(), "7", 2, 250.00m, DateTimeOffset.UtcNow);
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
