using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Experience.OnlineOrdering;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;
using ALKAROS.OnlineOrdering.OrderLinks;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-ONL-002 over real Postgres: stored webhook events become exactly one Accepted order with its
/// holds and kitchen ticket, or no order and a typed reason. The payloads are our own fixtures shaped
/// after the public Partner API v2.0.2 document; no real Yemeksepeti delivery exists (V0-YSP-001).
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class YemeksepetiOrderIntakeTests : IAsyncLifetime
{
    private const string Secret = "Bearer static-portal-token";

    private static readonly string?[] CreatedThenReplayed = ["OrderCreated", "OrderAlreadyExists"];
    private static readonly string?[] DivergedThenSkipped = ["Diverged", "SkippedCancellationRequested"];
    private static readonly string?[] AllergyNote = ["Soğansız, fıstık alerjisi"];

    private readonly OnlineOrderingTestDatabase _database = new();
    private WebApplication? _app;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        secrets.Set(YemeksepetiWebhookInbox.WebhookSecret, Secret);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(secrets);
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddYemeksepetiWebhookExperience();
        _app = builder.Build();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private YemeksepetiWebhookInbox Inbox => _app!.Services.GetRequiredService<YemeksepetiWebhookInbox>();

    private YemeksepetiOrderIntakeService Intake => _app!.Services.GetRequiredService<YemeksepetiOrderIntakeService>();

    private static string NewOrderId() => Guid.NewGuid().ToString("D");

    private static byte[] Delivery(string orderId, string status, string updatedAt, params (string Sku, int Quantity)[] items)
    {
        var lines = string.Join(",", items.Select(i =>
            "{\"_id\":\"" + Guid.NewGuid().ToString("N") + "\",\"sku\":\"" + i.Sku + "\",\"name\":\"Pide\","
            + "\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":" + i.Quantity + ",\"unit_price\":150.00}}"));
        var json = "{\"order_id\":\"" + orderId + "\",\"external_order_id\":\"YS-" + orderId[..6] + "\",\"status\":\"" + status + "\","
                   + "\"transport_type\":\"LOGISTICS_DELIVERY\",\"comment\":\"Zil çalışmıyor\","
                   + "\"customer\":{\"first_name\":\"Ayşe\",\"phone_number\":\"+905551112233\"},"
                   + "\"items\":[" + lines + "],\"sys\":{\"updated_at\":\"" + updatedAt + "\"}}";
        return Encoding.UTF8.GetBytes(json);
    }

    private async Task StoreAsync(byte[] body) =>
        Assert.Equal(WebhookReceiptOutcome.Stored, (await Inbox.ReceiveAsync(Secret, body)).Outcome);

    private async Task<int> DrainAsync()
    {
        var processed = 0;
        while (await Intake.ProcessNextAsync())
            processed++;
        return processed;
    }

    [Fact]
    public async Task ANewProviderOrderBecomesOneAcceptedOrderWithItsHoldsAndKitchenTicket()
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 3m);
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 2)));

        Assert.Equal(1, await DrainAsync());

        var order = Assert.Single(await _database.OnlineOrdersAsync(orderId));
        Assert.Equal("Accepted", order.Status);
        Assert.Equal("YS-" + orderId, order.OrderNumber);
        // V12-RMD-007: the customer's note never reaches the order; it stays in the encrypted payload only.
        Assert.Equal($"Yemeksepeti YS-{orderId[..6]}", order.Notes);
        Assert.Equal(new[] { ("Reserved", "Online") }, await _database.HoldsAsync(order.OrderId));
        Assert.Equal(1, await _database.KitchenTicketItemCountAsync(order.OrderId));
        Assert.Equal(1m, await _database.AvailableAsync(productId));
        var inbox = Assert.Single(await _database.InboxAsync(orderId));
        Assert.Equal(("OrderCreated", (Guid?)order.OrderId), (inbox.Outcome, inbox.OrderId));
    }

    [Fact]
    public async Task TheSameProviderOrderDeliveredTwiceIsStillOneOrder()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 1)));
        await StoreAsync(Delivery(orderId, "RECEIVED", "t2", (sku, 1)));

        Assert.Equal(2, await DrainAsync());

        var order = Assert.Single(await _database.OnlineOrdersAsync(orderId));
        Assert.Equal(CreatedThenReplayed, (await _database.InboxAsync(orderId)).Select(e => e.Outcome).ToArray());
        Assert.All(await _database.InboxAsync(orderId), e => Assert.Equal(order.OrderId, e.OrderId));
    }

    [Fact]
    public async Task ParallelProcessorsOnDuplicateEventsStillCreateOneOrder()
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var orderId = NewOrderId();
        for (var i = 0; i < 4; i++)
            await StoreAsync(Delivery(orderId, "RECEIVED", "t" + i, (sku, 1)));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workers = Enumerable.Range(0, 4).Select(async _ =>
        {
            await start.Task;
            return await DrainAsync();
        }).ToList();
        start.SetResult();

        Assert.Equal(4, (await Task.WhenAll(workers)).Sum());
        Assert.Single(await _database.OnlineOrdersAsync(orderId));
        Assert.Equal(4m, await _database.AvailableAsync(productId));
    }

    [Fact]
    public async Task WhenTheLastPortionIsHeldElsewhereNoOrderIsWrittenAndTheDivergenceIsRecorded()
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        Assert.Equal(CrossChannelReservationOutcome.Reserved, await _database.HoldElsewhereAsync(productId));
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 1)));

        await DrainAsync();

        Assert.Empty(await _database.OnlineOrdersAsync(orderId));
        var inbox = Assert.Single(await _database.InboxAsync(orderId));
        Assert.Equal("Diverged", inbox.Outcome);
        Assert.Contains("OutOfStock", inbox.Detail);
        Assert.Equal(0m, await _database.AvailableAsync(productId));
    }

    [Fact]
    public async Task TwoProviderOrdersRacingForTheLastPortionLeaveOneOrderAndOneDivergence()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        var first = NewOrderId();
        var second = NewOrderId();
        await StoreAsync(Delivery(first, "RECEIVED", "t1", (sku, 1)));
        await StoreAsync(Delivery(second, "RECEIVED", "t1", (sku, 1)));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workers = Enumerable.Range(0, 2).Select(async _ =>
        {
            await start.Task;
            return await DrainAsync();
        }).ToList();
        start.SetResult();
        await Task.WhenAll(workers);

        var outcomes = (await _database.InboxAsync(first)).Concat(await _database.InboxAsync(second)).Select(e => e.Outcome).ToList();
        Assert.Equal(1, outcomes.Count(o => o == "OrderCreated"));
        Assert.Equal(1, outcomes.Count(o => o == "Diverged"));
        Assert.Equal(1, (await _database.OnlineOrdersAsync(first)).Count + (await _database.OnlineOrdersAsync(second)).Count);
    }

    [Fact]
    public async Task AnUnmappedSkuRejectsTheOrderWithoutWritingAnything()
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 1), ("ys-not-mapped", 1)));

        await DrainAsync();

        Assert.Empty(await _database.OnlineOrdersAsync(orderId));
        var inbox = Assert.Single(await _database.InboxAsync(orderId));
        Assert.Equal("Rejected", inbox.Outcome);
        Assert.Contains(nameof(NormalizationRejection.UnmappedSku), inbox.Detail);
        Assert.Equal(2m, await _database.AvailableAsync(productId));
    }

    [Fact]
    public async Task KnownNoOpUnknownAndOrderlessCancellationStatusesChangeNoOrder()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "DISPATCHED", "t1", (sku, 1)));
        await StoreAsync(Delivery(orderId, "PREPARING", "t2", (sku, 1)));
        await StoreAsync(Delivery(orderId, "CANCELLED", "t3", (sku, 1)));

        Assert.Equal(3, await DrainAsync());

        Assert.Empty(await _database.OnlineOrdersAsync(orderId));
        var inbox = await _database.InboxAsync(orderId);
        Assert.Equal(
            new[] { ("DISPATCHED", "NoOp"), ("PREPARING", "UnknownStatus"), ("CANCELLED", (string?)"CancelledBeforeOrder") },
            inbox.Select(e => (e.Status, e.Outcome)).ToArray());
        Assert.Contains("EvidenceId", inbox[1].Detail);
    }

    [Fact]
    public async Task APoisonEventIsRetriedBoundedlyThenClosedWithoutBlockingOthers()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var poisoned = NewOrderId();
        var healthy = NewOrderId();
        await StoreAsync(Delivery(poisoned, "RECEIVED", "t1", (sku, 1)));
        await _database.CorruptEnvelopeAsync(poisoned);
        await StoreAsync(Delivery(healthy, "RECEIVED", "t1", (sku, 1)));

        var failures = 0;
        for (var pass = 0; pass < 30; pass++)
        {
            try
            {
                if (!await Intake.ProcessNextAsync())
                {
                    // V12-RMD-004: the poisoned event waits between attempts; nothing else is left, so its wait
                    // is made due. Stop once it is closed and nothing waits any more.
                    if ((await _database.InboxAsync(poisoned))[0].Outcome is not null)
                        break;
                    await _database.ExpireRetryWaitsAsync();
                }
            }
            catch (Exception)
            {
                failures++;
            }
        }

        Assert.Equal(YemeksepetiInboxProcessingStore.MaxAttempts, failures);
        var poison = Assert.Single(await _database.InboxAsync(poisoned));
        Assert.Equal(("Failed", YemeksepetiInboxProcessingStore.MaxAttempts), (poison.Outcome, poison.Attempts));
        Assert.Equal("OrderCreated", Assert.Single(await _database.InboxAsync(healthy)).Outcome);
    }

    private static byte[] Custom(string orderId, string updatedAt, string transportType, string itemExtra, string sku, string paymentJson = "")
    {
        var json = "{\"order_id\":\"" + orderId + "\",\"external_order_id\":\"YS-" + orderId[..6] + "\",\"status\":\"RECEIVED\","
                   + "\"transport_type\":\"" + transportType + "\",\"comment\":\"Kapıda ödeme yok, 0555 111 22 33\","
                   + "\"items\":[{\"_id\":\"" + Guid.NewGuid().ToString("N") + "\",\"sku\":\"" + sku + "\",\"name\":\"Pide\","
                   + itemExtra
                   + "\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":1,\"unit_price\":150.00}}]"
                   + paymentJson + ",\"sys\":{\"updated_at\":\"" + updatedAt + "\"}}";
        return Encoding.UTF8.GetBytes(json);
    }

    [Fact]
    public async Task AFailingEventWaitsLongerAfterEachAttemptAndIsNotClaimedBeforeItIsDue()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        var poisoned = NewOrderId();
        await StoreAsync(Delivery(poisoned, "RECEIVED", "t1", (sku, 1)));
        await _database.CorruptEnvelopeAsync(poisoned);

        await Assert.ThrowsAnyAsync<Exception>(() => Intake.ProcessNextAsync());
        var firstWait = await _database.SecondsUntilNextAttemptAsync(poisoned);
        Assert.False(await Intake.ProcessNextAsync());

        await _database.ExpireRetryWaitsAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => Intake.ProcessNextAsync());
        var secondWait = await _database.SecondsUntilNextAttemptAsync(poisoned);

        Assert.InRange(firstWait, 5, 11);
        Assert.InRange(secondWait, 15, 21);
    }

    [Fact]
    public async Task ATimeoutThatIsNotAShutdownUsesUpAnAttempt()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 1)));
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), EnvelopeKey());
        secrets.Set(YemeksepetiWebhookInbox.WebhookSecret, Secret);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(secrets);
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddYemeksepetiWebhookExperience();
        builder.Services.AddSingleton<ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping.IYemeksepetiProductMappingService, TimingOutMappings>();
        await using var app = builder.Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => app.Services.GetRequiredService<YemeksepetiOrderIntakeService>().ProcessNextAsync());

        var inbox = Assert.Single(await _database.InboxAsync(orderId));
        Assert.Equal((null, 1), (inbox.Outcome, inbox.Attempts));
    }

    private string EnvelopeKey() =>
        _app!.Services.GetRequiredService<ISecretProvider>().GetValue(new SecretReference("envelope-master-key"))!;

    private sealed class TimingOutMappings : ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping.IYemeksepetiProductMappingService
    {
        public Task<ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping.ProductMappingResolution> ResolveAsync(
            string externalSku, DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException("The mapping lookup timed out.");

        public Task<ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping.YemeksepetiProductMapping> MapAsync(
            string externalSku, Guid productId, DateTimeOffset effectiveFrom, Guid actorId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindOpenSkuForProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping.YemeksepetiProductMapping>> ListOpenMappingsAsync(
            int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task ASecondReceivedAfterARefusalWithACancellationRequestCreatesNoOrder()
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 0m);
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 1)));
        Assert.Equal(1, await DrainAsync());

        // Stock arrives, then the provider sends the same order again with a new update time.
        await _database.AddOnHandAsync(productId, 5m);
        await StoreAsync(Delivery(orderId, "RECEIVED", "t2", (sku, 1)));
        Assert.Equal(1, await DrainAsync());

        Assert.Empty(await _database.OnlineOrdersAsync(orderId));
        Assert.Equal(DivergedThenSkipped, (await _database.InboxAsync(orderId)).Select(e => e.Outcome).ToArray());
    }

    [Fact]
    public async Task ItemInstructionsBecomeTheItemNoteWhileTheOrderCommentDoesNot()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var orderId = NewOrderId();
        await StoreAsync(Custom(orderId, "t1", "LOGISTICS_DELIVERY", "\"instructions\":\"Soğansız, fıstık alerjisi\",\"status\":\"IN_CART\",", sku));

        Assert.Equal(1, await DrainAsync());

        var order = Assert.Single(await _database.OnlineOrdersAsync(orderId));
        Assert.Equal(AllergyNote, (await _database.OrderItemNotesAsync(order.OrderId)).ToArray());
    }

    [Fact]
    public async Task ANewOrderWithAnUnknownDeliveryKindCreatesNothingAndIsRecordedAsUnknown()
    {
        // The status mapper stops it before normalization (no documented handover exists for it); V12-RMD-004 makes
        // such an event a reconciliation case instead of a silent dead end.
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var orderId = NewOrderId();
        await StoreAsync(Custom(orderId, "t1", "PICKUP", "", sku));

        Assert.Equal(1, await DrainAsync());

        Assert.Empty(await _database.OnlineOrdersAsync(orderId));
        Assert.Equal("UnknownStatus", Assert.Single(await _database.InboxAsync(orderId)).Outcome);
        Assert.Empty(await _database.OutboundStatusUpdatesAsync(orderId));
    }

    [Theory]
    [InlineData("LOGISTICS_DELIVERY", "\"status\":\"REMOVED\",", "UnsupportedItemStatus")]
    [InlineData("LOGISTICS_DELIVERY", "\"replaced_id\":\"a1b2\",", "UnsupportedItemStatus")]
    public async Task AnOrderThatCouldNeverBeHandledIsRefusedForAPersonWithoutACancellationRequest(
        string transportType, string itemExtra, string rejection)
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var orderId = NewOrderId();
        await StoreAsync(Custom(orderId, "t1", transportType, itemExtra, sku));

        Assert.Equal(1, await DrainAsync());

        Assert.Empty(await _database.OnlineOrdersAsync(orderId));
        var inbox = Assert.Single(await _database.InboxAsync(orderId));
        Assert.Equal("Rejected", inbox.Outcome);
        Assert.Contains($"\"rejection\": \"{rejection}\"", inbox.Detail);
        Assert.Contains("\"providerCancellationRequested\": false", inbox.Detail);
        Assert.Empty(await _database.OutboundStatusUpdatesAsync(orderId));
    }

    [Theory]
    [InlineData(",\"payment\":{\"sub_total\":150.00,\"order_total\":165.00}", "true")]
    [InlineData(",\"payment\":{\"sub_total\":140.00,\"order_total\":155.00}", "false")]
    public async Task TheProviderSubTotalIsComparedAndRecordedWithoutStoppingTheOrder(string payment, string match)
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var orderId = NewOrderId();
        await StoreAsync(Custom(orderId, "t1", "VENDOR_DELIVERY", "", sku, payment));

        Assert.Equal(1, await DrainAsync());

        Assert.Single(await _database.OnlineOrdersAsync(orderId));
        var inbox = Assert.Single(await _database.InboxAsync(orderId));
        Assert.Equal("OrderCreated", inbox.Outcome);
        Assert.Contains($"\"totalsMatch\": {match}", inbox.Detail);
        Assert.Contains("\"localSubTotal\": 150", inbox.Detail);
    }

    [Theory]
    [InlineData(150.00, "true", 0)]
    [InlineData(140.00, "false", 10)]
    public async Task AProviderPriceDifferentFromTheCatalogIsRecordedWithoutStoppingTheOrder(
        decimal catalogPrice, string match, int differenceAmount)
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        await _database.ExecAsync(
            "UPDATE catalog.products SET current_price = @price WHERE product_id = @id;", ("price", catalogPrice), ("id", productId));
        var orderId = NewOrderId();
        await StoreAsync(Custom(orderId, "t1", "VENDOR_DELIVERY", "", sku));

        Assert.Equal(1, await DrainAsync());

        Assert.Single(await _database.OnlineOrdersAsync(orderId));
        var inbox = Assert.Single(await _database.InboxAsync(orderId));
        Assert.Equal("OrderCreated", inbox.Outcome);
        Assert.Contains($"\"pricesMatch\": {match}", inbox.Detail);
        Assert.Contains($"\"priceDifferenceAmount\": {differenceAmount}", inbox.Detail);
        if (match == "false")
            Assert.Contains($"\"catalogUnitPrice\": 140", inbox.Detail);
    }

    private async Task<(Guid OrderId, string Provider)?> LinkAsync(string externalOrderId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT order_id, provider FROM online_ordering.online_orders WHERE external_order_id = $1 AND provider = 'yemeksepeti';");
        command.Parameters.AddWithValue(externalOrderId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? (reader.GetGuid(0), reader.GetString(1)) : null;
    }

    private async Task LinkDirectlyAsync(Guid orderId, string provider, string externalOrderId)
    {
        await using var connection = await _database.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await OnlineOrderLinkStore.LinkAsync(orderId, provider, externalOrderId, connection, transaction);
        await transaction.CommitAsync();
    }

    private Task InsertOtherPlatformOrderAsync(Guid orderId, string externalOrderId) =>
        _database.ExecAsync(
            """
            INSERT INTO orders.orders (order_id, source, source_external_id, status, confirmation_status, order_number, created_at, updated_at)
            VALUES (@id, 'Online', @external, 'Accepted', 'Accepted', @number, now(), now());
            """,
            ("id", orderId), ("external", externalOrderId), ("number", "TG-" + externalOrderId[..8]));

    [Fact]
    public async Task AnOnlineOrderIsLinkedToItsPlatformAndOnePlatformNumberNeverBecomesTwoOrders()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 1)));
        Assert.Equal(1, await DrainAsync());
        var local = Assert.Single(await _database.OnlineOrdersAsync(orderId)).OrderId;

        Assert.Equal((local, OnlineOrderProviders.Yemeksepeti), await LinkAsync(orderId));
        var duplicate = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => LinkDirectlyAsync(Guid.NewGuid(), OnlineOrderProviders.Yemeksepeti, orderId));
        Assert.Equal("23505", duplicate.SqlState);
    }

    [Fact]
    public async Task TwoPlatformsMayUseTheSameOrderNumberAndEachFindsOnlyItsOwn()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var sharedNumber = NewOrderId();
        // Another platform's order with the same number exists first; Yemeksepeti's own order is still created.
        var otherOrder = Guid.NewGuid();
        await InsertOtherPlatformOrderAsync(otherOrder, sharedNumber);
        await LinkDirectlyAsync(otherOrder, "trendyol-go", sharedNumber);

        await StoreAsync(Delivery(sharedNumber, "RECEIVED", "t1", (sku, 1)));
        Assert.Equal(1, await DrainAsync());
        var yemeksepetiOrder = Assert.Single(await _database.OnlineOrdersAsync(sharedNumber), o => o.OrderId != otherOrder).OrderId;
        Assert.Equal("OrderCreated", Assert.Single(await _database.InboxAsync(sharedNumber)).Outcome);

        await using (var connection = await _database.DataSource.OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync();
            Assert.Equal(yemeksepetiOrder,
                await OnlineOrderLinkStore.FindOrderIdAsync(OnlineOrderProviders.Yemeksepeti, sharedNumber, connection, transaction));
            Assert.Equal(otherOrder, await OnlineOrderLinkStore.FindOrderIdAsync("trendyol-go", sharedNumber, connection, transaction));
            Assert.Null(await OnlineOrderLinkStore.FindOrderIdAsync("migros-yemek", sharedNumber, connection, transaction));
            await transaction.CommitAsync();
        }

        // A Yemeksepeti cancellation for that number reaches only the Yemeksepeti order.
        await StoreAsync(Delivery(sharedNumber, "CANCELLED", "t2", (sku, 1)));
        await DrainAsync();
        Assert.Equal("Accepted", await _database.ScalarTextAsync($"SELECT status FROM orders.orders WHERE order_id = '{otherOrder}';"));
        Assert.Equal("Cancelled", await _database.ScalarTextAsync($"SELECT status FROM orders.orders WHERE order_id = '{yemeksepetiOrder}';"));
    }

    [Fact]
    public async Task ThePlatformLinkMigrationBackfillsRollsBackOnlyWhenSafeAndReapplies()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var existing = NewOrderId();
        await StoreAsync(Delivery(existing, "RECEIVED", "t1", (sku, 1)));
        Assert.Equal(1, await DrainAsync());

        await _database.RunFixtureAsync("153-online-order-provider-link.down.sql");
        await _database.RunFixtureAsync("153-online-order-provider-link.up.sql");
        Assert.Equal(OnlineOrderProviders.Yemeksepeti, (await LinkAsync(existing))?.Provider);

        // Two platforms now share a number: going back to the platform-blind rule would have to drop one of them.
        var otherOrder = Guid.NewGuid();
        await InsertOtherPlatformOrderAsync(otherOrder, existing);
        var refused = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => _database.RunFixtureAsync("153-online-order-provider-link.down.sql"));
        Assert.Contains("rollback refused", refused.MessageText);
        await _database.ExecAsync("DELETE FROM orders.orders WHERE order_id = @id;", ("id", otherOrder));
    }

    [Fact]
    public async Task TheIntakeCorrectnessMigrationRollsBackAndReapplies()
    {
        // V12-ONL-006: 153 replaced 150's platform-blind index, so 150 is rolled back and reapplied beneath it.
        await _database.RunFixtureAsync("153-online-order-provider-link.down.sql");
        await _database.RunFixtureAsync("150-online-intake-backoff-and-unique-order.down.sql");
        await _database.RunFixtureAsync("150-online-intake-backoff-and-unique-order.up.sql");
        await _database.RunFixtureAsync("153-online-order-provider-link.up.sql");
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 1)));

        Assert.Equal(1, await DrainAsync());
        Assert.Equal("OrderCreated", Assert.Single(await _database.InboxAsync(orderId)).Outcome);
    }

    [Fact]
    public async Task TheScrubMigrationRemovesCustomerNotesFromOnlineOrdersOnly()
    {
        var online = Guid.NewGuid();
        var cashier = Guid.NewGuid();
        await _database.ExecAsync(
            """
            INSERT INTO orders.orders (order_id, source, source_external_id, status, confirmation_status, order_number, notes, created_at, updated_at)
            VALUES (@online, 'Online', @external, 'Accepted', 'Accepted', @online_number, 'Yemeksepeti YS-12: Zil çalışmıyor, 0555 111 22 33', now(), now()),
                   (@cashier, 'Cashier', NULL, 'Accepted', 'NotRequired', @cashier_number, 'Yemeksepeti YS-12: kasiyer notu', now(), now());
            """,
            ("online", online), ("external", Guid.NewGuid().ToString("D")), ("online_number", "SCRUB-" + online.ToString("N")[..8]),
            ("cashier", cashier), ("cashier_number", "SCRUB-" + cashier.ToString("N")[..8]));

        await _database.RunFixtureAsync("151-online-order-customer-note-scrub.up.sql");
        await _database.RunFixtureAsync("151-online-order-customer-note-scrub.down.sql");

        Assert.Equal("Yemeksepeti YS-12", await _database.OrderNotesAsync(online));
        Assert.Equal("Yemeksepeti YS-12: kasiyer notu", await _database.OrderNotesAsync(cashier));
    }

    [Fact]
    public async Task TheProcessingMigrationRollsBackAndReapplies()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        await _database.RunFixtureAsync("146-yemeksepeti-inbox-processing.down.sql");
        await _database.RunFixtureAsync("146-yemeksepeti-inbox-processing.up.sql");
        var orderId = NewOrderId();
        await StoreAsync(Delivery(orderId, "RECEIVED", "t1", (sku, 1)));

        Assert.Equal(1, await DrainAsync());
        Assert.Equal("OrderCreated", Assert.Single(await _database.InboxAsync(orderId)).Outcome);
    }
}

[CollectionDefinition("Online ordering PostgreSQL", DisableParallelization = true)]
public sealed class OnlineOrderingPostgresqlDefinition;
