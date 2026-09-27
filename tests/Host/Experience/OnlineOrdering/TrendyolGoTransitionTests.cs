using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.StatusSync;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-TGO-005 over real HTTP and Postgres: an Uber Eats transition order (EXT:TGO-MEAL-API "Meal Changes", read
/// 2026-09-27: 5-character orderCode, masked apartment/floor/door, no coupon, promotions, appName UberEats, dynamic phone
/// and 8-digit pinCode, automatic Invoiced) is taken like any other, and staff see the platform's number and code to call
/// the customer only when they open the order's note, which is audited. UNVERIFIED DRAFT (V12-TGO-001 Blocked).
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class TrendyolGoTransitionTests : IAsyncLifetime, IDisposable
{
    private readonly OnlineOrderingTestDatabase _database = new();
    private readonly Guid _terminalId = Guid.NewGuid();
    private WebApplication? _app;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        secrets.Set(YemeksepetiWebhookInbox.WebhookSecret, "Bearer x");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(secrets);
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddYemeksepetiWebhookExperience();
        builder.Services.AddOnlineCatalogPublishingExperience();
        OnlineAvailabilityPublishingHostedService.AddOnlineAvailabilityPublishingExperience(builder.Services);
        builder.Services.AddOnlineOperationsExperience();
        builder.Services.AddTrendyolGoWebhookExperience();
        builder.Services.AddTransient(services => new TrendyolGoOrderNormalizer(
            Mappings(), services.GetRequiredService<IProductRepository>(), services.GetRequiredService<ITaxProfileRepository>()));
        builder.Services.AddTransient<IOnlineOrderProvider, TrendyolGoOnlineOrderProvider>();
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy("terminal-read", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("r"));
            limiter.AddPolicy("terminal-write", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("w"));
        });
        _app = builder.Build();
        _app.UseRateLimiter();
        _app.MapOnlineOperationsApi();
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

    private PostgresYemeksepetiProductMappingService Mappings() => new(
        _database.DataSource, new PostgresProductRepository(_database.DataSource), new PostgresProductModifierGroupRepository(_database.DataSource),
        new PostgresModifierGroupRepository(_database.DataSource), TrendyolGoEvents.Provider);

    /// <summary>The document's transition model: masked address details, the full address as free text in address1.</summary>
    private static string UberPackage(string packageId, string tgoSku, string? pinCode = "12345678", string? callCenterPhone = "0212 555 66 77") =>
        "{\"id\":\"" + packageId + "\",\"orderCode\":\"4D5AC\",\"storePickupSelected\":false,\"deliveryType\":\"GO\",\"orderNumber\":\"1199521762\","
        + "\"totalPrice\":150.0," + (callCenterPhone is null ? "" : "\"callCenterPhone\":\"" + callCenterPhone + "\",")
        + "\"customer\":{\"id\":1904246,\"firstName\":\"Ayşe\",\"lastName\":\"K.\"},"
        + "\"address\":{\"address1\":\"xxxyyy zzzhhh, 9 kat 2 daire\",\"apartmentNumber\":\"TGO Yemek\",\"floor\":\"TGO Yemek\",\"doorNumber\":\"TGO Yemek\","
        + "\"phone\":\"0212 555 66 77\"" + (pinCode is null ? "" : ",\"pinCode\":\"" + pinCode + "\"") + "},"
        + "\"packageStatus\":\"Created\",\"customerNote\":\"Zili çalmayın\","
        + "\"lines\":[{\"price\":75.0,\"unitSellingPrice\":75.0,\"items\":[{\"packageItemId\":\"1\",\"lineItemId\":1,\"isCancelled\":false},"
        + "{\"packageItemId\":\"2\",\"lineItemId\":2,\"isCancelled\":false}],\"productId\":" + tgoSku + ",\"name\":\"Lahmacun\","
        + "\"modifierProducts\":[],\"extraIngredients\":[],\"removedIngredients\":[]}],"
        + "\"coupon\":null,\"promotions\":[{\"promotionId\":1,\"description\":\"Promotion\",\"totalSellerAmount\":0}],"
        + "\"userInformation\":{\"appName\":\"UberEats\"}}";

    private async Task<(string PackageId, Guid OrderId)> TransitionOrderAsync(string? pinCode = "12345678")
    {
        var (productId, _) = await _database.SeedSellableProductAsync(onHand: 5m);
        var tgoSku = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Mappings().MapAsync(tgoSku, productId, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid());
        var packageId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await _app!.Services.GetRequiredService<ProviderInbox>().StoreAsync(new ProviderInboxEvent(
            TrendyolGoEvents.Provider, TrendyolGoEvents.EventKey(TrendyolGoEvents.Created, packageId), packageId, TrendyolGoEvents.Created, null,
            Encoding.UTF8.GetBytes(UberPackage(packageId, tgoSku, pinCode))));
        var intake = _app.Services.GetRequiredService<YemeksepetiOrderIntakeService>();
        while (await intake.ProcessNextAsync())
        {
        }

        return (packageId, Assert.Single(await _database.OnlineOrdersAsync(packageId)).OrderId);
    }

    [Fact]
    public async Task AnUberEatsTransitionOrderBecomesAnOrderUnderItsFiveCharacterCode()
    {
        var (packageId, orderId) = await TransitionOrderAsync();
        var order = Assert.Single(await _database.OnlineOrdersAsync(packageId));
        Assert.Equal(("Accepted", "Trendyol Go 4D5AC"), (order.Status, order.Notes));
        Assert.Equal(orderId, order.OrderId);
        var created = Assert.Single(await _database.InboxAsync(packageId));
        Assert.Contains("\"displayCode\": \"4D5AC\"", created.Detail, StringComparison.Ordinal);
        // No address, name or phone reaches the order or the processing record.
        Assert.DoesNotContain("9 kat", created.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("0212", created.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningTheNoteShowsThePlatformsCallNumberAndCodeAndIsAudited()
    {
        var (_, orderId) = await TransitionOrderAsync();
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{_terminalId:D}/online-operations/orders/{orderId:D}/customer-note");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Zili çalmayın", "0212 555 66 77", "12345678"),
            (body.GetProperty("note").GetString(), body.GetProperty("callPhone").GetString(), body.GetProperty("callCode").GetString()));
        var metadata = await _database.ScalarTextAsync(
            $"SELECT metadata_json::text FROM audit.audit_events WHERE event_name = 'Order.CustomerNoteViewed' AND aggregate_id = '{orderId:D}';");
        Assert.Contains("\"hadCallInfo\": true", metadata, StringComparison.Ordinal);
        Assert.Contains("\"hadNote\": true", metadata, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("12345678", "0212 555 66 77", true)]
    [InlineData("12345678901", "0212 555 66 77", true)]
    [InlineData(null, "0212 555 66 77", false)]
    [InlineData("12AB5678", "0212 555 66 77", false)]
    [InlineData("12345678", null, true)]
    [InlineData("12345678", "0212 555 66 77 ext", false)]
    public void CallInfoIsOnlyAWholePhoneNumberAndDigitCode(string? pin, string? callCenterPhone, bool expected)
    {
        var provider = new TrendyolGoOnlineOrderProvider(_app!.Services.GetRequiredService<TrendyolGoOrderNormalizer>());
        var payload = UberPackage("p", "1", pin, callCenterPhone);
        if (callCenterPhone == "0212 555 66 77 ext")
            payload = payload.Replace("\"phone\":\"0212 555 66 77\"", "\"phone\":\"x\"", StringComparison.Ordinal);
        var call = provider.ReadCallInfo(payload);
        Assert.Equal(expected, call is not null);
        if (call is not null)
            Assert.Equal((pin, "0212 555 66 77"), ((string?)call.PinCode, call.Phone));
        Assert.Equal("Zili çalmayın", provider.ReadCustomerNote(payload));
    }

    [Fact]
    public async Task InvoicedAgainAfterTheAutomaticInvoicedIsNotAFailureButOtherConflictsAre()
    {
        var secrets = new InMemorySecretProvider();
        secrets.Set(TrendyolGoApiSettings.BaseUrlReference, "https://stageapi.tgoapis.com");
        secrets.Set(TrendyolGoApiSettings.SupplierIdReference, "107385");
        secrets.Set(TrendyolGoApiSettings.ApiKeyReference, "key");
        secrets.Set(TrendyolGoApiSettings.ApiSecretReference, "secret");
        secrets.Set(TrendyolGoApiSettings.IntegratorNameReference, "ALKAROS");
        secrets.Set(TrendyolGoApiSettings.ExecutorEmailReference, "kasa@example.test");
        using var client = new TrendyolGoStatusClient(new HttpClient(new ConflictHandler()), secrets, TimeProvider.System);

        await client.InvoicedAsync("p1");
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => client.PickedAsync("p1", 20));
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => client.ManualShippedAsync("p1"));
    }

    [Fact]
    public async Task AYemeksepetiOrderHasNoCallInfo()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var externalId = Guid.NewGuid().ToString("D");
        var body = "{\"order_id\":\"" + externalId + "\",\"external_order_id\":\"YS-1\",\"status\":\"RECEIVED\",\"transport_type\":\"LOGISTICS_DELIVERY\","
                   + "\"comment\":\"Kapıda bekleyin\",\"items\":[{\"_id\":\"i1\",\"sku\":\"" + sku + "\",\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":1,\"unit_price\":150}}],"
                   + "\"sys\":{\"updated_at\":\"t1\"}}";
        await _app!.Services.GetRequiredService<YemeksepetiWebhookInbox>().ReceiveAsync("Bearer x", Encoding.UTF8.GetBytes(body));
        var intake = _app.Services.GetRequiredService<YemeksepetiOrderIntakeService>();
        while (await intake.ProcessNextAsync())
        {
        }

        var orderId = Assert.Single(await _database.OnlineOrdersAsync(externalId)).OrderId;
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{_terminalId:D}/online-operations/orders/{orderId:D}/customer-note");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        var note = await (await _client!.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Kapıda bekleyin", note.GetProperty("note").GetString());
        Assert.Equal(JsonValueKind.Null, note.GetProperty("callCode").ValueKind);
    }

    private sealed class ConflictHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("{}") });
    }
}
