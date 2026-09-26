using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using ALKAROS.Host.Experience.OnlineOrdering;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;
using ALKAROS.TestHelpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-ONL-001: the webhook endpoint over real HTTP and real Postgres. This exercises our own
/// endpoint and inbox; it proves nothing about Yemeksepeti's real deliveries, which have never
/// been received (V0-YSP-001 Blocked).
/// </summary>
public sealed class YemeksepetiWebhookHttpTests : IAsyncLifetime
{
    private const string Secret = "Bearer static-portal-token";

    private readonly OnlineOrderingTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private static string Body(string orderId) =>
        $$$"""{"order_id":"{{{orderId}}}","status":"RECEIVED","transport_type":"VENDOR_DELIVERY","sys":{"updated_at":"2026-09-25T18:00:00Z"}}""";

    [Fact]
    public async Task AStoredDeliveryIsAcknowledgedAndItsRetryIsAcknowledgedAgain()
    {
        await using var app = await StartAsync(configured: true);
        using var client = CreateClient(app);
        var body = Body(Guid.NewGuid().ToString("D"));

        using var first = await client.SendAsync(Delivery(body, Secret));
        using var retry = await client.SendAsync(Delivery(body, Secret));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("stored", await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Contains("duplicate", await retry.Content.ReadAsStringAsync());
        Assert.Equal(1, await _database.CountAllAsync());
    }

    [Fact]
    public async Task AWrongOrMissingSecretIsRefusedAndNothingIsStored()
    {
        await using var app = await StartAsync(configured: true);
        using var client = CreateClient(app);

        using var wrong = await client.SendAsync(Delivery(Body(Guid.NewGuid().ToString("D")), "Bearer guessed"));
        using var missing = await client.SendAsync(Delivery(Body(Guid.NewGuid().ToString("D")), null));

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(0, await _database.CountAllAsync());
    }

    [Fact]
    public async Task WithoutAConfiguredSecretTheEndpointIsClosed()
    {
        await using var app = await StartAsync(configured: false);
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Delivery(Body(Guid.NewGuid().ToString("D")), Secret));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, await _database.CountAllAsync());
    }

    [Fact]
    public async Task AMalformedBodyIsRefusedAndNothingIsStored()
    {
        await using var app = await StartAsync(configured: true);
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Delivery("""{"status":"RECEIVED"}""", Secret));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _database.CountAllAsync());
    }

    [Fact]
    public async Task AnUnauthenticatedDeliveryIsRefusedBeforeItsBodyIsConsidered()
    {
        // V12-RMD-007: authentication comes first, so even an oversized body from a stranger is a 401, not a 413.
        await using var app = await StartAsync(configured: true);
        using var client = CreateClient(app);
        var huge = "{\"order_id\":\"x\",\"status\":\"RECEIVED\",\"pad\":\"" + new string('a', YemeksepetiWebhookInbox.MaxBodyBytes) + "\"}";

        using var response = await client.SendAsync(Delivery(huge, "Bearer guessed"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await _database.CountAllAsync());
    }

    [Fact]
    public async Task TheSchemeOfTheSecretIsMatchedCaseInsensitively()
    {
        await using var app = await StartAsync(configured: true);
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Delivery(Body(Guid.NewGuid().ToString("D")), "bearer static-portal-token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await _database.CountAllAsync());
    }

    [Fact]
    public async Task AnOversizedBodyIsRefusedWithoutBeingBuffered()
    {
        await using var app = await StartAsync(configured: true);
        using var client = CreateClient(app);
        var huge = "{\"order_id\":\"x\",\"status\":\"RECEIVED\",\"pad\":\"" + new string('a', YemeksepetiWebhookInbox.MaxBodyBytes) + "\"}";

        using var response = await client.SendAsync(Delivery(huge, Secret));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, await _database.CountAllAsync());
    }

    private static HttpRequestMessage Delivery(string body, string? authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, YemeksepetiWebhookEndpoints.Route)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (authorization is not null)
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return request;
    }

    private async Task<WebApplication> StartAsync(bool configured)
    {
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        if (configured)
            secrets.Set(YemeksepetiWebhookInbox.WebhookSecret, Secret);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(secrets);
        builder.Services.AddYemeksepetiWebhookExperience();
        builder.Services.AddRateLimiter(limiter => limiter.AddPolicy("yemeksepeti-webhook", _ =>
            RateLimitPartition.GetNoLimiter("test")));
        var app = builder.Build();
        app.UseRateLimiter();
        app.MapYemeksepetiWebhookApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
