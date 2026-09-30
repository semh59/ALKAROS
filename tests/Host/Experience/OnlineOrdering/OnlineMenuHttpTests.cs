using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.Menu;
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
/// V12-OUI-005 over real HTTP and Postgres: the Menu tab's data and its mapping changes — manager only, per platform,
/// never moving a platform code between products silently, and reading Trendyol Go's own menu when it can.
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class OnlineMenuHttpTests : IAsyncLifetime, IDisposable
{
    private readonly OnlineOrderingTestDatabase _database = new();
    private readonly InMemorySecretProvider _secrets = new();
    private readonly MenuHandler _platform = new();
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
        builder.Services.AddSingleton(new TrendyolGoMenuClient(new HttpClient(_platform), _secrets, TimeProvider.System));
        builder.Services.AddOnlineMenuExperience();
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy("terminal-read", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("r"));
            limiter.AddPolicy("terminal-write", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("w"));
        });
        _app = builder.Build();
        _app.UseRateLimiter();
        _app.MapOnlineMenuApi();
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public void Dispose()
    {
        _client?.Dispose();
        _platform.Dispose();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private string Root => $"/api/v1/terminals/{_terminalId:D}/online-menu";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? cookie, object? body = null)
    {
        using var request = new HttpRequestMessage(method, Root + path) { Content = body is null ? null : JsonContent.Create(body) };
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await _client!.SendAsync(request);
    }

    private static async Task<(string? Code, string? Message)> ErrorAsync(HttpResponseMessage response)
    {
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        return (error.GetProperty("code").GetString(), error.GetProperty("message").GetString());
    }

    private Task<string> ManagerAsync() => _database.SeedStaffSessionAsync(_terminalId, "integrations.manage");

    private PostgresYemeksepetiProductMappingService Mappings(string provider) => new(
        _database.DataSource, new PostgresProductRepository(_database.DataSource), new PostgresProductModifierGroupRepository(_database.DataSource),
        new PostgresModifierGroupRepository(_database.DataSource), provider);

    [Fact]
    public async Task OnlyAManagerReadsOrChangesTheMenuAndAnUnknownPlatformIsRefused()
    {
        var (productId, _) = await _database.SeedSellableProductAsync(onHand: 1m);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/yemeksepeti", null)).StatusCode);
        var cashier = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/yemeksepeti", cashier)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Put, $"/yemeksepeti/mappings/{productId:D}", cashier, new { externalSku = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Delete, $"/yemeksepeti/mappings/{productId:D}", cashier)).StatusCode);

        using var unknown = await SendAsync(HttpMethod.Get, "/migros-yemek", await ManagerAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Bu online platform tanınmıyor.", (await ErrorAsync(unknown)).Message);
    }

    [Fact]
    public async Task EachProductShowsItsPlatformCodeWhatThePlatformShowsAndTheLastPublishedPrice()
    {
        var manager = await ManagerAsync();
        var (mapped, sku) = await _database.SeedSellableProductAsync(onHand: 2m, name: "Adana");
        var (soldOut, soldOutSku) = await _database.SeedSellableProductAsync(onHand: 0m, name: "Beyti");
        var (menuId, _, _) = await _database.SeedPublishableMenuAsync(price: 120m);
        await _database.ExecuteAsync("UPDATE catalog.products SET current_price = 150 WHERE product_id = @p;", ("p", mapped));
        await _database.ExecuteAsync(
            """
            INSERT INTO online_ordering.availability_states (channel, product_id, external_sku, desired_quantity, desired_version, delivered_quantity, delivered_version)
            VALUES ('Yemeksepeti', @a, @as, 2, 1, 2, 1), ('Yemeksepeti', @b, @bs, 0, 1, 0, 1);
            INSERT INTO online_ordering.catalog_publications (publication_id, channel, menu_id, status, content_sha256, item_count, requested_by, requested_at, delivered_at, delivery_attempts)
            VALUES (@pub, 'Yemeksepeti', @menu, 'Delivered', repeat('a', 64), 1, @actor, now() - interval '1 hour', now() - interval '1 hour', 1),
                   (@failing, 'Yemeksepeti', @menu, 'Pending', repeat('b', 64), 1, @actor, now(), NULL, 2);
            UPDATE online_ordering.catalog_publications SET last_error = 'HttpRequestException: x' WHERE publication_id = @failing;
            INSERT INTO online_ordering.catalog_publication_items (publication_id, product_id, external_sku, title, price, active)
            VALUES (@pub, @a, @as, 'Adana', 140, true);
            """,
            ("a", mapped), ("as", sku), ("b", soldOut), ("bs", soldOutSku), ("pub", Guid.NewGuid()), ("failing", Guid.NewGuid()),
            ("menu", menuId), ("actor", Guid.NewGuid()));

        using var response = await SendAsync(HttpMethod.Get, "/yemeksepeti", manager);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Yemeksepeti", body.GetProperty("channel").GetString());
        var products = body.GetProperty("products").EnumerateArray().ToDictionary(p => p.GetProperty("productId").GetGuid());
        Assert.Equal((sku, 150m, 140m, "OnSale"), (products[mapped].GetProperty("externalSku").GetString(), products[mapped].GetProperty("catalogPrice").GetDecimal(),
            products[mapped].GetProperty("publishedPrice").GetDecimal(), products[mapped].GetProperty("saleState").GetString()));
        Assert.Equal("SoldOut", products[soldOut].GetProperty("saleState").GetString());
        Assert.Contains(body.GetProperty("menus").EnumerateArray(), m => m.GetProperty("menuId").GetGuid() == menuId);
        var publications = body.GetProperty("publications").EnumerateArray().ToList();
        Assert.Equal(("Pending", true), (publications[0].GetProperty("status").GetString(), publications[0].GetProperty("hasError").GetBoolean()));
        Assert.Equal(("Delivered", false), (publications[1].GetProperty("status").GetString(), publications[1].GetProperty("hasError").GetBoolean()));
        Assert.DoesNotContain("HttpRequestException", body.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("platformProducts").ValueKind);
    }

    [Fact]
    public async Task TrendyolGoListsItsOwnMenuWhenItCanAndSaysWhenItCannot()
    {
        var manager = await ManagerAsync();
        using (var unreadable = await SendAsync(HttpMethod.Get, "/trendyol-go", manager))
            Assert.True((await unreadable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("platformMenuUnavailable").GetBoolean());

        foreach (var (reference, value) in new[]
                 {
                     (TrendyolGoApiSettings.BaseUrlReference, "https://stageapi.tgoapis.com"), (TrendyolGoApiSettings.SupplierIdReference, "1"),
                     (TrendyolGoApiSettings.ApiKeyReference, "k"), (TrendyolGoApiSettings.ApiSecretReference, "s"),
                     (TrendyolGoApiSettings.IntegratorNameReference, "ALKAROS"), (TrendyolGoApiSettings.ExecutorEmailReference, "e@example.test"),
                     (TrendyolGoMenuClient.StoreIdReference, "153"),
                 })
            _secrets.Set(reference, value);
        var (productId, _) = await _database.SeedSellableProductAsync(onHand: 1m);
        await Mappings(TrendyolGoEvents.Provider).MapAsync("11", productId, DateTimeOffset.UtcNow.AddMinutes(-1), Guid.NewGuid());
        _platform.Menu = "{\"products\":[{\"id\":11,\"name\":\"Adana (TGO)\",\"status\":\"ACTIVE\"},{\"id\":12,\"name\":\"Çorba (TGO)\",\"status\":\"PASSIVE\"}]}";

        using var response = await SendAsync(HttpMethod.Get, "/trendyol-go", manager);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("platformMenuUnavailable").GetBoolean());
        var platform = body.GetProperty("platformProducts").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetString()!);
        Assert.Equal(("Adana (TGO)", true, productId), (platform["11"].GetProperty("name").GetString(), platform["11"].GetProperty("active").GetBoolean(),
            platform["11"].GetProperty("mappedProductId").GetGuid()));
        Assert.Equal((false, JsonValueKind.Null), (platform["12"].GetProperty("active").GetBoolean(), platform["12"].GetProperty("mappedProductId").ValueKind));

        _platform.Fail = true;
        using var down = await SendAsync(HttpMethod.Get, "/trendyol-go", manager);
        Assert.Equal(HttpStatusCode.OK, down.StatusCode);
        Assert.True((await down.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("platformMenuUnavailable").GetBoolean());
    }

    [Fact]
    public async Task RejectedOrdersNameTheirUnmappedCodesPerPlatformUntilTheCodeIsMapped()
    {
        var manager = await ManagerAsync();
        var (productId, _) = await _database.SeedSellableProductAsync(onHand: 1m);
        await _database.ExecuteAsync(
            """
            INSERT INTO online_ordering.provider_inbox
                (inbox_id, event_key, external_order_id, provider_status, body_sha256, payload_envelope, provider, received_at, processed_at, processing_outcome, outcome_detail)
            VALUES
                (gen_random_uuid(), repeat('1', 64), 'o-1', 'RECEIVED', repeat('b', 64), '\x00'::bytea, 'trendyol-go', now() - interval '2 hours', now(), 'Rejected', '{"rejection":"UnmappedSku","detail":"31"}'),
                (gen_random_uuid(), repeat('2', 64), 'o-2', 'RECEIVED', repeat('b', 64), '\x00'::bytea, 'trendyol-go', now() - interval '1 hour', now(), 'Rejected', '{"rejection":"UnmappedSku","detail":"31"}'),
                (gen_random_uuid(), repeat('3', 64), 'o-3', 'RECEIVED', repeat('b', 64), '\x00'::bytea, 'trendyol-go', now() - interval '3 hours', now(), 'Rejected', '{"rejection":"UnmappedSku","detail":"32"}'),
                (gen_random_uuid(), repeat('4', 64), 'o-4', 'RECEIVED', repeat('b', 64), '\x00'::bytea, 'trendyol-go', now() - interval '40 days', now(), 'Rejected', '{"rejection":"UnmappedSku","detail":"33"}'),
                (gen_random_uuid(), repeat('5', 64), 'o-5', 'RECEIVED', repeat('b', 64), '\x00'::bytea, 'trendyol-go', now() - interval '1 hour', now(), 'Rejected', '{"rejection":"ItemUnavailable","detail":"34"}'),
                (gen_random_uuid(), repeat('6', 64), 'o-6', 'RECEIVED', repeat('b', 64), '\x00'::bytea, 'yemeksepeti', now() - interval '1 hour', now(), 'Rejected', '{"rejection":"UnmappedSku","detail":"SKU-9"}');
            """);

        async Task<List<(string Code, int Orders)>> CodesAsync(string provider)
        {
            using var response = await SendAsync(HttpMethod.Get, "/" + provider, manager);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("unmappedCodes").EnumerateArray()
                .Select(c => (c.GetProperty("code").GetString()!, c.GetProperty("orderCount").GetInt32())).ToList();
        }

        Assert.Equal([("31", 2), ("32", 1)], await CodesAsync("trendyol-go"));
        Assert.Equal([("SKU-9", 1)], await CodesAsync("yemeksepeti"));

        using var mapped = await SendAsync(HttpMethod.Put, $"/trendyol-go/mappings/{productId:D}", manager, new { externalSku = "31" });
        Assert.Equal(HttpStatusCode.OK, mapped.StatusCode);
        Assert.Equal([("32", 1)], await CodesAsync("trendyol-go"));
    }

    [Fact]
    public async Task AManagerMapsAndUnmapsAndACodeIsNeverMovedBetweenProductsSilently()
    {
        var manager = await ManagerAsync();
        var (first, _) = await _database.SeedSellableProductAsync(onHand: 1m);
        var (second, _) = await _database.SeedSellableProductAsync(onHand: 1m);

        using var nonNumeric = await SendAsync(HttpMethod.Put, $"/trendyol-go/mappings/{first:D}", manager, new { externalSku = "abc" });
        Assert.Equal((HttpStatusCode.BadRequest, "Trendyol Go ürün kodu yalnız rakamlardan oluşur."), (nonNumeric.StatusCode, (await ErrorAsync(nonNumeric)).Message));
        using var blank = await SendAsync(HttpMethod.Put, $"/trendyol-go/mappings/{first:D}", manager, new { externalSku = "  " });
        Assert.Equal(("INVALID_CODE", "Platform ürün kodu geçersiz."), await ErrorAsync(blank));

        using var mapped = await SendAsync(HttpMethod.Put, $"/trendyol-go/mappings/{first:D}", manager, new { externalSku = " 21 " });
        Assert.Equal(HttpStatusCode.OK, mapped.StatusCode);
        Assert.Equal("21", await Mappings(TrendyolGoEvents.Provider).FindOpenSkuForProductAsync(first));

        using var taken = await SendAsync(HttpMethod.Put, $"/trendyol-go/mappings/{second:D}", manager, new { externalSku = "21" });
        Assert.Equal((HttpStatusCode.Conflict, "CODE_TAKEN"), (taken.StatusCode, (await ErrorAsync(taken)).Code));
        Assert.Equal("21", await Mappings(TrendyolGoEvents.Provider).FindOpenSkuForProductAsync(first));

        using var second21 = await SendAsync(HttpMethod.Put, $"/trendyol-go/mappings/{first:D}", manager, new { externalSku = "22" });
        Assert.Equal((HttpStatusCode.Conflict, "Bu ürün zaten başka bir platform koduna eşli; önce o eşlemeyi kaldırın."),
            (second21.StatusCode, (await ErrorAsync(second21)).Message));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"/trendyol-go/mappings/{first:D}", manager)).StatusCode);
        Assert.Null(await Mappings(TrendyolGoEvents.Provider).FindOpenSkuForProductAsync(first));
        Assert.Equal(1L, await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM online_ordering.provider_product_mappings WHERE provider = 'trendyol-go' AND product_id = '{first:D}' AND closed_by IS NOT NULL AND effective_to IS NOT NULL"));
        using var again = await SendAsync(HttpMethod.Delete, $"/trendyol-go/mappings/{first:D}", manager);
        Assert.Equal((HttpStatusCode.NotFound, "NOT_MAPPED"), (again.StatusCode, (await ErrorAsync(again)).Code));

        // Mappings are per platform: Yemeksepeti's own mapping of the same product is untouched.
        Assert.NotNull(await Mappings("yemeksepeti").FindOpenSkuForProductAsync(first));

        await _database.ExecuteAsync("UPDATE catalog.products SET active = false WHERE product_id = @p;", ("p", second));
        using var inactive = await SendAsync(HttpMethod.Put, $"/trendyol-go/mappings/{second:D}", manager, new { externalSku = "23" });
        Assert.Equal((HttpStatusCode.Conflict, "Pasif ürün platformda satılamaz."), (inactive.StatusCode, (await ErrorAsync(inactive)).Message));
    }

    private sealed class MenuHandler : HttpMessageHandler
    {
        public string Menu { get; set; } = "{\"products\":[]}";

        public bool Fail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Fail
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Menu, Encoding.UTF8, "application/json") });
    }
}
