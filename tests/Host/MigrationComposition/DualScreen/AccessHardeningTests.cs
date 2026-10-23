using System.Net;
using System.Net.Http.Headers;
using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.SecurityAdministration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

[Collection("Host database password environment")]
public sealed class AccessHardeningTests : IDisposable
{
    private const string LoginLimitVariable = "ALKAROS_LOGIN_RATE_LIMIT_PERMITS";
    private readonly string? _originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");
    private readonly string? _originalLoginLimit = Environment.GetEnvironmentVariable(LoginLimitVariable);
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-hardening-test-{Guid.NewGuid():N}");

    public AccessHardeningTests()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", "test-password");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _originalPassword);
        Environment.SetEnvironmentVariable(LoginLimitVariable, _originalLoginLimit);
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task TheHostWaitsForInFlightWorkLongerThanTheDefaultButShorterThanTheContainerGrace()
    {
        await using var app = await StartAsync();

        var timeout = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Hosting.HostOptions>>()
            .Value.ShutdownTimeout;

        Assert.Equal(DualScreenApplication.ShutdownTimeout, timeout);
        Assert.True(timeout > TimeSpan.FromSeconds(30) && timeout < TimeSpan.FromSeconds(50));
    }

    [Fact]
    public async Task EveryApiRouteWithoutItsOwnPolicyIsBoundedPerClientAddress()
    {
        await using var app = await StartAsync(apiPermits: 3);
        using var client = CreateClient(app);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var allowed = await SendAsync(client, HttpMethod.Get, "/api/not-found", "198.51.100.61");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        using var limited = await SendAsync(client, HttpMethod.Get, "/api/not-found", "198.51.100.61");
        using var otherClient = await SendAsync(client, HttpMethod.Get, "/api/not-found", "198.51.100.62");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherClient.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task RoutesOutsideApiAreNotCountedAgainstTheApiDefault()
    {
        await using var app = await StartAsync(apiPermits: 1);
        using var client = CreateClient(app);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await SendAsync(client, HttpMethod.Get, "/index.html", "198.51.100.63");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task QrOrderRequestsAreBoundedPerAddressEvenWhenEverySessionHeaderIsDifferent()
    {
        const int limit = 3;
        await using var app = await StartAsync(qrPermits: limit);
        using var client = CreateClient(app);

        for (var attempt = 0; attempt < limit; attempt++)
        {
            using var allowed = await SendQrMenuAsync(client, "198.51.100.64", attempt);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        using var limited = await SendQrMenuAsync(client, "198.51.100.64", limit);
        using var otherAddress = await SendQrMenuAsync(client, "198.51.100.65", 0);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherAddress.StatusCode);
    }

    [Fact]
    public async Task TheLoginLimitOverrideCannotRaiseTheBoundPastItsCeiling()
    {
        Environment.SetEnvironmentVariable(LoginLimitVariable, "1000000");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var ceiling = DualScreenApplication.MaxLoginRateLimitPermits;

        for (var attempt = 0; attempt < ceiling; attempt++)
        {
            using var allowed = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login", "198.51.100.66", "{}");
            Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode);
        }

        using var limited = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login", "198.51.100.66", "{}");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedScreensaverUploadIsRefusedWithoutItsBodyBeingParsed()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/management/customer-display/screensaver")
        {
            Content = new StringContent("not a multipart body"),
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=missing");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.67");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ObjectsHoldingTheDatabasePasswordPrintNothingSensitive()
    {
        var options = new DualScreenOptions("Host=db;Username=app;Password=hunter2", _webRoot, "http://127.0.0.1:0");

        Assert.DoesNotContain("hunter2", options.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "hunter2", new HostDatabaseConnection("Host=db;Password=hunter2").ToString(), StringComparison.Ordinal);
        var psql = new PsqlOptions("postgresql://app@db/shop", Password: "hunter2");
        Assert.DoesNotContain("hunter2", psql.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("app@db", psql.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("postgresql://app@db:5432/shop", false)]
    [InlineData("postgresql://app:@db:5432/shop", false)]
    [InlineData("postgresql://app:secret@db:5432/shop", true)]
    [InlineData("not a url", false)]
    public void ADatabaseUrlWithAPasswordIsRecognised(string url, bool expected)
        => Assert.Equal(expected, PsqlScriptRunner.UrlCarriesPassword(url));

    [Fact]
    public void ThePsqlPasswordComesFromTheOptionsNeverFromTheUrl()
    {
        var withUrlPassword = PsqlScriptRunner.CreateStartInfo(
            new PsqlOptions("postgresql://app:fromurl@db:5432/shop", Password: "fromenv"));
        var urlOnly = PsqlScriptRunner.CreateStartInfo(new PsqlOptions("postgresql://app:fromurl@db:5432/shop"));

        Assert.Equal("fromenv", withUrlPassword.Environment["PGPASSWORD"]);
        Assert.False(urlOnly.Environment.TryGetValue("PGPASSWORD", out var leaked) && leaked == "fromurl");
    }

    [Fact]
    public async Task TheSweepAbortsOnlyTheConnectionsWhoseCheckNowFailsAndKeepsThoseItCannotCheck()
    {
        var registry = new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance);
        var revoked = new RecordingContext();
        var valid = new RecordingContext();
        var unreachable = new RecordingContext();
        registry.Track(revoked, _ => Task.FromResult(false));
        registry.Track(valid, _ => Task.FromResult(true));
        registry.Track(unreachable, _ => throw new InvalidOperationException("database down"));

        var aborted = await registry.AbortRevokedAsync(CancellationToken.None);

        Assert.Equal(1, aborted);
        Assert.True(revoked.Aborted);
        Assert.False(valid.Aborted);
        Assert.False(unreachable.Aborted);
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public async Task AFaultInOneSweepTickIsLoggedInsteadOfStoppingTheHost()
    {
        var registry = new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance);
        registry.Track(new RecordingContext(), _ => throw new ArgumentException("unexpected"));
        var sweeper = new HubSessionSweeper(registry, NullLogger<HubSessionSweeper>.Instance);

        await sweeper.SweepOnceAsync(CancellationToken.None);

        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public async Task AReleasedConnectionIsNoLongerChecked()
    {
        var registry = new HubSessionRegistry(NullLogger<HubSessionRegistry>.Instance);
        var context = new RecordingContext();
        registry.Track(context, _ => Task.FromResult(false));

        registry.Release(context.ConnectionId);

        Assert.Equal(0, await registry.AbortRevokedAsync(CancellationToken.None));
        Assert.False(context.Aborted);
    }

    [Fact]
    public void TheSweeperAndRegistryAreRegisteredOnceHoweverManyAreasAskForThem()
    {
        var services = new ServiceCollection().AddLogging();

        services.AddHubSessionTracking().AddHubSessionTracking();
        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<HubSessionRegistry>(), provider.GetRequiredService<HubSessionRegistry>());
        Assert.Single(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<HubSessionSweeper>());
    }

    private async Task<WebApplication> StartAsync(int apiPermits = 1200, int qrPermits = 300)
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            "Host=localhost;Database=unused;Username=unused;Password=unused",
            _webRoot,
            "http://127.0.0.1:0",
            [IPAddress.Loopback],
            ApiRateLimitPermits: apiPermits,
            QrOrderAddressPermits: qrPermits));
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, string forwardedFor, string? json = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendQrMenuAsync(HttpClient client, string forwardedFor, int sessionNumber)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/menu");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Alkaros-Qr-Session", $"session-{sessionNumber}");
        return await client.SendAsync(request);
    }

    private sealed class RecordingContext : HubCallerContext
    {
        public bool Aborted { get; private set; }

        public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() => Aborted = true;
    }
}
