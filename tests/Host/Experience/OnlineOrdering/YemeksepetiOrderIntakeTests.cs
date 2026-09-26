using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Experience.OnlineOrdering;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;
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
        Assert.Equal($"Yemeksepeti YS-{orderId[..6]}: Zil çalışmıyor", order.Notes);
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
        for (var pass = 0; pass < 10; pass++)
        {
            try
            {
                if (!await Intake.ProcessNextAsync())
                    break;
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
