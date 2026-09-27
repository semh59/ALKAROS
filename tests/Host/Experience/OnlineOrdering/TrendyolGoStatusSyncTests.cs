using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.IntegrationContracts;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.StatusSync;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-TGO-003 over real Postgres: acceptance, handover and restaurant cancellation of a Trendyol Go order are queued in
/// the same transaction as the local change and delivered as the document's calls (EXT:TGO-MEAL-API, read 2026-09-27).
/// UNVERIFIED DRAFT: the calls are checked against the document, not against Trendyol Go (V12-TGO-001 Blocked).
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class TrendyolGoStatusSyncTests : IAsyncLifetime
{
    private static readonly string[] OpenItems = ["1000008723602", "1000008723603"];

    private readonly OnlineOrderingTestDatabase _database = new();
    private readonly InMemorySecretProvider _secrets = new();
    private WebApplication? _app;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _secrets.Set(TrendyolGoWebhookInbox.WebhookSecret, "x");
        _secrets.Set(TrendyolGoApiSettings.BaseUrlReference, "https://stageapi.tgoapis.com");
        _secrets.Set(TrendyolGoApiSettings.SupplierIdReference, "107385");
        _secrets.Set(TrendyolGoApiSettings.ApiKeyReference, "key");
        _secrets.Set(TrendyolGoApiSettings.ApiSecretReference, "secret");
        _secrets.Set(TrendyolGoApiSettings.IntegratorNameReference, "ALKAROS");
        _secrets.Set(TrendyolGoApiSettings.ExecutorEmailReference, "kasa@example.test");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(_secrets);
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddYemeksepetiWebhookExperience();
        builder.Services.AddTrendyolGoWebhookExperience();
        builder.Services.AddTransient(services => new TrendyolGoOrderNormalizer(
            Mappings(), services.GetRequiredService<IProductRepository>(), services.GetRequiredService<ITaxProfileRepository>()));
        builder.Services.AddTransient<IOnlineOrderProvider, TrendyolGoOnlineOrderProvider>();
        _app = builder.Build();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private PostgresYemeksepetiProductMappingService Mappings() => new(
        _database.DataSource, new PostgresProductRepository(_database.DataSource), new PostgresProductModifierGroupRepository(_database.DataSource),
        new PostgresModifierGroupRepository(_database.DataSource), TrendyolGoEvents.Provider);

    private YemeksepetiStatusSyncService Sync => _app!.Services.GetRequiredService<YemeksepetiStatusSyncService>();

    private static string NewPackageId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string Package(string packageId, string tgoSku, string deliveryType = "GO", bool pickup = false) =>
        "{\"id\":\"" + packageId + "\",\"orderCode\":\"1E2\",\"storePickupSelected\":" + (pickup ? "true" : "false") + ",\"deliveryType\":\"" + deliveryType + "\","
        + "\"totalPrice\":182.0,\"packageStatus\":\"Created\",\"lines\":[{\"price\":91.0,\"unitSellingPrice\":91.0,\"items\":["
        + "{\"packageItemId\":\"1000008723602\",\"lineItemId\":1,\"isCancelled\":false},"
        + "{\"packageItemId\":\"1000008723603\",\"lineItemId\":2,\"isCancelled\":false},"
        + "{\"packageItemId\":\"1000008723604\",\"lineItemId\":3,\"isCancelled\":true}],"
        + "\"productId\":" + tgoSku + ",\"name\":\"Penne\",\"modifierProducts\":[],\"extraIngredients\":[],\"removedIngredients\":[]}]}";

    private async Task<(string PackageId, Guid OrderId)> AcceptedOrderAsync(string deliveryType = "GO", bool pickup = false, bool mapped = true)
    {
        var (productId, _) = await _database.SeedSellableProductAsync(onHand: 5m);
        var tgoSku = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (mapped)
            await Mappings().MapAsync(tgoSku, productId, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid());
        var packageId = NewPackageId();
        var body = Encoding.UTF8.GetBytes(Package(packageId, tgoSku, deliveryType, pickup));
        await _app!.Services.GetRequiredService<ProviderInbox>().StoreAsync(new ProviderInboxEvent(
            TrendyolGoEvents.Provider, TrendyolGoEvents.EventKey(TrendyolGoEvents.Created, packageId), packageId, TrendyolGoEvents.Created, null, body));
        var intake = _app.Services.GetRequiredService<YemeksepetiOrderIntakeService>();
        while (await intake.ProcessNextAsync())
        {
        }

        var order = (await _database.OnlineOrdersAsync(packageId)).SingleOrDefault();
        return (packageId, order.OrderId);
    }

    private async Task<IReadOnlyList<TrendyolGoStatusUpdateRequested>> QueuedAsync(string packageId)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            SELECT payload_envelope FROM outbox_messages
            WHERE event_type = $1 AND convert_from(payload_envelope, 'UTF8')::jsonb->>'externalOrderId' = $2
            ORDER BY created_at, id;
            """);
        command.Parameters.AddWithValue(TrendyolGoStatusSync.StatusUpdateRequestedEventType);
        command.Parameters.AddWithValue(packageId);
        var rows = new List<TrendyolGoStatusUpdateRequested>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(IntegrationEventSerializer.Deserialize<TrendyolGoStatusUpdateRequested>(reader.GetFieldValue<byte[]>(0)));
        return rows;
    }

    [Fact]
    public async Task AnAcceptedOrderIsReportedPickedAndItsHandoverFollowsTheDeliveryKind()
    {
        var (platformCourier, goOrder) = await AcceptedOrderAsync("GO");
        Assert.Equal(new[] { TrendyolGoPackageAction.Picked }, (await QueuedAsync(platformCourier)).Select(u => u.Action));
        Assert.Equal(OnlineOrderActionOutcome.Applied, await Sync.HandOverAsync(goOrder, Guid.NewGuid()));
        Assert.Equal(TrendyolGoPackageAction.Invoiced, (await QueuedAsync(platformCourier))[^1].Action);

        var (ownCourier, storeOrder) = await AcceptedOrderAsync("STORE");
        await Sync.HandOverAsync(storeOrder, Guid.NewGuid());
        Assert.Equal(new[] { TrendyolGoPackageAction.Picked, TrendyolGoPackageAction.InvoicedAndShipped },
            (await QueuedAsync(ownCourier)).Select(u => u.Action));

        var (pickup, pickupOrder) = await AcceptedOrderAsync("STORE", pickup: true);
        await Sync.HandOverAsync(pickupOrder, Guid.NewGuid());
        Assert.Equal(TrendyolGoPackageAction.Invoiced, (await QueuedAsync(pickup))[^1].Action);
    }

    [Theory]
    [InlineData(OnlineCancellationReason.ItemUnavailable, 621)]
    [InlineData(OnlineCancellationReason.Closed, 622)]
    [InlineData(OnlineCancellationReason.TooBusy, 623)]
    public async Task ARestaurantCancellationIsReportedUnsuppliedWithTheDocumentedReason(OnlineCancellationReason reason, int reasonId)
    {
        var (packageId, orderId) = await AcceptedOrderAsync();
        Assert.Equal(OnlineOrderActionOutcome.Applied, await Sync.CancelByRestaurantAsync(orderId, reason, Guid.NewGuid()));
        var cancel = (await QueuedAsync(packageId))[^1];
        Assert.Equal((TrendyolGoPackageAction.Unsupplied, (int?)reasonId), (cancel.Action, cancel.ReasonId));
    }

    [Fact]
    public async Task AnOrderRefusedAtIntakeIsReportedUnsuppliedAsASupplyProblemAndNeverPicked()
    {
        var (packageId, orderId) = await AcceptedOrderAsync(mapped: false);
        Assert.Equal(Guid.Empty, orderId);
        var update = Assert.Single(await QueuedAsync(packageId));
        Assert.Equal((TrendyolGoPackageAction.Unsupplied, (int?)621), (update.Action, update.ReasonId));
    }

    [Fact]
    public async Task AnUnsuppliedCallWithoutADocumentedReasonIsNeverQueued()
    {
        await using var connection = await _database.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => TrendyolGoStatusSync.EnqueueAsync(
            new TrendyolGoStatusUpdateRequested(Guid.NewGuid(), "p", TrendyolGoPackageAction.Unsupplied, null, DateTimeOffset.UtcNow),
            connection, transaction));
        await Assert.ThrowsAsync<ArgumentException>(() => TrendyolGoStatusSync.EnqueueAsync(
            new TrendyolGoStatusUpdateRequested(Guid.NewGuid(), "p", TrendyolGoPackageAction.Unsupplied, 601, DateTimeOffset.UtcNow),
            connection, transaction));
        Assert.Null(TrendyolGoStatusSync.ReasonId(null));
    }

    [Fact]
    public async Task TheConsumerSendsTheDocumentedCallsWithTheDocumentedHeadersAndBodies()
    {
        var (packageId, _) = await AcceptedOrderAsync();
        _secrets.Set(TrendyolGoStatusUpdateConsumer.PreparationMinutesReference, "25");
        var handler = new RecordingHandler();
        var consumer = Consumer(new TrendyolGoStatusClient(new HttpClient(handler), _secrets, TimeProvider.System));
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var (action, reason) in new (TrendyolGoPackageAction, int?)[]
                 {
                     (TrendyolGoPackageAction.Picked, null), (TrendyolGoPackageAction.Invoiced, null),
                     (TrendyolGoPackageAction.InvoicedAndShipped, null), (TrendyolGoPackageAction.Unsupplied, 622)
                 })
        {
            await consumer.HandleAsync(TrendyolGoStatusSync.StatusUpdateRequestedEventType,
                IntegrationEventSerializer.Serialize(new TrendyolGoStatusUpdateRequested(Guid.NewGuid(), packageId, action, reason, DateTimeOffset.UtcNow)),
                CancellationToken.None);
        }

        var root = "https://stageapi.tgoapis.com/integrator/order/meal/suppliers/107385/";
        Assert.Equal(
            new[]
            {
                root + "packages/picked", root + "packages/invoiced", root + "packages/invoiced",
                root + $"packages/{packageId}/manual-shipped", root + "packages/unsupplied"
            },
            handler.Calls.Select(c => c.Uri));
        Assert.All(handler.Calls, c => Assert.Equal(("PUT", "107385 - ALKAROS", "ALKAROS", "kasa@example.test"), (c.Method, c.UserAgent, c.AgentName, c.ExecutorUser)));
        Assert.All(handler.Calls, c => Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("key:secret")), c.Authorization));

        var picked = handler.Calls[0].Body;
        Assert.Equal((packageId, 25), (picked.GetProperty("packageId").GetString(), picked.GetProperty("preparationTime").GetInt32()));
        var invoiced = handler.Calls[1].Body;
        Assert.Equal(packageId, invoiced.GetProperty("packageId").GetString());
        Assert.InRange(invoiced.GetProperty("actualDate").GetInt64(), before - 5_000, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1);
        Assert.True(handler.Calls[3].Body.TryGetProperty("actualDate", out _));
        var unsupplied = handler.Calls[4].Body;
        Assert.Equal((packageId, 622), (unsupplied.GetProperty("packageId").GetString(), unsupplied.GetProperty("reasonId").GetInt32()));
        // The package's items that were not already cancelled, from its stored created payload.
        Assert.Equal(OpenItems, unsupplied.GetProperty("itemIdList").EnumerateArray().Select(i => i.GetString()!).ToArray());

        // No or an out-of-range preparation time falls back to 20 minutes.
        foreach (var setting in new[] { "0", "abc", "121" })
        {
            _secrets.Set(TrendyolGoStatusUpdateConsumer.PreparationMinutesReference, setting);
            await consumer.HandleAsync(TrendyolGoStatusSync.StatusUpdateRequestedEventType,
                IntegrationEventSerializer.Serialize(new TrendyolGoStatusUpdateRequested(Guid.NewGuid(), packageId, TrendyolGoPackageAction.Picked, null, DateTimeOffset.UtcNow)),
                CancellationToken.None);
            Assert.Equal(20, handler.Calls[^1].Body.GetProperty("preparationTime").GetInt32());
        }
    }

    private TrendyolGoStatusUpdateConsumer Consumer(TrendyolGoStatusClient client) =>
        new(client, _database.DataSource, _app!.Services.GetRequiredService<ProviderInbox>(), _secrets);

    [Fact]
    public async Task AFailedCallOrMissingSettingsOrAnUnknownPackageFailsTheDeliverySoTheOutboxRetriesIt()
    {
        // V12-TGO-005: a conflict on invoiced means "already invoiced"; any other failure fails the delivery.
        var handler = new RecordingHandler { Status = HttpStatusCode.InternalServerError };
        var consumer = Consumer(new TrendyolGoStatusClient(new HttpClient(handler), _secrets, TimeProvider.System));
        var payload = IntegrationEventSerializer.Serialize(new TrendyolGoStatusUpdateRequested(
            Guid.NewGuid(), NewPackageId(), TrendyolGoPackageAction.Invoiced, null, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => consumer.HandleAsync(TrendyolGoStatusSync.StatusUpdateRequestedEventType, payload, CancellationToken.None));

        handler.Status = HttpStatusCode.OK;
        var unknown = IntegrationEventSerializer.Serialize(new TrendyolGoStatusUpdateRequested(
            Guid.NewGuid(), NewPackageId(), TrendyolGoPackageAction.Unsupplied, 621, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => consumer.HandleAsync(TrendyolGoStatusSync.StatusUpdateRequestedEventType, unknown, CancellationToken.None));

        var noSettings = new TrendyolGoStatusUpdateConsumer(
            new TrendyolGoStatusClient(new HttpClient(handler), new InMemorySecretProvider(), TimeProvider.System),
            _database.DataSource, _app!.Services.GetRequiredService<ProviderInbox>(), _secrets);
        var calls = handler.Calls.Count;
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => noSettings.HandleAsync(TrendyolGoStatusSync.StatusUpdateRequestedEventType, payload, CancellationToken.None));
        Assert.Equal(calls, handler.Calls.Count);
        Assert.True(consumer.CanHandle(TrendyolGoStatusSync.StatusUpdateRequestedEventType));
        Assert.False(consumer.CanHandle("online-ordering.yemeksepeti.status-update-requested.v1"));
    }

    /// <summary>Reconciliation repeats this name (it may not reference OnlineOrdering); a rename must break a test.</summary>
    [Fact]
    public void TheQueuedEventTypeIsTheOneReconciliationWatches() =>
        Assert.Equal("online-ordering.trendyol-go.status-update-requested.v1", TrendyolGoStatusSync.StatusUpdateRequestedEventType);

    [Fact]
    public async Task TheClientNeverExceedsTheEndpointsRateWindow()
    {
        var handler = new RecordingHandler();
        var client = new TrendyolGoStatusClient(new HttpClient(handler), _secrets, TimeProvider.System, requestsPerWindow: 2, window: TimeSpan.FromMilliseconds(400));
        var started = DateTimeOffset.UtcNow;
        await client.InvoicedAsync("p1");
        await client.InvoicedAsync("p2");
        await client.PickedAsync("p3", 20); // another endpoint has its own window
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromMilliseconds(350));
        await client.InvoicedAsync("p4");
        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromMilliseconds(390));
    }

    private sealed record Call(string Method, string Uri, string? Authorization, string? UserAgent, string? AgentName, string? ExecutorUser, JsonElement Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public List<Call> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new Call(
                request.Method.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("User-Agent", out var agent) ? string.Join(" ", agent) : null,
                request.Headers.TryGetValues("x-agentname", out var name) ? name.Single() : null,
                request.Headers.TryGetValues("x-executor-user", out var user) ? user.Single() : null,
                JsonDocument.Parse(body).RootElement.Clone()));
            return new HttpResponseMessage(Status) { Content = new StringContent("{}") };
        }
    }
}
