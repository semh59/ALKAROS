using System.Security.Claims;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.InventoryReporting.Tests;

[Collection("Inventory reporting PostgreSQL HTTP")]
public sealed class LowStockAlertHubSessionTests : IAsyncLifetime
{
    private readonly InventoryReportingTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ARevokedManagerSessionIsDroppedByTheSweepAndNothingElseIs()
    {
        var sessions = new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance);
        var accepted = Connect(InventoryReportingTestDatabase.ViewerToken, sessions);
        var refused = Connect("not-a-real-token", sessions);
        await accepted.Hub.OnConnectedAsync();
        await refused.Hub.OnConnectedAsync();
        Assert.True(refused.Context.Aborted);
        Assert.False(accepted.Context.Aborted);
        Assert.Equal(1, sessions.Count);
        Assert.Equal(0, await sessions.AbortRevokedAsync(CancellationToken.None));

        await using var revoke = _database.DataSource.CreateCommand(
            "UPDATE identity.device_sessions SET revoked_at = now() WHERE device_id = 'manager:test';");
        await revoke.ExecuteNonQueryAsync();

        Assert.Equal(1, await sessions.AbortRevokedAsync(CancellationToken.None));
        Assert.True(accepted.Context.Aborted);
        await accepted.Hub.OnDisconnectedAsync(null);
        Assert.Equal(0, sessions.Count);
    }

    [Fact]
    public async Task DisconnectingReleasesTheTrackedConnection()
    {
        var sessions = new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance);
        var connection = Connect(InventoryReportingTestDatabase.ViewerToken, sessions);
        await connection.Hub.OnConnectedAsync();
        Assert.Equal(1, sessions.Count);

        await connection.Hub.OnDisconnectedAsync(null);

        Assert.Equal(0, sessions.Count);
    }

    private (LowStockAlertHub Hub, FakeContext Context) Connect(string token, HubSessionRegistry sessions)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Append("Cookie", $"alkaros.manager={token}");
        var context = new FakeContext(httpContext);
        return (new LowStockAlertHub(_database.DataSource, sessions) { Context = context }, context);
    }

    private sealed class FakeContext : HubCallerContext
    {
        private readonly FeatureCollection _features = new();

        public FakeContext(HttpContext httpContext)
            => _features.Set<IHttpContextFeature>(new HttpContextFeature(httpContext));

        public bool Aborted { get; private set; }

        public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features => _features;
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() => Aborted = true;

        private sealed class HttpContextFeature(HttpContext httpContext) : IHttpContextFeature
        {
            public HttpContext? HttpContext { get; set; } = httpContext;
        }
    }
}
