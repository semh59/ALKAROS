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
    public async Task AnAuthenticatedConnectionJoinsItsOwnUserGroup()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var hub = new WaiterOrderStatusHub(new DualScreenStore(_database.DataSource));
        var groups = new FakeGroupManager();
        var context = new FakeHubCallerContext(cookie, terminalId);
        hub.Context = context;
        hub.Groups = groups;

        await hub.OnConnectedAsync();

        Assert.False(context.Aborted);
        var joined = Assert.Single(groups.AddedGroups);
        Assert.Equal(context.ConnectionId, joined.ConnectionId);
        Assert.Equal(WaiterOrderStatusHub.GroupName(userId), joined.GroupName);
    }

    [Fact]
    public async Task AnUnauthenticatedConnectionIsAbortedAndJoinsNoGroup()
    {
        var terminalId = Guid.NewGuid();
        var hub = new WaiterOrderStatusHub(new DualScreenStore(_database.DataSource));
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
