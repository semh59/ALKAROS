using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.OnlineOrdering.Polling;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Providers.Inbox;
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
/// V12-TGO-002 over real HTTP and Postgres. The payloads follow the public document's own samples (EXT:TGO-MEAL-API,
/// read 2026-09-27); no real Trendyol Go delivery exists (V12-TGO-001 Blocked, C106 waiver), so this proves the
/// draft does what the document says, not that the platform does.
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class TrendyolGoOrderIntakeTests : IAsyncLifetime, IDisposable
{
    private const string WebhookValue = "tgo-webhook-value";

    private readonly OnlineOrderingTestDatabase _database = new();
    private static readonly string[] CreatedAndShipped = ["created", "shipped"];

    private readonly TestSecrets _secrets = new();
    private WebApplication? _app;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _secrets.Set(TrendyolGoWebhookInbox.WebhookSecret, WebhookValue);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(_secrets);
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddYemeksepetiWebhookExperience();
        builder.Services.AddTrendyolGoWebhookExperience();
        // The adapter is registered here for the intake; in the Host it is registered with its outbound calls (V12-TGO-003).
        builder.Services.AddTransient(services => new TrendyolGoOrderNormalizer(
            Mappings(), services.GetRequiredService<IProductRepository>(), services.GetRequiredService<ITaxProfileRepository>()));
        builder.Services.AddTransient<IOnlineOrderProvider, TrendyolGoOnlineOrderProvider>();
        builder.Services.AddRateLimiter(limiter =>
            limiter.AddPolicy("trendyol-go-webhook", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("w")));
        _app = builder.Build();
        _app.UseRateLimiter();
        _app.MapTrendyolGoWebhookApi();
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

    private async Task<(Guid ProductId, string TgoSku)> SeedTgoProductAsync(decimal onHand = 5m)
    {
        var (productId, _) = await _database.SeedSellableProductAsync(onHand);
        var tgoSku = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Mappings().MapAsync(tgoSku, productId, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid());
        // The shared mapping table keeps Trendyol Go's rows apart from Yemeksepeti's.
        Assert.Equal("trendyol-go", await _database.ScalarTextAsync(
            $"SELECT provider FROM online_ordering.provider_product_mappings WHERE external_sku = '{tgoSku}';"));
        return (productId, tgoSku);
    }

    private static string NewPackageId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    /// <summary>The document's Model 1 package sample, trimmed: two units of a menu with chosen modifiers, one of them cancelled.</summary>
    private static string Package(string packageId, string tgoSku, string status = "Created", string deliveryType = "STORE",
        bool cancelSecondItem = false, bool withCoupon = false, decimal totalPrice = 290m, string? cancelInfo = null) =>
        "{\"id\":\"" + packageId + "\",\"supplierId\":107385,\"storeId\":153,\"orderCode\":\"1E1\",\"storePickupSelected\":false,"
        + "\"deliveryType\":\"" + deliveryType + "\",\"packageCreationDate\":1783520347138,\"packageModificationDate\":1783520347146,"
        + "\"orderId\":\"1001199521762\",\"orderNumber\":\"1199521762\",\"totalPrice\":" + totalPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
        + "\"customer\":{\"id\":35018440,\"firstName\":\"Oms\",\"lastName\":\"M\"},\"payment\":{\"paymentType\":\"PAY_WITH_CARD\",\"mealCard\":null},"
        + "\"address\":{\"address1\":\"Gündoğdu Koleji Yanı\",\"phone\":\"5554443322\"},\"packageStatus\":\"" + status + "\","
        + "\"lines\":[{\"price\":145.0,\"unitSellingPrice\":145.0,\"items\":["
        + "{\"packageItemId\":\"1000008723596\",\"lineItemId\":1000008181985,\"isCancelled\":false,\"coupon\":null,\"promotions\":[]},"
        + "{\"packageItemId\":\"1000008723597\",\"lineItemId\":1000008181986,\"isCancelled\":" + (cancelSecondItem ? "true" : "false") + ",\"coupon\":null,\"promotions\":[]}],"
        + "\"productId\":" + tgoSku + ",\"name\":\"Big King Menü\","
        + "\"modifierProducts\":[{\"name\":\"Big King\",\"price\":0.0,\"productId\":319239,\"modifierProducts\":[],\"extraIngredients\":[],\"removedIngredients\":[{\"name\":\"Soğan\"}]},"
        + "{\"name\":\"Sprite\",\"price\":30.5,\"productId\":317823,\"modifierProducts\":[],\"extraIngredients\":[],\"removedIngredients\":[]}],"
        + "\"extraIngredients\":[{\"name\":\"Cheddar\"}],\"removedIngredients\":[]}],"
        + "\"customerNote\":\"Servis İstiyorum\",\"totalDeliveryPrice\":null,"
        + (withCoupon ? "\"coupon\":{\"couponId\":\"c\",\"description\":\"Coupon\",\"totalSellerAmount\":10}," : "\"coupon\":null,")
        + "\"promotions\":null" + (cancelInfo is null ? "" : ",\"cancelInfo\":" + cancelInfo) + "}";

    private async Task<HttpResponseMessage> DeliverAsync(string eventType, string body, string? header = WebhookValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TrendyolGoWebhookEndpoints.RoutePrefix}/{eventType}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (header is not null)
            request.Headers.TryAddWithoutValidation(TrendyolGoWebhookInbox.HeaderName, header);
        return await _client!.SendAsync(request);
    }

    private async Task<int> DrainAsync()
    {
        var intake = _app!.Services.GetRequiredService<YemeksepetiOrderIntakeService>();
        var processed = 0;
        while (await intake.ProcessNextAsync())
            processed++;
        return processed;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;

    [Fact]
    public async Task TheWebhookRefusesBeforeReadingAndStoresEachPackageEventOnce()
    {
        var packageId = NewPackageId();
        var body = Package(packageId, "123");

        using var wrong = await DeliverAsync("created", body, "other");
        Assert.Equal((HttpStatusCode.Unauthorized, "UNAUTHENTICATED"), (wrong.StatusCode, await CodeAsync(wrong)));
        using var missing = await DeliverAsync("created", body, header: null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var unknownType = await DeliverAsync("paid", body);
        Assert.Equal((HttpStatusCode.NotFound, "UNKNOWN_EVENT_TYPE"), (unknownType.StatusCode, await CodeAsync(unknownType)));
        using var malformed = await DeliverAsync("created", "{\"orderNumber\":\"1\"}");
        Assert.Equal((HttpStatusCode.BadRequest, "MALFORMED_PAYLOAD"), (malformed.StatusCode, await CodeAsync(malformed)));
        Assert.Empty(await _database.InboxAsync(packageId));

        using var first = await DeliverAsync("created", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("stored", await first.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var repeat = await DeliverAsync("created", body);
        Assert.Contains("duplicate", await repeat.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var shipped = await DeliverAsync("shipped", "{\"id\":\"" + packageId + "\",\"orderNumber\":\"946511491\",\"orderCode\":\"047\",\"timestamp\":1734951544000}");
        Assert.Contains("stored", await shipped.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Equal(CreatedAndShipped, (await _database.InboxAsync(packageId)).Select(e => e.Status).Order().ToArray());
        Assert.Equal("trendyol-go", await _database.ScalarTextAsync(
            $"SELECT DISTINCT provider FROM online_ordering.provider_inbox WHERE external_order_id = '{packageId}';"));

        _secrets.Remove(TrendyolGoWebhookInbox.WebhookSecret);
        using var closed = await DeliverAsync("created", body);
        Assert.Equal((HttpStatusCode.ServiceUnavailable, "CHANNEL_NOT_CONFIGURED"), (closed.StatusCode, await CodeAsync(closed)));
    }

    [Fact]
    public async Task ACreatedPackageBecomesOneOrderWithItsChoicesForTheKitchenAndACancellationCancelsIt()
    {
        var (productId, tgoSku) = await SeedTgoProductAsync(onHand: 5m);
        var packageId = NewPackageId();
        (await DeliverAsync("created", Package(packageId, tgoSku, cancelSecondItem: true, totalPrice: 145m))).EnsureSuccessStatusCode();

        Assert.Equal(1, await DrainAsync());
        var order = Assert.Single(await _database.OnlineOrdersAsync(packageId));
        Assert.Equal(("Accepted", "TG-" + packageId[..47], "Trendyol Go 1E1"), (order.Status, order.OrderNumber, order.Notes));
        Assert.Equal(4m, await _database.AvailableAsync(productId));
        var notes = Assert.Single(await _database.OrderItemNotesAsync(order.OrderId));
        Assert.Equal("Seçimler: Big King (-Soğan), Sprite; Ekstra: Cheddar", notes);
        var created = Assert.Single(await _database.InboxAsync(packageId));
        Assert.Equal("OrderCreated", created.Outcome);
        Assert.DoesNotContain("Gündoğdu", created.Detail ?? "", StringComparison.Ordinal);

        (await DeliverAsync("cancelled", "{\"id\":\"" + packageId + "\",\"orderNumber\":\"1199521762\",\"orderCode\":\"1E1\",\"status\":\"Cancelled\","
            + "\"timestamp\":1734952118000,\"lines\":[],\"cancelInfo\":{\"reasonCode\":601,\"reason\":\"Adres yanlış\"}}")).EnsureSuccessStatusCode();
        Assert.Equal(1, await DrainAsync());
        Assert.Equal("Cancelled", Assert.Single(await _database.OnlineOrdersAsync(packageId)).Status);
        var cancelled = Assert.Single(await _database.InboxAsync(packageId), e => e.Status == "cancelled");
        Assert.Equal("OrderCancelled", cancelled.Outcome);
        Assert.Contains("601 Adres yanlış", cancelled.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APackageThatArrivesByWebhookAndByPollingIsOneInboxRowAndOneOrder()
    {
        var (_, tgoSku) = await SeedTgoProductAsync();
        var packageId = NewPackageId();
        var package = Package(packageId, tgoSku);
        (await DeliverAsync("created", package)).EnsureSuccessStatusCode();

        SetApiSettings();
        var handler = new ScriptedHandler();
        handler.Answer(HttpStatusCode.OK, "{\"page\":0,\"size\":50,\"totalPages\":1,\"totalCount\":1,\"content\":[" + package + "]}");
        var source = new TrendyolGoOrderPollingSource(new HttpClient(handler), _secrets, TimeProvider.System);
        var poller = new OnlineOrderPoller(_database.DataSource, _app!.Services.GetRequiredService<ProviderInbox>(), [source]);
        Assert.Equal(OnlineOrderPollOutcome.Polled, (await poller.PollDueAsync())[TrendyolGoEvents.Provider]);

        Assert.Single(await _database.InboxAsync(packageId));
        Assert.Equal(1, await DrainAsync());
        Assert.Single(await _database.OnlineOrdersAsync(packageId));
    }

    private void SetApiSettings()
    {
        _secrets.Set(TrendyolGoApiSettings.BaseUrlReference, "https://stageapi.tgoapis.com");
        _secrets.Set(TrendyolGoApiSettings.SupplierIdReference, "107385");
        _secrets.Set(TrendyolGoApiSettings.ApiKeyReference, "key");
        _secrets.Set(TrendyolGoApiSettings.ApiSecretReference, "secret");
        _secrets.Set(TrendyolGoApiSettings.IntegratorNameReference, "ALKAROS");
        _secrets.Set(TrendyolGoApiSettings.ExecutorEmailReference, "kasa@example.test");
    }

    [Fact]
    public async Task PollingSendsTheDocumentedHeadersReadsEveryPageOfAWindowAndMovesTheCursorWithAnOverlap()
    {
        SetApiSettings();
        var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        var handler = new ScriptedHandler();
        var p1 = NewPackageId();
        var p2 = NewPackageId();
        handler.Answer(HttpStatusCode.OK, "{\"page\":0,\"size\":50,\"totalPages\":2,\"content\":[{\"id\":\"" + p1 + "\",\"packageStatus\":\"Picking\",\"packageModificationDate\":1}]}");
        handler.Answer(HttpStatusCode.OK, "{\"page\":1,\"size\":50,\"totalPages\":2,\"content\":[{\"id\":\"" + p2 + "\",\"packageStatus\":\"Returned\"}]}");
        var source = new TrendyolGoOrderPollingSource(new HttpClient(handler), _secrets, new FixedTime(now));

        var page = await source.PollAsync(null);

        var start = now.AddHours(-1).ToUnixTimeMilliseconds();
        var end = now.AddHours(-1).AddMinutes(15).ToUnixTimeMilliseconds();
        Assert.Equal(2, handler.Requests.Count);
        var request = handler.Requests[0];
        Assert.Equal($"https://stageapi.tgoapis.com/integrator/order/meal/suppliers/107385/packages?packageModificationStartDate={start}&packageModificationEndDate={end}&page=0&size=50",
            request.Uri);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("key:secret")), request.Authorization);
        Assert.Equal(("107385 - ALKAROS", "ALKAROS", "kasa@example.test"), (request.UserAgent, request.AgentName, request.ExecutorUser));
        Assert.EndsWith("&page=1&size=50", handler.Requests[1].Uri, StringComparison.Ordinal);

        Assert.Equal(new[] { ("picking", p1), ("Returned", p2) }, page.Events.Select(e => (e.ProviderStatus, e.ExternalOrderId)));
        Assert.Equal(TrendyolGoEvents.EventKey("picking", p1), page.Events[0].EventKey);
        Assert.Equal((end - 60_000).ToString(System.Globalization.CultureInfo.InvariantCulture), page.NextCursor);

        // Caught up: the window ends now and the cursor never moves backwards.
        handler.Answer(HttpStatusCode.OK, "{\"totalPages\":1,\"content\":[]}");
        var recent = now.AddSeconds(-20).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var caughtUp = await source.PollAsync(recent);
        Assert.Contains($"packageModificationEndDate={now.ToUnixTimeMilliseconds()}&", handler.Requests[2].Uri, StringComparison.Ordinal);
        Assert.Equal(recent, caughtUp.NextCursor);
    }

    [Fact]
    public async Task PollingReportsRateLimitsFailuresAndMissingSettingsAsThePollerExpects()
    {
        var handler = new ScriptedHandler();
        var source = new TrendyolGoOrderPollingSource(new HttpClient(handler), _secrets, TimeProvider.System);
        await Assert.ThrowsAsync<OnlineOrderPollingNotConfiguredException>(() => source.PollAsync(null));
        Assert.Empty(handler.Requests);

        SetApiSettings();
        _secrets.Set(TrendyolGoApiSettings.BaseUrlReference, "http://stageapi.tgoapis.com");
        await Assert.ThrowsAsync<OnlineOrderPollingNotConfiguredException>(() => source.PollAsync(null));
        SetApiSettings();

        handler.Answer(HttpStatusCode.TooManyRequests, "{}", retryAfterSeconds: 42);
        var limited = await Assert.ThrowsAsync<OnlineOrderPollRateLimitedException>(() => source.PollAsync(null));
        Assert.Equal(TimeSpan.FromSeconds(42), limited.RetryAfter);

        handler.Answer(HttpStatusCode.Unauthorized, "{}");
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => source.PollAsync(null));
        handler.Answer(HttpStatusCode.OK, "{\"totalPages\":1,\"content\":[{\"packageStatus\":\"Created\"}]}");
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => source.PollAsync(null));
        for (var i = 0; i < TrendyolGoOrderPollingSource.MaxPages; i++)
            handler.Answer(HttpStatusCode.OK, "{\"totalPages\":99,\"content\":[]}");
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => source.PollAsync(null));
    }

    [Theory]
    [InlineData("GO", false, "GO")]
    [InlineData("STORE", false, "STORE")]
    [InlineData("GO", true, "STORE_PICKUP")]
    public async Task TheDeliveryKindIsKept(string deliveryType, bool pickup, string expected)
    {
        var (_, tgoSku) = await SeedTgoProductAsync();
        var payload = Package(NewPackageId(), tgoSku, deliveryType: deliveryType)
            .Replace("\"storePickupSelected\":false", "\"storePickupSelected\":" + (pickup ? "true" : "false"), StringComparison.Ordinal);
        var result = await Normalizer().NormalizeAsync(payload, DateTimeOffset.UtcNow);
        Assert.Equal(expected, result.Order!.TransportType);
    }

    private TrendyolGoOrderNormalizer Normalizer() => _app!.Services.GetRequiredService<TrendyolGoOrderNormalizer>();

    [Fact]
    public async Task NormalizationReadsQuantityPriceAndTotalsAndRefusesWhatItCannotUse()
    {
        var (productId, tgoSku) = await SeedTgoProductAsync();
        var packageId = NewPackageId();

        var full = (await Normalizer().NormalizeAsync(Package(packageId, tgoSku), DateTimeOffset.UtcNow)).Order!;
        var line = Assert.Single(full.Lines);
        Assert.Equal((tgoSku, productId, 2m, 145m), (line.ExternalSku, line.ProductId, line.Quantity, line.UnitPrice));
        Assert.Equal((packageId, "1E1", "Servis İstiyorum"), (full.ExternalOrderId, full.DisplayCode, full.Comment));
        Assert.Equal((290m, true), (full.ProviderSubTotal, full.TotalsMatch));

        var mismatch = (await Normalizer().NormalizeAsync(Package(packageId, tgoSku, totalPrice: 300m), DateTimeOffset.UtcNow)).Order!;
        Assert.False(mismatch.TotalsMatch);
        var discounted = (await Normalizer().NormalizeAsync(Package(packageId, tgoSku, withCoupon: true, totalPrice: 280m), DateTimeOffset.UtcNow)).Order!;
        Assert.Null(discounted.ProviderSubTotal);
        var delivered = (await Normalizer().NormalizeAsync(
            Package(packageId, tgoSku, totalPrice: 305.99m).Replace("\"totalDeliveryPrice\":null", "\"totalDeliveryPrice\":15.99", StringComparison.Ordinal),
            DateTimeOffset.UtcNow)).Order!;
        Assert.Equal(290m, delivered.ProviderSubTotal);

        Assert.Equal(NormalizationRejection.UnmappedSku, (await Normalizer().NormalizeAsync(Package(packageId, "999999999"), DateTimeOffset.UtcNow)).Rejection);
        Assert.Equal(NormalizationRejection.UnsupportedTransportType,
            (await Normalizer().NormalizeAsync(Package(packageId, tgoSku, deliveryType: "DRONE"), DateTimeOffset.UtcNow)).Rejection);
        Assert.Equal(NormalizationRejection.EmptyOrder, (await Normalizer().NormalizeAsync(
            Package(packageId, tgoSku, cancelSecondItem: true).Replace("\"lineItemId\":1000008181985,\"isCancelled\":false", "\"lineItemId\":1000008181985,\"isCancelled\":true", StringComparison.Ordinal),
            DateTimeOffset.UtcNow)).Rejection);
        Assert.Equal(NormalizationRejection.InvalidPrice, (await Normalizer().NormalizeAsync(
            Package(packageId, tgoSku).Replace("\"price\":145.0,\"unitSellingPrice\":145.0,", "", StringComparison.Ordinal), DateTimeOffset.UtcNow)).Rejection);
        Assert.Equal(NormalizationRejection.MalformedPayload, (await Normalizer().NormalizeAsync("{\"lines\":[]}", DateTimeOffset.UtcNow)).Rejection);
        Assert.Equal(new[] { new OnlineOrderLineReference(tgoSku, 2m) }, TrendyolGoOrderNormalizer.ReadItemReferences(Package(packageId, tgoSku)));
    }

    [Theory]
    [InlineData("created", StatusMappingKind.Command, InternalOrderCommand.AcceptIncomingOrder, null)]
    [InlineData("cancelled", StatusMappingKind.Command, InternalOrderCommand.CancelOrder, null)]
    [InlineData("unsupplied", StatusMappingKind.Command, InternalOrderCommand.CancelOrder, null)]
    [InlineData("shipped", StatusMappingKind.NoOp, null, StatusNoOpReason.HandedToCourier)]
    [InlineData("delivered", StatusMappingKind.NoOp, null, StatusNoOpReason.DeliveredByPlatform)]
    [InlineData("picking", StatusMappingKind.NoOp, null, StatusNoOpReason.OwnOutboundStatusEcho)]
    [InlineData("invoiced", StatusMappingKind.NoOp, null, StatusNoOpReason.OwnOutboundStatusEcho)]
    [InlineData("courierNearby", StatusMappingKind.Unknown, null, null)]
    [InlineData("storeChanged", StatusMappingKind.Unknown, null, null)]
    [InlineData("Returned", StatusMappingKind.Unknown, null, null)]
    public void EveryEventMapsToOneDocumentedOutcome(string status, StatusMappingKind kind, InternalOrderCommand? command, StatusNoOpReason? noOp)
    {
        var provider = _app!.Services.GetServices<IOnlineOrderProvider>().Single(p => p.Provider == TrendyolGoEvents.Provider);
        var result = provider.MapStatus("p1", status, "{\"id\":\"p1\",\"cancelInfo\":{\"reasonCode\":621,\"reason\":\"Tedarik problemi\"}}");
        Assert.Equal((kind, command, noOp), (result.Kind, result.Command, result.NoOpReason));
        if (status == "unsupplied")
            Assert.Equal((CancellationParty.Vendor, "621 Tedarik problemi"), (result.Cancellation!.Party, result.Cancellation.Reason));
        if (status == "cancelled")
            Assert.Equal(CancellationParty.Unrecognized, result.Cancellation!.Party);
        if (kind == StatusMappingKind.Unknown)
            Assert.Equal(result.Evidence!.EvidenceId, provider.MapStatus("p1", status, "{}").Evidence!.EvidenceId);
    }

    /// <summary>Secrets a test can also take away (the channel switched off).</summary>
    private sealed class TestSecrets : ISecretProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

        public void Set(SecretReference reference, string value) => _values[reference.Name] = value;

        public void Remove(SecretReference reference) => _values.TryRemove(reference.Name, out _);

        public string? GetValue(SecretReference reference) => _values.TryGetValue(reference.Name, out var value) ? value : null;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record SeenRequest(string Uri, string? Authorization, string? UserAgent, string? AgentName, string? ExecutorUser);

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body, int? RetryAfter)> _answers = new();

        public List<SeenRequest> Requests { get; } = [];

        public void Answer(HttpStatusCode status, string body, int? retryAfterSeconds = null) => _answers.Enqueue((status, body, retryAfterSeconds));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SeenRequest(
                request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("User-Agent", out var agent) ? string.Join(" ", agent) : null,
                request.Headers.TryGetValues("x-agentname", out var name) ? name.Single() : null,
                request.Headers.TryGetValues("x-executor-user", out var user) ? user.Single() : null));
            var (status, body, retryAfter) = _answers.Dequeue();
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (retryAfter is { } seconds)
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            return Task.FromResult(response);
        }
    }
}
