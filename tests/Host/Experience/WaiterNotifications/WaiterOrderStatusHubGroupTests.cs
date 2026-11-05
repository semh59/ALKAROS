using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace ALKAROS.Host.Experience.WaiterNotifications.Tests;

/// <summary>
/// V1-RMD-201: <see cref="WaiterOrderStatusHub.OnConnectedAsync"/> now joins
/// its own per-user group so a later notification can target one waiter
/// instead of broadcasting. Exercises the hub directly (documented
/// ASP.NET Core SignalR unit-testing pattern: <c>Context</c>/<c>Groups</c>
/// are public settable properties) against a real seeded session, rather
/// than a live SignalR client connection.
/// </summary>
[Collection("Waiter notifications PostgreSQL")]
public sealed class WaiterOrderStatusHubGroupTests : IAsyncLifetime
{
    private readonly WaiterNotificationsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AnAuthenticatedConnectionJoinsItsOwnUserGroupAndIsMarkedPresent()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var presence = new WaiterPresenceTracker();
        var hub = new WaiterOrderStatusHub(new DualScreenStore(_database.DataSource), presence, new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance));
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(cookie, terminalId);
        hub.Context = context;
        hub.Groups = groups;

        await hub.OnConnectedAsync();

        Assert.False(context.Aborted);
        var joined = Assert.Single(groups.AddedGroups);
        Assert.Equal(context.ConnectionId, joined.ConnectionId);
        Assert.Equal(WaiterOrderStatusHub.GroupName(userId), joined.GroupName);
        Assert.True(presence.IsConnected(userId));
    }

    [Fact]
    public async Task OnlyASessionThatMayReadTheTillQueueJoinsTheQueueGroup()
    {
        var terminalId = Guid.NewGuid();
        var (allowedUser, allowedCookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (_, deniedCookie) = await _database.SeedCashierSessionAsync(terminalId);
        await GrantOrdersCreateAsync(allowedUser);

        var allowedGroups = await ConnectAsync(allowedCookie, terminalId);
        var deniedGroups = await ConnectAsync(deniedCookie, terminalId);

        Assert.Contains(allowedGroups.AddedGroups, joined => joined.GroupName == WaiterOrderStatusHub.CashierQueueGroup);
        Assert.DoesNotContain(deniedGroups.AddedGroups, joined => joined.GroupName == WaiterOrderStatusHub.CashierQueueGroup);
    }

    private async Task<FakeGroupManager> ConnectAsync(string cookie, Guid terminalId)
    {
        var groups = new FakeGroupManager();
        var hub = new WaiterOrderStatusHub(
            new DualScreenStore(_database.DataSource), new WaiterPresenceTracker(),
            new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance));
        hub.Context = new FakeHubCallerContext(cookie, terminalId);
        hub.Groups = groups;
        await hub.OnConnectedAsync();
        return groups;
    }

    private async Task GrantOrdersCreateAsync(Guid userId)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO identity.permissions (permission_id, code, name) VALUES (gen_random_uuid(), 'orders.create', 'Create orders') ON CONFLICT (code) DO NOTHING;
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Hub Test Role');
            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT @role_permission_id, @role_id, permission_id FROM identity.permissions WHERE code = 'orders.create';
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@user_role_id, @user_id, @role_id);
            """);
        command.Parameters.AddWithValue("role_id", Guid.NewGuid());
        command.Parameters.AddWithValue("role_code", "hub-test-role-" + Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("role_permission_id", Guid.NewGuid());
        command.Parameters.AddWithValue("user_role_id", Guid.NewGuid());
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task DisconnectingMarksTheUserNoLongerPresent()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var presence = new WaiterPresenceTracker();
        var hub = new WaiterOrderStatusHub(new DualScreenStore(_database.DataSource), presence, new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance));
        hub.Context = new FakeHubCallerContext(cookie, terminalId);
        hub.Groups = new FakeGroupManager();

        await hub.OnConnectedAsync();
        Assert.True(presence.IsConnected(userId));

        await hub.OnDisconnectedAsync(null);

        Assert.False(presence.IsConnected(userId));
    }

    [Fact]
    public async Task ADeactivatedUserIsDroppedByTheSweep()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var sessions = new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance);
        var hub = new WaiterOrderStatusHub(new DualScreenStore(_database.DataSource), new WaiterPresenceTracker(), sessions);
        var context = new FakeHubCallerContext(cookie, terminalId);
        hub.Context = context;
        hub.Groups = new FakeGroupManager();
        await hub.OnConnectedAsync();
        Assert.Equal(0, await sessions.AbortRevokedAsync(CancellationToken.None));
        Assert.False(context.Aborted);

        await using var deactivate = _database.DataSource.CreateCommand("UPDATE identity.users SET active = false WHERE user_id = @id;");
        deactivate.Parameters.AddWithValue("id", userId);
        await deactivate.ExecuteNonQueryAsync();

        Assert.Equal(1, await sessions.AbortRevokedAsync(CancellationToken.None));
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task AnUnauthenticatedConnectionIsAbortedAndJoinsNoGroup()
    {
        var terminalId = Guid.NewGuid();
        var hub = new WaiterOrderStatusHub(new DualScreenStore(_database.DataSource), new WaiterPresenceTracker(), new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance));
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(
            $"{DualScreenApplication.CashierCookieName}=not-a-real-token", terminalId);
        hub.Context = context;
        hub.Groups = groups;

        await hub.OnConnectedAsync();

        Assert.True(context.Aborted);
        Assert.Empty(groups.AddedGroups);
    }

    [Fact]
    public void GroupNameIsStablePerUserAndDistinctAcrossUsers()
    {
        var userId = Guid.NewGuid();

        Assert.Equal(WaiterOrderStatusHub.GroupName(userId), WaiterOrderStatusHub.GroupName(userId));
        Assert.NotEqual(WaiterOrderStatusHub.GroupName(userId), WaiterOrderStatusHub.GroupName(Guid.NewGuid()));
    }
}

[CollectionDefinition("Waiter notifications PostgreSQL", DisableParallelization = true)]
public sealed class WaiterNotificationsPostgresqlDefinition;

internal sealed class FakeGroupManager : IGroupManager
{
    public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = [];

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        AddedGroups.Add((connectionId, groupName));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class FakeHubCallerContext : HubCallerContext
{
    public FakeHubCallerContext(string cookieHeader, Guid terminalId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Append("Cookie", cookieHeader);
        httpContext.Request.QueryString = new QueryString($"?terminalId={terminalId:D}");
        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new SimpleHttpContextFeature(httpContext));
        Features = features;
        ConnectionId = Guid.NewGuid().ToString("N");
    }

    public bool Aborted { get; private set; }
    public override string ConnectionId { get; }
    public override string? UserIdentifier => null;
    public override ClaimsPrincipal? User => null;
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features { get; }
    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort() => Aborted = true;
}

/// <summary>The concrete <c>HttpContextFeature</c> ASP.NET Core normally installs is not public.</summary>
internal sealed class SimpleHttpContextFeature : IHttpContextFeature
{
    public SimpleHttpContextFeature(HttpContext httpContext)
    {
        HttpContext = httpContext;
    }

    public HttpContext? HttpContext { get; set; }
}

/// <summary>The till queue announcement reaches only the sessions that joined the queue group.</summary>
public sealed class SignalRCashierQueueAnnouncerTests
{
    [Fact]
    public async Task TheAnnouncementGoesToTheQueueGroupOnly()
    {
        var hub = new RecordingHub();
        var orderId = Guid.NewGuid();

        await new SignalRCashierQueueAnnouncer(hub).AnnouncePendingChecksChangedAsync("Cancelled", orderId, null);

        var sent = Assert.Single(hub.Sent);
        Assert.Equal((WaiterOrderStatusHub.CashierQueueGroup, WaiterOrderStatusHub.PendingChecksChanged), (sent.Group, sent.Method));
        Assert.Equal(new PendingChecksChangedV1("Cancelled", orderId, null), sent.Payload);
    }

    private sealed class RecordingHub : IHubContext<WaiterOrderStatusHub>
    {
        public List<(string Group, string Method, object? Payload)> Sent { get; } = [];

        public IHubClients Clients => new RecordingClients(Sent);

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class RecordingClients(List<(string Group, string Method, object? Payload)> sent) : IHubClients
    {
        public IClientProxy All => throw new NotSupportedException();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Client(string connectionId) => throw new NotSupportedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IClientProxy Group(string groupName) => new RecordingProxy(groupName, sent);

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IClientProxy User(string userId) => throw new NotSupportedException();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingProxy(string group, List<(string Group, string Method, object? Payload)> sent) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            sent.Add((group, method, args[0]));
            return Task.CompletedTask;
        }
    }
}
