using System.Net;
using System.Net.Http.Json;
using System.Text;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.WebPush.Tests;

/// <summary>
/// V1-WTR-011: the subscription surface over real HTTP. The crypto tests
/// prove the body is right; these prove the endpoints are actually mapped,
/// gated by a session, and that a re-subscribing device does not accumulate
/// rows.
/// </summary>
[Collection("Web push PostgreSQL HTTP")]
public sealed class WebPushHttpTests : IAsyncLifetime
{
    private const string ValidP256dh = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string ValidAuth = "BTBZMqHH6r4Tts7J_aSIgg";

    private readonly WebPushTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        var terminalId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            SubscriptionsPath(terminalId),
            new SavePushSubscriptionRequestV1("https://push.example.net/x", ValidP256dh, ValidAuth));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await _database.SubscriptionCountAsync());
    }

    [Fact]
    public async Task ThePublicKeyIsCreatedOnceAndThenStaysTheSame()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var first = await client.SendAsync(GetRequest(PublicKeyPath(terminalId), cookie));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstKey = await first.Content.ReadFromJsonAsync<PushPublicKeyResponseV1>();

        using var second = await client.SendAsync(GetRequest(PublicKeyPath(terminalId), cookie));
        var secondKey = await second.Content.ReadFromJsonAsync<PushPublicKeyResponseV1>();

        // Regenerating the pair would silently invalidate every subscription
        // already held by a device, so the identity must be stable.
        Assert.Equal(firstKey!.PublicKey, secondKey!.PublicKey);
        Assert.Equal(1, await _database.VapidRowCountAsync());

        // 65-byte uncompressed P-256 point, which is what the browser's
        // applicationServerKey expects.
        Assert.Equal(65, WebPushCrypto.FromBase64Url(firstKey.PublicKey).Length);
    }

    [Fact]
    public async Task ADeviceThatResubscribesUpdatesItsRowInsteadOfAddingOne()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        const string endpoint = "https://push.example.net/same-device";
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var first = await client.SendAsync(JsonRequest(
            SubscriptionsPath(terminalId), cookie,
            new SavePushSubscriptionRequestV1(endpoint, ValidP256dh, ValidAuth)));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // A browser reissues a subscription with fresh keys on the same
        // endpoint whenever its own keys rotate.
        var rotated = WebPushCrypto.ToBase64Url(NewPoint());
        using var second = await client.SendAsync(JsonRequest(
            SubscriptionsPath(terminalId), cookie,
            new SavePushSubscriptionRequestV1(endpoint, rotated, ValidAuth)));
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        Assert.Equal(1, await _database.SubscriptionCountAsync());
        Assert.Equal(rotated, await _database.StoredKeyAsync(endpoint));
    }

    [Theory]
    [InlineData("http://push.example.net/x", ValidP256dh, ValidAuth)]
    [InlineData("file:///etc/passwd", ValidP256dh, ValidAuth)]
    [InlineData("not a url", ValidP256dh, ValidAuth)]
    [InlineData("https://push.example.net/x", "not base64url!!", ValidAuth)]
    [InlineData("https://push.example.net/x", ValidP256dh, "also not!!")]
    [InlineData("", ValidP256dh, ValidAuth)]
    public async Task AMalformedSubscriptionIsRejectedRatherThanStored(string endpoint, string p256dh, string auth)
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            SubscriptionsPath(terminalId), cookie,
            new SavePushSubscriptionRequestV1(endpoint, p256dh, auth)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _database.SubscriptionCountAsync());
    }

    [Fact]
    public async Task DeletingASubscriptionRemovesIt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        const string endpoint = "https://push.example.net/leaving";
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var saved = await client.SendAsync(JsonRequest(
            SubscriptionsPath(terminalId), cookie,
            new SavePushSubscriptionRequestV1(endpoint, ValidP256dh, ValidAuth)));
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal(1, await _database.SubscriptionCountAsync());

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{SubscriptionsPath(terminalId)}?endpoint={Uri.EscapeDataString(endpoint)}");
        request.Headers.Add("Cookie", cookie);
        using var deleted = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, await _database.SubscriptionCountAsync());
    }

    // V1-RMD-160: found by the 2026-09-10 Garson audit — the delete
    // endpoint used to remove whatever row matched the endpoint string with
    // no owner check, so any authenticated session could unsubscribe any
    // other device by guessing/observing its endpoint URL.
    [Fact]
    public async Task DeletingAnotherUsersSubscriptionDoesNothing()
    {
        var terminalId = Guid.NewGuid();
        var ownerCookie = await _database.SeedCashierSessionAsync(terminalId);
        var attackerCookie = await _database.SeedCashierSessionAsync(terminalId);
        const string endpoint = "https://push.example.net/owned-by-someone-else";
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var saved = await client.SendAsync(JsonRequest(
            SubscriptionsPath(terminalId), ownerCookie,
            new SavePushSubscriptionRequestV1(endpoint, ValidP256dh, ValidAuth)));
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal(1, await _database.SubscriptionCountAsync());

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{SubscriptionsPath(terminalId)}?endpoint={Uri.EscapeDataString(endpoint)}");
        request.Headers.Add("Cookie", attackerCookie);
        using var response = await client.SendAsync(request);

        // The request itself still succeeds (no ownership leak in the
        // response — the caller cannot distinguish "not yours" from "never
        // existed"), but the row survives untouched.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await _database.SubscriptionCountAsync());
    }

    private static byte[] NewPoint()
    {
        var point = new byte[65];
        Random.Shared.NextBytes(point);
        point[0] = 0x04;
        return point;
    }

    private static string SubscriptionsPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/push/subscriptions";

    private static string PublicKeyPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/push/public-key";

    private static HttpRequestMessage GetRequest(string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Cookie", cookie);
        return request;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddWebPushExperience();
        var app = builder.Build();
        // The composed host installs this before mapping any API
        // (DualScreenApplication.Build), and it is what turns a
        // DualScreenUnauthorizedException into a 401 with a Turkish message
        // instead of a bare 500. Without it here the test would be asserting
        // against a pipeline production does not have.
        DualScreenApplication.UseDualScreenErrorHandling(app);
        app.MapWebPushApi();
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

[CollectionDefinition("Web push PostgreSQL HTTP", DisableParallelization = true)]
public sealed class WebPushPostgresqlDefinition;
