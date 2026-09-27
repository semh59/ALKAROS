using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.OnlineOrdering.Credentials;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-OUI-004 over real HTTP and Postgres: the online food screen's platform status line — per registered platform,
/// whether its connection settings are entered (stored or from the environment), its last order event and its polling
/// state — for staff who take orders, without any secret.
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class OnlineChannelHealthHttpTests : IAsyncLifetime, IDisposable
{
    private const string ClientSecret = "cs-never-returned";
    private static readonly string[] BothPlatforms = ["trendyol-go", "yemeksepeti"];
    private static readonly string[] YemeksepetiFieldsButSecret = ["api-base-url", "chain-id", "vendor-id", "client-id", "webhook-secret"];

    private readonly OnlineOrderingTestDatabase _database = new();
    private readonly InMemorySecretProvider _secrets = new();
    private readonly Guid _terminalId = Guid.NewGuid();
    private WebApplication? _app;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(_secrets);
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddYemeksepetiWebhookExperience();
        builder.Services.AddTransient(services => new TrendyolGoOrderNormalizer(
            new PostgresYemeksepetiProductMappingService(_database.DataSource, new PostgresProductRepository(_database.DataSource),
                new PostgresProductModifierGroupRepository(_database.DataSource), new PostgresModifierGroupRepository(_database.DataSource), TrendyolGoEvents.Provider),
            services.GetRequiredService<IProductRepository>(), services.GetRequiredService<ITaxProfileRepository>()));
        builder.Services.AddTransient<IOnlineOrderProvider, TrendyolGoOnlineOrderProvider>();
        builder.Services.AddOnlineChannelHealthExperience();
        builder.Services.AddRateLimiter(limiter =>
            limiter.AddPolicy("terminal-read", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("r")));
        _app = builder.Build();
        _app.UseRateLimiter();
        _app.MapOnlineChannelHealthApi();
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public void Dispose() => _client?.Dispose();

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private async Task<HttpResponseMessage> GetAsync(string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{_terminalId:D}/online-channels");
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await _client!.SendAsync(request);
    }

    private async Task<Dictionary<string, JsonElement>> ChannelsAsync(string cookie)
    {
        using var response = await GetAsync(cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ClientSecret, text, StringComparison.Ordinal);
        return JsonDocument.Parse(text).RootElement.GetProperty("platforms").EnumerateArray()
            .ToDictionary(p => p.GetProperty("provider").GetString()!, p => p.Clone());
    }

    [Fact]
    public async Task OnlyStaffWhoTakeOrdersReadIt()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(null)).StatusCode);
        var kitchen = await _database.SeedStaffSessionAsync(_terminalId, "kitchen.advance");
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(kitchen)).StatusCode);
    }

    [Fact]
    public async Task EachPlatformSaysWhetherItIsSetUpWhenItLastHeardAndHowItsPollingIs()
    {
        var staff = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var fresh = await ChannelsAsync(staff);
        Assert.Equal(BothPlatforms, fresh.Keys.Order());
        Assert.All(fresh.Values, p => Assert.Equal((false, JsonValueKind.Null, "NotPolled"),
            (p.GetProperty("configured").GetBoolean(), p.GetProperty("lastEventAt").ValueKind, p.GetProperty("polling").GetString())));
        Assert.Equal(("Trendyol Go", "Yemeksepeti"), (fresh["trendyol-go"].GetProperty("displayName").GetString(), fresh["yemeksepeti"].GetProperty("displayName").GetString()));

        // Yemeksepeti from environment variables, Trendyol Go from the settings screen: both count.
        foreach (var field in YemeksepetiFieldsButSecret)
            _secrets.Set(new SecretReference("yemeksepeti-" + field), field == "api-base-url" ? "https://ys.example.test" : "v");
        Assert.False((await ChannelsAsync(staff))["yemeksepeti"].GetProperty("configured").GetBoolean());
        _secrets.Set(new SecretReference("yemeksepeti-client-secret"), ClientSecret);
        var store = new PostgresOnlinePlatformCredentialStore(_database.DataSource, _secrets);
        await store.SaveAsync(new SaveOnlinePlatformCredentialRequest("trendyol-go", new Dictionary<string, string>
        {
            ["api-base-url"] = "https://stageapi.tgoapis.com", ["supplier-id"] = "1", ["api-key"] = "k", ["api-secret"] = ClientSecret,
            ["integrator-name"] = "ALKAROS", ["executor-email"] = "kasa@example.test",
        }, []), null);

        await _database.ExecuteAsync(
            """
            INSERT INTO online_ordering.provider_inbox (inbox_id, event_key, external_order_id, provider_status, body_sha256, payload_envelope, provider, received_at)
            VALUES (@id, repeat('a', 64), 'o-1', 'RECEIVED', repeat('b', 64), '\x00'::bytea, 'yemeksepeti', '2026-09-27T09:05:00Z');
            INSERT INTO online_ordering.provider_poll_state (provider, consecutive_failures, failing_since, last_error)
            VALUES ('trendyol-go', 2, now(), 'HttpRequestException');
            """, ("id", Guid.NewGuid()));

        var now = await ChannelsAsync(staff);
        Assert.True(now["yemeksepeti"].GetProperty("configured").GetBoolean());
        Assert.True(now["trendyol-go"].GetProperty("configured").GetBoolean());
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 9, 5, 0, TimeSpan.Zero), now["yemeksepeti"].GetProperty("lastEventAt").GetDateTimeOffset());
        Assert.Equal(("NotPolled", "Failing"), (now["yemeksepeti"].GetProperty("polling").GetString(), now["trendyol-go"].GetProperty("polling").GetString()));

        await _database.ExecuteAsync("UPDATE online_ordering.provider_poll_state SET last_error = 'RateLimited' WHERE provider = 'trendyol-go';");
        Assert.Equal("RateLimited", (await ChannelsAsync(staff))["trendyol-go"].GetProperty("polling").GetString());
        await _database.ExecuteAsync("UPDATE online_ordering.provider_poll_state SET consecutive_failures = 0, failing_since = NULL, last_error = NULL WHERE provider = 'trendyol-go';");
        Assert.Equal("Working", (await ChannelsAsync(staff))["trendyol-go"].GetProperty("polling").GetString());
    }
}
