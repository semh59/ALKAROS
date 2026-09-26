using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ALKAROS.Host.Experience.OnlineOrdering;
using ALKAROS.OnlineOrdering.CatalogPublishing.Yemeksepeti;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>V12-ONL-004: the manager endpoint that publishes a menu, over real HTTP and Postgres. The provider call itself is never made here.</summary>
[Collection("Online ordering PostgreSQL")]
public sealed class OnlineCatalogPublishingHttpTests : IAsyncLifetime
{
    private readonly OnlineOrderingTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private sealed class UnusedPartnerClient : IYemeksepetiPartnerClient
    {
        public Task UpdateOrderStatusAsync(YemeksepetiStatusUpdateRequested update, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Publishing only commits; delivery is the outbox's job.");

        public Task<string?> UpdateVendorCatalogAsync(IReadOnlyList<YemeksepetiCatalogProductUpdate> products, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Publishing only commits; delivery is the outbox's job.");
    }

    private static string Path(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/online-ordering/catalog-publications";

    [Fact]
    public async Task AManagerPublishesAMenuAndGetsTheTypedSummary()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedStaffSessionAsync(terminalId, "integrations.manage");
        var (menuId, _, _) = await _database.SeedPublishableMenuAsync(89.9m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Post(terminalId, cookie, new PublishCatalogRequest(YemeksepetiCatalogPublisher.ChannelName, menuId)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PublishCatalogResponse>();
        Assert.Equal("Pending", body!.Status);
        Assert.Equal(1, body.ItemCount);
        Assert.Empty(body.ValidationErrors);
        Assert.Contains("TaxMetadata", body.UnsupportedCapabilities);
    }

    [Fact]
    public async Task WithoutASessionOrPermissionNothingIsPublished()
    {
        var terminalId = Guid.NewGuid();
        var staffCookie = await _database.SeedStaffSessionAsync(terminalId, "orders.create");
        var (menuId, _, _) = await _database.SeedPublishableMenuAsync(10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = new PublishCatalogRequest(YemeksepetiCatalogPublisher.ChannelName, menuId);

        using var anonymous = await client.SendAsync(Post(terminalId, null, request));
        using var forbidden = await client.SendAsync(Post(terminalId, staffCookie, request));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, await _database.CountPublicationsAsync());
    }

    [Fact]
    public async Task AnUnknownChannelIsATurkishValidationError()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedStaffSessionAsync(terminalId, "integrations.manage");
        var (menuId, _, _) = await _database.SeedPublishableMenuAsync(10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Post(terminalId, cookie, new PublishCatalogRequest("Getir", menuId)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("UNKNOWN_CHANNEL", text);
        Assert.Contains("Bu satış kanalı tanımlı değil.", text);
    }

    private static HttpRequestMessage Post(Guid terminalId, string? cookie, PublishCatalogRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path(terminalId)) { Content = JsonContent.Create(body) };
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<WebApplication> StartAsync()
    {
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(secrets);
        builder.Services.AddSingleton<IYemeksepetiPartnerClient>(new UnusedPartnerClient());
        builder.Services.AddYemeksepetiWebhookExperience();
        builder.Services.AddOnlineCatalogPublishingExperience();
        builder.Services.AddRateLimiter(limiter => limiter.AddPolicy("terminal-write", _ =>
            System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test")));
        var app = builder.Build();
        app.UseRateLimiter();
        app.MapOnlineCatalogPublishingApi();
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
