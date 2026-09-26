using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace ALKAROS.Host.Experience.HelpRequests.Tests;

/// <summary>
/// V1-RMD-289: HelpRequestHub's own connection gate had no test at all (HelpRequestHttpTests.cs's own doc
/// comment: "The SignalR broadcast side ... is not independently asserted here"). Drives OnConnectedAsync
/// directly against a real Postgres-backed ManagementSessionLookup, with a fake HubCallerContext/IGroupManager
/// (the officially supported way to unit-test a Hub - Context/Groups are public settable properties) rather
/// than a live network round-trip, so no new client-side SignalR package is needed for this task's Owned
/// surface (Host-only).
/// </summary>
[Collection("Help request PostgreSQL Hub")]
public sealed class HelpRequestHubTests : IAsyncLifetime
{
    private readonly HelpRequestTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoCookieAtAllAbortsTheConnection()
    {
        var (hub, context, groups) = Build(cookie: null);

        await hub.OnConnectedAsync();

        Assert.True(context.Aborted);
        Assert.Empty(groups.Memberships);
    }

    [Fact]
    public async Task ACashierSessionCannotConnect()
    {
        var (_, cookie) = await _database.SeedCashierSessionAsync(Guid.NewGuid());
        var rawToken = cookie.Split('=', 2)[1];
        var (hub, context, groups) = Build($"alkaros.manager={rawToken}");

        await hub.OnConnectedAsync();

        Assert.True(context.Aborted);
        Assert.Empty(groups.Memberships);
    }

    [Fact]
    public async Task AManagerSessionConnectsAndJoinsTheRecipientsGroup()
    {
        var (_, rawToken) = await _database.SeedManagementSessionAsync("manager");
        var (hub, context, groups) = Build($"alkaros.manager={rawToken}");

        await hub.OnConnectedAsync();

        Assert.False(context.Aborted);
        Assert.Contains((context.ConnectionId, HelpRequestHub.RecipientsGroup), groups.Memberships);
    }

    [Fact]
    public async Task ASupervisorSessionAlsoConnectsAndJoinsTheRecipientsGroup()
    {
        var (_, rawToken) = await _database.SeedManagementSessionAsync("supervisor");
        var (hub, context, groups) = Build($"alkaros.manager={rawToken}");

        await hub.OnConnectedAsync();

        Assert.False(context.Aborted);
        Assert.Contains((context.ConnectionId, HelpRequestHub.RecipientsGroup), groups.Memberships);
    }

    private (HelpRequestHub Hub, FakeHubCallerContext Context, FakeGroupManager Groups) Build(string? cookie)
    {
        var httpContext = new DefaultHttpContext();
        if (cookie is not null)
            httpContext.Request.Headers.Append("Cookie", cookie);

        var context = new FakeHubCallerContext(httpContext);
        var groups = new FakeGroupManager();
        var hub = new HelpRequestHub(_database.DataSource) { Context = context, Groups = groups };
        return (hub, context, groups);
    }
}

[CollectionDefinition("Help request PostgreSQL Hub", DisableParallelization = true)]
public sealed class HelpRequestHubPostgresqlDefinition;

/// <summary>Minimal fake - only what HelpRequestHub.OnConnectedAsync actually reads.</summary>
internal sealed class FakeHubCallerContext : HubCallerContext
{
    private readonly FeatureCollection _features = new();

    public FakeHubCallerContext(HttpContext httpContext)
    {
        // HubCallerContextExtensions.GetHttpContext() reads THIS feature (SignalR's own, not the generic
        // ASP.NET Core Http.Abstractions one of the same short name) - confirmed by reflecting the real
        // assembly, since it is not documented anywhere obvious.
        _features.Set<IHttpContextFeature>(new SimpleHttpContextFeature(httpContext));
    }

    private sealed class SimpleHttpContextFeature(HttpContext httpContext) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }

    public bool Aborted { get; private set; }

    public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");
    public override string? UserIdentifier => null;
    public override ClaimsPrincipal? User => null;
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features => _features;
    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort() => Aborted = true;
}

/// <summary>Records every group join, keyed (connectionId, groupName) - enough to prove which connection joined which group.</summary>
internal sealed class FakeGroupManager : IGroupManager
{
    public List<(string ConnectionId, string GroupName)> Memberships { get; } = [];

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Memberships.Add((connectionId, groupName));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Memberships.RemoveAll(m => m.ConnectionId == connectionId && m.GroupName == groupName);
        return Task.CompletedTask;
    }
}
