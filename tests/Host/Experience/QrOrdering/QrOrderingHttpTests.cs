using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.QrOrdering.Tests;

/// <summary>
/// V12-CWB-001: the relay-facing session-issue and menu-read endpoints.
/// Unlike NFC's own HTTP tests, every request here presents either a raw
/// table token or a customer session token — there is no LAN-only trust
/// shortcut on this surface.
/// </summary>
[Collection("QR ordering PostgreSQL HTTP")]
public sealed class QrOrderingHttpTests : IAsyncLifetime
{
    private readonly QrOrderingTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AnActiveTableTokenIssuesASessionScopedToItsTable()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await PostSessionAsync(client, rawToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QrSessionIssueResponse>();
        Assert.Equal(tableId, body!.TableId);
        Assert.False(string.IsNullOrWhiteSpace(body.SessionToken));
        Assert.Equal(1, await _database.NonceCountAsync());
    }

    [Fact]
    public async Task AnUnknownTableTokenIsRejectedWithATurkishMessage()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await PostSessionAsync(client, "alkaros-table-token:does-not-exist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("QR kodu tanınmadı", text);
        Assert.DoesNotContain("NOT_FOUND", text);
    }

    [Fact]
    public async Task ARevokedTableTokenIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedRevokedTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await PostSessionAsync(client, rawToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("artık geçerli değil", text);
    }

    [Fact]
    public async Task AnExpiredTableTokenIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedExpiredTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await PostSessionAsync(client, rawToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("süresi doldu", text);
    }

    [Fact]
    public async Task AStaleRequestTimestampIsRejected()
    {
        // V12-QRS-002: RelayRequestValidator.DefaultTimestampWindow is 2
        // minutes either side of server time.
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/qr/sessions",
            new QrSessionIssueRequest(rawToken, Guid.NewGuid(), DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("zaman aşımına", text);
    }

    [Fact]
    public async Task ReplayingTheExactSameRequestIsRejectedTheSecondTime()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var nonce = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        using var first = await client.PostAsJsonAsync(
            "/api/v1/qr/sessions", new QrSessionIssueRequest(rawToken, nonce, timestamp));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var replay = await client.PostAsJsonAsync(
            "/api/v1/qr/sessions", new QrSessionIssueRequest(rawToken, nonce, timestamp));

        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        var text = await replay.Content.ReadAsStringAsync();
        Assert.Contains("zaten işlendi", text);
    }

    [Fact]
    public async Task AnEmptyTableTokenIsRejectedWithATurkishMessage()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/qr/sessions", new QrSessionIssueRequest(string.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("QR kodu okunamadı", text);
        Assert.DoesNotContain("cannot be", text);
    }

    [Fact]
    public async Task AValidSessionListsAvailableProductsOnTheMenu()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await _database.SeedProductAsync("Mercimek Çorbası", 70m);
        await _database.SeedProductAsync("Izgara Köfte", 320m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/menu");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<CatalogProductDto>>();
        Assert.Equal(2, products!.Count);
        Assert.Contains(products, p => p.Name == "Mercimek Çorbası" && p.UnitPrice == 70m);
    }

    /// <summary>
    /// A manager-suspended ("86'd") product must not appear on the public
    /// menu either — same real-time toggle V1-RMD-128 fixed for NFC's own
    /// self-service path.
    /// </summary>
    [Fact]
    public async Task AnUnavailableProductIsHiddenFromTheMenu()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await _database.SeedProductAsync("Tükendi", 60m, isAvailable: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/menu");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        var products = await response.Content.ReadFromJsonAsync<List<CatalogProductDto>>();
        Assert.Empty(products!);
    }

    [Fact]
    public async Task AMissingSessionHeaderIsRejectedWithATurkishMessage()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/api/v1/qr/menu");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Oturumunuz bulunamadı", text);
        Assert.DoesNotContain("NOT_FOUND", text);
    }

    [Fact]
    public async Task AnUnknownSessionTokenIsRejected()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/menu");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, "does-not-exist");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> IssueSessionAsync(HttpClient client, string rawToken)
    {
        using var response = await PostSessionAsync(client, rawToken);
        var body = await response.Content.ReadFromJsonAsync<QrSessionIssueResponse>();
        return body!.SessionToken;
    }

    private static Task<HttpResponseMessage> PostSessionAsync(HttpClient client, string rawToken) =>
        client.PostAsJsonAsync("/api/v1/qr/sessions", new QrSessionIssueRequest(rawToken, Guid.NewGuid(), DateTimeOffset.UtcNow));

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        // MapQrOrderingApi requires the "qr-session"/"qr-order" named
        // policies to exist; the real Host (DualScreenApplication) registers
        // them with real windows, this standalone test host just needs them
        // present (same reasoning as AddNfcOrderingExperience's own test).
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("qr-session", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test"));
            options.AddPolicy("qr-order", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test"));
        });
        builder.Services.AddQrOrderingExperience();
        var app = builder.Build();
        app.UseRateLimiter();
        app.MapQrOrderingApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}

[CollectionDefinition("QR ordering PostgreSQL HTTP", DisableParallelization = true)]
public sealed class QrOrderingPostgresqlDefinition;
