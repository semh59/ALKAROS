using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Host.Composition;
using ALKAROS.ModuleComposition;
using ALKAROS.OnlineOrdering;
using ALKAROS.OnlineOrdering.AvailabilityPublishing;
using ALKAROS.OnlineOrdering.CatalogPublishing;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.Menu;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-TGO-004 over real Postgres (mappings) and scripted HTTP: Trendyol Go's on-sale status and prices are published
/// as the public document describes them (EXT:TGO-MEAL-API "Menu Integration", read 2026-09-27). UNVERIFIED DRAFT:
/// checked against the document, not against the platform (V12-TGO-001 Blocked).
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class TrendyolGoMenuTests : IAsyncLifetime, IDisposable
{
    private const string Root = "https://stageapi.tgoapis.com/integrator/product/meal/suppliers/107385/";
    private static readonly string[] PublishCalls = ["PUT", "PUT", "POST", "GET", "GET"];
    private static readonly int[] BatchSizes = [1000, 1];

    private readonly OnlineOrderingTestDatabase _database = new();
    private readonly InMemorySecretProvider _secrets = new();
    private readonly ScriptedHandler _handler = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _secrets.Set(TrendyolGoApiSettings.BaseUrlReference, "https://stageapi.tgoapis.com");
        _secrets.Set(TrendyolGoApiSettings.SupplierIdReference, "107385");
        _secrets.Set(TrendyolGoApiSettings.ApiKeyReference, "key");
        _secrets.Set(TrendyolGoApiSettings.ApiSecretReference, "secret");
        _secrets.Set(TrendyolGoApiSettings.IntegratorNameReference, "ALKAROS");
        _secrets.Set(TrendyolGoApiSettings.ExecutorEmailReference, "kasa@example.test");
        _secrets.Set(TrendyolGoMenuClient.StoreIdReference, "153");
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    public void Dispose() => _handler.Dispose();

    private PostgresYemeksepetiProductMappingService Mappings(string provider = TrendyolGoEvents.Provider) => new(
        _database.DataSource, new PostgresProductRepository(_database.DataSource), new PostgresProductModifierGroupRepository(_database.DataSource),
        new PostgresModifierGroupRepository(_database.DataSource), provider);

    private TrendyolGoMenuClient Client(ISecretProvider? secrets = null) =>
        new(new HttpClient(_handler), secrets ?? _secrets, new ImmediateTime());

    private async Task<(Guid ProductId, string TgoId)> MappedProductAsync()
    {
        var (productId, _) = await _database.SeedSellableProductAsync(onHand: 1m);
        var tgoId = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Mappings().MapAsync(tgoId, productId, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid());
        return (productId, tgoId);
    }

    [Fact]
    public async Task AvailabilityIsOnSaleStatusPerProductAndOnlyTrendyolGoMappingsAreItsProducts()
    {
        var (productId, tgoId) = await MappedProductAsync();
        var (_, soldId) = await MappedProductAsync();
        var publisher = new TrendyolGoAvailabilityPublisher(Mappings(), Client());

        Assert.True(publisher.IsEnabled);
        var products = await publisher.PublishedProductsAsync(1000);
        Assert.Contains(new PublishedChannelProduct(productId, tgoId), products);
        // SeedSellableProductAsync also made a Yemeksepeti mapping; that one is not Trendyol Go's.
        Assert.All(products, p => Assert.Matches("^[0-9]+$", p.ExternalId));

        _handler.Answer(HttpStatusCode.OK, "{}");
        _handler.Answer(HttpStatusCode.Conflict, "{}");
        _handler.Answer(HttpStatusCode.OK, "{}");
        await publisher.PublishAsync([new ChannelAvailability(tgoId, 3), new ChannelAvailability(soldId, 0)]);

        Assert.Equal(3, _handler.Calls.Count);
        Assert.Equal(($"{Root}stores/153/products/{tgoId}/status", "PUT", "ACTIVE"),
            (_handler.Calls[0].Uri, _handler.Calls[0].Method, _handler.Calls[0].Body.GetProperty("status").GetString()));
        // A concurrent change of the same store (409) is sent once more.
        Assert.All(_handler.Calls.Skip(1), c => Assert.Equal(($"{Root}stores/153/products/{soldId}/status", "PASSIVE"),
            (c.Uri, c.Body.GetProperty("status").GetString())));
        Assert.All(_handler.Calls, c => Assert.Equal(("107385 - ALKAROS", "ALKAROS", "kasa@example.test"), (c.UserAgent, c.AgentName, c.ExecutorUser)));

        _handler.Answer(HttpStatusCode.Conflict, "{}");
        _handler.Answer(HttpStatusCode.Conflict, "{}");
        await Assert.ThrowsAsync<TrendyolGoMenuException>(() => publisher.PublishAsync([new ChannelAvailability(tgoId, 1)]));
    }

    [Fact]
    public void TheChannelIsOffUntilEverySettingIncludingTheStoreIsEntered()
    {
        var partial = new InMemorySecretProvider();
        partial.Set(TrendyolGoApiSettings.BaseUrlReference, "https://stageapi.tgoapis.com");
        Assert.False(new TrendyolGoAvailabilityPublisher(Mappings(), Client(partial)).IsEnabled);
        var noStore = new InMemorySecretProvider();
        foreach (var reference in new[] { TrendyolGoApiSettings.BaseUrlReference, TrendyolGoApiSettings.SupplierIdReference, TrendyolGoApiSettings.ApiKeyReference,
                     TrendyolGoApiSettings.ApiSecretReference, TrendyolGoApiSettings.IntegratorNameReference, TrendyolGoApiSettings.ExecutorEmailReference })
            noStore.Set(reference, _secrets.GetValue(reference)!);
        Assert.False(new TrendyolGoAvailabilityPublisher(Mappings(), Client(noStore)).IsEnabled);
        noStore.Set(TrendyolGoMenuClient.StoreIdReference, "153");
        Assert.True(new TrendyolGoAvailabilityPublisher(Mappings(), Client(noStore)).IsEnabled);
    }

    [Fact]
    public async Task AProductIsPublishedOnlyUnderItsMappingAndOnlyWhenTheStoreMenuHasIt()
    {
        var (onMenu, onMenuId) = await MappedProductAsync();
        var (offMenu, _) = await MappedProductAsync();
        var (unmapped, _) = await _database.SeedSellableProductAsync(onHand: 1m);
        _handler.Answer(HttpStatusCode.OK, "{\"products\":[{\"id\":" + onMenuId + ",\"status\":\"ACTIVE\"},{\"id\":3,\"status\":\"ACTIVE\"}],\"sections\":[]}");
        var publisher = new TrendyolGoCatalogPublisher(Mappings(), Client(), new ImmediateTime());

        Assert.Equal(onMenuId, await publisher.AssignExternalIdAsync(onMenu, "ON-1", createIfMissing: true, Guid.NewGuid()));
        Assert.Null(await publisher.AssignExternalIdAsync(offMenu, "ON-2", createIfMissing: true, Guid.NewGuid()));
        // Never made up from the catalog SKU: the platform numbers its products.
        Assert.Null(await publisher.AssignExternalIdAsync(unmapped, "ON-3", createIfMissing: true, Guid.NewGuid()));
        Assert.Null(await Mappings().FindOpenSkuForProductAsync(unmapped));
        // The menu is read once per publication.
        Assert.Equal($"{Root}stores/153/products", Assert.Single(_handler.Calls).Uri);
        Assert.Equal(new[] { CatalogCapability.UpdatePrice, CatalogCapability.UpdateAvailability }.ToHashSet(), publisher.SupportedCapabilities.ToHashSet());
        Assert.Equal("Trendyol Go", publisher.Channel);
    }

    [Fact]
    public async Task APublicationSendsStatusAndQueuedPricesAndWaitsForTheBatchResult()
    {
        var publisher = new TrendyolGoCatalogPublisher(Mappings(), Client(), new ImmediateTime());
        _handler.Answer(HttpStatusCode.OK, "{}");
        _handler.Answer(HttpStatusCode.OK, "{}");
        _handler.Answer(HttpStatusCode.OK, "{\"batchRequestId\":\"b-1\"}");
        _handler.Answer(HttpStatusCode.OK, "{\"status\":\"IN_PROGRESS\",\"failedItemCount\":0,\"items\":[{\"status\":\"IN_PROGRESS\",\"failureReasons\":[],\"requestItem\":{\"productId\":11}}]}");
        _handler.Answer(HttpStatusCode.OK, "{\"status\":\"COMPLETED\",\"failedItemCount\":0,\"items\":[{\"status\":\"SUCCESS\",\"failureReasons\":[],\"requestItem\":{\"productId\":11}}]}");

        var job = await publisher.PublishAsync([
            new CatalogPublicationItem(Guid.NewGuid(), "11", "Adana", 15.50m, true),
            new CatalogPublicationItem(Guid.NewGuid(), "12", "Çorba", 5m, false)]);

        Assert.Equal("b-1", job);
        Assert.Equal(PublishCalls, _handler.Calls.Select(c => c.Method));
        Assert.Equal(("ACTIVE", "PASSIVE"), (_handler.Calls[0].Body.GetProperty("status").GetString(), _handler.Calls[1].Body.GetProperty("status").GetString()));
        var price = _handler.Calls[2];
        Assert.Equal($"{Root}products/price", price.Uri);
        var items = price.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal((11L, 15.50m, 12L, 5m), (items[0].GetProperty("productId").GetInt64(), items[0].GetProperty("sellingPrice").GetDecimal(),
            items[1].GetProperty("productId").GetInt64(), items[1].GetProperty("sellingPrice").GetDecimal()));
        Assert.Equal($"{Root}batch-requests/b-1", _handler.Calls[3].Uri);
    }

    [Fact]
    public async Task ARefusedPriceFailsThePublicationWithTheProductAndReason()
    {
        var publisher = new TrendyolGoCatalogPublisher(Mappings(), Client(), new ImmediateTime());
        _handler.Answer(HttpStatusCode.OK, "{}");
        _handler.Answer(HttpStatusCode.OK, "{\"batchRequestId\":\"b-2\"}");
        _handler.Answer(HttpStatusCode.OK, "{\"status\":\"COMPLETED\",\"failedItemCount\":1,\"items\":[{\"status\":\"FAILED\",\"failureReasons\":[\"Fiyat çok yüksek\"],\"requestItem\":{\"productId\":21}}]}");

        var refused = await Assert.ThrowsAsync<TrendyolGoMenuException>(() => publisher.PublishAsync([new CatalogPublicationItem(Guid.NewGuid(), "21", "Döner", 999m, true)]));
        Assert.Contains("21 Fiyat çok yüksek", refused.Message, StringComparison.Ordinal);
        Assert.Contains("b-2", refused.Message, StringComparison.Ordinal);

        _handler.Answer(HttpStatusCode.OK, "{}");
        _handler.Answer(HttpStatusCode.BadRequest, "{\"exception\":\"X\",\"errors\":[{\"key\":\"k\",\"message\":\"Repeated price update requests cannot be sent\"}]}");
        var repeated = await Assert.ThrowsAsync<TrendyolGoMenuException>(() => publisher.PublishAsync([new CatalogPublicationItem(Guid.NewGuid(), "21", "Döner", 999m, true)]));
        Assert.Contains("HTTP 400: Repeated price update requests cannot be sent", repeated.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MoreThanAThousandPricesGoInSeveralBatches()
    {
        var publisher = new TrendyolGoCatalogPublisher(Mappings(), Client(), new ImmediateTime());
        var items = Enumerable.Range(1, 1001).Select(i => new CatalogPublicationItem(Guid.NewGuid(), i.ToString(System.Globalization.CultureInfo.InvariantCulture), "x", 1m, true)).ToList();
        for (var i = 0; i < 1001; i++)
            _handler.Answer(HttpStatusCode.OK, "{}");
        _handler.Answer(HttpStatusCode.OK, "{\"batchRequestId\":\"a\"}");
        _handler.Answer(HttpStatusCode.OK, "{\"status\":\"COMPLETED\",\"failedItemCount\":0,\"items\":[]}");
        _handler.Answer(HttpStatusCode.OK, "{\"batchRequestId\":\"b\"}");
        _handler.Answer(HttpStatusCode.OK, "{\"status\":\"COMPLETED\",\"failedItemCount\":0,\"items\":[]}");

        Assert.Equal("b", await publisher.PublishAsync(items));
        var posts = _handler.Calls.Where(c => c.Method == "POST").ToList();
        Assert.Equal(BatchSizes, posts.Select(p => p.Body.GetProperty("items").GetArrayLength()));
        await Assert.ThrowsAsync<ArgumentException>(() => Client().UpdatePricesAsync([]));
    }

    [Fact]
    public async Task TheModuleRegistersTrendyolGoAsACatalogAndAvailabilityChannel()
    {
        var context = new ModuleContext();
        new OnlineOrderingModule().Register(context);
        var services = new ServiceCollection();
        HostComposition.ApplyComposedModuleServices(services, context.Services);
        services.AddSingleton(_database.DataSource);
        services.AddSingleton<ISecretProvider>(_secrets);
        services.AddTransient<IProductRepository>(_ => new PostgresProductRepository(_database.DataSource));
        services.AddTransient<IProductModifierGroupRepository>(_ => new PostgresProductModifierGroupRepository(_database.DataSource));
        services.AddTransient<IModifierGroupRepository>(_ => new PostgresModifierGroupRepository(_database.DataSource));
        await using var provider = services.BuildServiceProvider();
        Assert.Contains(provider.GetServices<ICatalogChannelPublisher>(), p => p.Channel == "Trendyol Go");
        Assert.Contains(provider.GetServices<IAvailabilityChannelPublisher>(), p => p.Channel == "Trendyol Go");
    }

    /// <summary>A clock whose waits end at once, so a publication's batch checks do not slow the tests.</summary>
    private sealed class ImmediateTime : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoTimer();
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed record Call(string Method, string Uri, string? UserAgent, string? AgentName, string? ExecutorUser, JsonElement Body);

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _answers = new();

        public List<Call> Calls { get; } = [];

        public void Answer(HttpStatusCode status, string body) => _answers.Enqueue((status, body));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Calls)
            {
                Calls.Add(new Call(request.Method.Method, request.RequestUri!.ToString(),
                    request.Headers.TryGetValues("User-Agent", out var agent) ? string.Join(" ", agent) : null,
                    request.Headers.TryGetValues("x-agentname", out var name) ? name.Single() : null,
                    request.Headers.TryGetValues("x-executor-user", out var user) ? user.Single() : null,
                    JsonDocument.Parse(body).RootElement.Clone()));
            }

            var (status, answer) = _answers.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }
}
