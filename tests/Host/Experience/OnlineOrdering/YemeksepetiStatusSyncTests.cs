using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Host.Experience.OnlineOrdering;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Outbox;
using ALKAROS.IntegrationContracts;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Messaging;
using ALKAROS.OnlineOrdering.CatalogPublishing;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-ONL-003 over real Postgres: provider cancellations, restaurant handover and cancellation,
/// and the outbound status updates they queue. Provider-facing parts are exercised only through
/// the outbox here; the HTTP client itself is an unverified draft (V0-YSP-001 Blocked).
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class YemeksepetiStatusSyncTests : IAsyncLifetime
{
    private const string Secret = "Bearer static-portal-token";

    private static readonly string?[] CreatedCancelledRepeated = ["OrderCreated", "OrderCancelled", "AlreadyCancelled"];
    private static readonly string?[] CancelledThenSkipped = ["CancelledBeforeOrder", "SkippedCancelledOrder"];

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

    private YemeksepetiStatusSyncService Sync => _app!.Services.GetRequiredService<YemeksepetiStatusSyncService>();

    private ICrossChannelPortionArbiter Arbiter => _app!.Services.GetRequiredService<ICrossChannelPortionArbiter>();

    private static byte[] Event(string orderId, string status, string updatedAt, string sku, string transport = "LOGISTICS_DELIVERY",
        string? cancelledBy = null, bool afterPickup = false)
    {
        var cancellation = cancelledBy is null
            ? string.Empty
            : ",\"cancellation\":{\"cancelled_by\":\"" + cancelledBy + "\",\"reason\":\"CUSTOMER_REQUEST\",\"post_picked_up\":"
              + (afterPickup ? "true" : "false") + "}";
        var json = "{\"order_id\":\"" + orderId + "\",\"status\":\"" + status + "\",\"transport_type\":\"" + transport + "\","
                   + "\"items\":[{\"_id\":\"i1\",\"sku\":\"" + sku + "\",\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":1,\"unit_price\":150}}]"
                   + cancellation + ",\"sys\":{\"updated_at\":\"" + updatedAt + "\"}}";
        return Encoding.UTF8.GetBytes(json);
    }

    private async Task StoreAsync(byte[] body) =>
        Assert.Equal(WebhookReceiptOutcome.Stored, (await Inbox.ReceiveAsync(Secret, body)).Outcome);

    private async Task DrainAsync()
    {
        while (await Intake.ProcessNextAsync())
        {
        }
    }

    private async Task<(Guid ProductId, string Sku, string ExternalId, Guid OrderId)> AcceptedOrderAsync(
        decimal onHand = 2m, string transport = "LOGISTICS_DELIVERY")
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand);
        var externalId = Guid.NewGuid().ToString("D");
        await StoreAsync(Event(externalId, "RECEIVED", "t1", sku, transport));
        await DrainAsync();
        var order = Assert.Single(await _database.OnlineOrdersAsync(externalId));
        Assert.Equal("Accepted", order.Status);
        return (productId, sku, externalId, order.OrderId);
    }

    private async Task<IReadOnlyList<YemeksepetiStatusUpdateRequested>> OutboundAsync(string externalId) =>
        (await _database.OutboundStatusUpdatesAsync(externalId))
            .Select(json => IntegrationEventSerializer.Deserialize<YemeksepetiStatusUpdateRequested>(Encoding.UTF8.GetBytes(json)))
            .ToList();

    [Fact]
    public async Task AProviderCancellationBeforeTheKitchenStartsReleasesTheHoldExactlyOnce()
    {
        var (productId, sku, externalId, orderId) = await AcceptedOrderAsync(onHand: 2m);
        Assert.Equal(1m, await _database.AvailableAsync(productId));

        await StoreAsync(Event(externalId, "CANCELLED", "t2", sku, cancelledBy: "CUSTOMER"));
        await StoreAsync(Event(externalId, "CANCELLED", "t3", sku, cancelledBy: "CUSTOMER"));
        await DrainAsync();

        Assert.Equal("Cancelled", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
        Assert.Equal(new[] { ("Released", "Online") }, await _database.HoldsAsync(orderId));
        Assert.Equal((2m, 2m), (await _database.OnHandAsync(productId), await _database.AvailableAsync(productId)));
        Assert.Equal(
            CreatedCancelledRepeated,
            (await _database.InboxAsync(externalId)).Select(e => e.Outcome).ToArray());
        // The provider initiated it; nothing is sent back.
        Assert.Empty(await OutboundAsync(externalId));
    }

    [Fact]
    public async Task AProviderCancellationAfterPreparationStartedIsWastedExactlyOnce()
    {
        var (productId, sku, externalId, orderId) = await AcceptedOrderAsync(onHand: 2m);
        await _database.MarkKitchenPreparingAsync(orderId);

        await StoreAsync(Event(externalId, "CANCELLED", "t2", sku, cancelledBy: "CUSTOMER"));
        await DrainAsync();

        Assert.Equal(new[] { ("Waste", "Online") }, await _database.HoldsAsync(orderId));
        // The prepared portion left the shelf once; nothing is held any more.
        Assert.Equal((1m, 1m), (await _database.OnHandAsync(productId), await _database.AvailableAsync(productId)));
    }

    [Fact]
    public async Task ARetryAfterACrashMidCancellationNeverRepeatsTheStockEffect()
    {
        var (productId, sku, externalId, orderId) = await AcceptedOrderAsync(onHand: 2m);
        // A previous attempt compensated the holds, then crashed before its transaction committed.
        await Arbiter.CompensateAsync(orderId, YemeksepetiOrderIntakeService.SystemActorId, "önceki deneme");

        await StoreAsync(Event(externalId, "CANCELLED", "t2", sku, cancelledBy: "CUSTOMER"));
        await DrainAsync();

        Assert.Equal("Cancelled", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
        Assert.Equal(new[] { ("Released", "Online") }, await _database.HoldsAsync(orderId));
        Assert.Equal((2m, 2m), (await _database.OnHandAsync(productId), await _database.AvailableAsync(productId)));
    }

    [Fact]
    public async Task ACancellationThatOvertakesItsOrderPreventsTheOrderFromEverExisting()
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        var externalId = Guid.NewGuid().ToString("D");
        await StoreAsync(Event(externalId, "CANCELLED", "t2", sku, cancelledBy: "CUSTOMER"));
        await StoreAsync(Event(externalId, "RECEIVED", "t1", sku));

        await DrainAsync();

        Assert.Empty(await _database.OnlineOrdersAsync(externalId));
        Assert.Equal(
            CancelledThenSkipped,
            (await _database.InboxAsync(externalId)).Select(e => e.Outcome).ToArray());
        Assert.Equal(1m, await _database.AvailableAsync(productId));
    }

    [Fact]
    public async Task AHandoverConsumesTheHoldCompletesTheOrderAndTellsTheProviderOnce()
    {
        var (productId, _, externalId, orderId) = await AcceptedOrderAsync(onHand: 2m);
        var staff = Guid.NewGuid();

        Assert.Equal(OnlineOrderActionOutcome.Applied, await Sync.HandOverAsync(orderId, staff));
        Assert.Equal(OnlineOrderActionOutcome.AlreadyApplied, await Sync.HandOverAsync(orderId, staff));

        Assert.Equal("Completed", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
        Assert.Equal(new[] { ("Consumed", "Online") }, await _database.HoldsAsync(orderId));
        Assert.Equal((1m, 1m), (await _database.OnHandAsync(productId), await _database.AvailableAsync(productId)));
        var update = Assert.Single(await OutboundAsync(externalId));
        Assert.Equal(YemeksepetiOutboundStatus.ReadyForPickup, update.Status);
        Assert.Null(update.Reason);
        Assert.Single(update.Items);
    }

    [Fact]
    public async Task TheHostOutboxGivesProviderDeliveriesTheirOwnRetryBudget()
    {
        var (_, _, _, orderId) = await AcceptedOrderAsync();
        await Sync.HandOverAsync(orderId, Guid.NewGuid());
        var services = new ServiceCollection();
        services.AddSingleton(_database.DataSource);
        HostComposition.ApplyComposedModuleServices(services, ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog).Services);
        services.AddOutboxDispatch();
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<OutboxStore>();
        await store.EnqueueAsync(new OutboxEnvelope(CatalogPublicationService.RequestedEventType, "CatalogPublication", Guid.NewGuid(), [1]));
        await store.EnqueueAsync(new OutboxEnvelope("rmd008.other-event.v1", "Other", Guid.NewGuid(), [1]));

        while (await store.DispatchAsync(new FailingSink(), batchSize: 50) > 0)
        {
        }

        // A provider delivery waits 30 s after its first failure; any other event keeps the default 5 s.
        foreach (var eventType in new[] { YemeksepetiStatusSync.StatusUpdateRequestedEventType, CatalogPublicationService.RequestedEventType })
            Assert.InRange(await SecondsUntilRetryAsync(eventType), 27, 31);
        Assert.InRange(await SecondsUntilRetryAsync("rmd008.other-event.v1"), 2, 6);
    }

    private async Task<double> SecondsUntilRetryAsync(string eventType)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT max(EXTRACT(EPOCH FROM next_retry_at - now()))::float8 FROM outbox_messages WHERE event_type = @type AND attempt_count = 1;");
        command.Parameters.AddWithValue("type", eventType);
        return (double)(await command.ExecuteScalarAsync())!;
    }

    private sealed class FailingSink : IOutboxDeliverySink
    {
        public Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    [Fact]
    public async Task ACancellationWaitsForAKitchenWriteInFlightAndDecidesOnWhatItWrote()
    {
        var (_, _, _, orderId) = await AcceptedOrderAsync(onHand: 2m);
        await using var kitchen = await _database.DataSource.OpenConnectionAsync();
        await using var kitchenWrite = await kitchen.BeginTransactionAsync();
        await using (var start = new NpgsqlCommand(
            """
            UPDATE kitchen.kitchen_tickets SET row_version = row_version + 1 WHERE order_id = @id;
            UPDATE kitchen.kitchen_ticket_items SET status = 'Preparing'
            WHERE ticket_id IN (SELECT id FROM kitchen.kitchen_tickets WHERE order_id = @id);
            """, kitchen, kitchenWrite))
        {
            start.Parameters.AddWithValue("id", orderId);
            await start.ExecuteNonQueryAsync();
        }

        var cancel = Task.Run(() => Sync.CancelByRestaurantAsync(orderId, YemeksepetiCancellationReason.TooBusy, Guid.NewGuid()));
        await Task.Delay(TimeSpan.FromMilliseconds(700));
        Assert.False(cancel.IsCompleted);
        await kitchenWrite.CommitAsync();

        Assert.Equal(OnlineOrderActionOutcome.Applied, await cancel);
        // The kitchen had started, so the portion is wasted, not put back on sale.
        Assert.Equal(new[] { ("Waste", "Online") }, await _database.HoldsAsync(orderId));
        Assert.All(await KitchenItemStatusesAsync(orderId), status => Assert.Equal("Cancelled", status));
    }

    [Fact]
    public async Task ACancellationThatFailsLeavesTheKitchenAndTheStockUntouched()
    {
        var (_, _, externalId, orderId) = await AcceptedOrderAsync(onHand: 2m);
        var guard = "rmd008_refuse_" + Guid.NewGuid().ToString("N")[..12];
        await _database.ExecAsync(
            $$"""
            CREATE FUNCTION public.{{guard}}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'refused by test'; END $$;
            CREATE TRIGGER {{guard}} BEFORE UPDATE ON orders.orders FOR EACH ROW
                WHEN (NEW.order_id = '{{orderId}}' AND NEW.status = 'Cancelled') EXECUTE FUNCTION public.{{guard}}();
            """);
        try
        {
            var cancel = () => Sync.CancelByRestaurantAsync(orderId, YemeksepetiCancellationReason.TooBusy, Guid.NewGuid());
            await Assert.ThrowsAsync<PostgresException>(cancel);
        }
        finally
        {
            await _database.ExecAsync($"DROP TRIGGER {guard} ON orders.orders; DROP FUNCTION public.{guard}();");
        }

        Assert.Equal("Accepted", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
        Assert.Equal(new[] { ("Reserved", "Online") }, await _database.HoldsAsync(orderId));
        Assert.All(await KitchenItemStatusesAsync(orderId), status => Assert.NotEqual("Cancelled", status));
        Assert.Empty(await OutboundAsync(externalId));
    }

    private async Task<IReadOnlyList<string>> KitchenItemStatusesAsync(Guid orderId)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            SELECT i.status FROM kitchen.kitchen_ticket_items i
            JOIN kitchen.kitchen_tickets t ON t.id = i.ticket_id
            WHERE t.order_id = @id;
            """);
        command.Parameters.AddWithValue("id", orderId);
        var statuses = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            statuses.Add(reader.GetString(0));
        Assert.NotEmpty(statuses);
        return statuses;
    }

    [Fact]
    public async Task ARestaurantCourierHandoverIsReportedAsDispatched()
    {
        var (_, _, externalId, orderId) = await AcceptedOrderAsync(transport: "VENDOR_DELIVERY");

        await Sync.HandOverAsync(orderId, Guid.NewGuid());

        Assert.Equal(YemeksepetiOutboundStatus.Dispatched, Assert.Single(await OutboundAsync(externalId)).Status);
    }

    [Fact]
    public async Task AProviderCancellationAfterHandoverIsTypedDivergenceWithStableEvidence()
    {
        var (productId, sku, externalId, orderId) = await AcceptedOrderAsync(onHand: 2m);
        await Sync.HandOverAsync(orderId, Guid.NewGuid());

        await StoreAsync(Event(externalId, "CANCELLED", "t2", sku, cancelledBy: "LOGISTICS", afterPickup: true));
        await StoreAsync(Event(externalId, "CANCELLED", "t3", sku, cancelledBy: "LOGISTICS", afterPickup: true));
        await DrainAsync();

        Assert.Equal("Completed", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
        var diverged = (await _database.InboxAsync(externalId)).Where(e => e.Outcome == "Diverged").ToList();
        Assert.Equal(2, diverged.Count);
        Assert.All(diverged, e => Assert.Contains("CancelledAfterHandover", e.Detail));
        Assert.Equal(EvidenceIdOf(diverged[0].Detail!), EvidenceIdOf(diverged[1].Detail!));
        Assert.Equal(1m, await _database.OnHandAsync(productId));
    }

    [Fact]
    public async Task ARestaurantCancellationReleasesTheHoldAndTellsTheProviderWhy()
    {
        var (productId, _, externalId, orderId) = await AcceptedOrderAsync(onHand: 1m);
        var staff = Guid.NewGuid();

        Assert.Equal(
            OnlineOrderActionOutcome.Applied,
            await Sync.CancelByRestaurantAsync(orderId, YemeksepetiCancellationReason.TooBusy, staff));
        Assert.Equal(
            OnlineOrderActionOutcome.AlreadyApplied,
            await Sync.CancelByRestaurantAsync(orderId, YemeksepetiCancellationReason.TooBusy, staff));

        Assert.Equal(new[] { ("Released", "Online") }, await _database.HoldsAsync(orderId));
        Assert.Equal(1m, await _database.AvailableAsync(productId));
        var update = Assert.Single(await OutboundAsync(externalId));
        Assert.Equal((YemeksepetiOutboundStatus.Cancelled, (YemeksepetiCancellationReason?)YemeksepetiCancellationReason.TooBusy),
            (update.Status, update.Reason));
    }

    [Fact]
    public async Task AHandedOverOrderCannotBeCancelledByTheRestaurant()
    {
        var (_, _, externalId, orderId) = await AcceptedOrderAsync();
        await Sync.HandOverAsync(orderId, Guid.NewGuid());

        Assert.Equal(
            OnlineOrderActionOutcome.NotAllowed,
            await Sync.CancelByRestaurantAsync(orderId, YemeksepetiCancellationReason.Closed, Guid.NewGuid()));
        Assert.Single(await OutboundAsync(externalId));
    }

    [Fact]
    public async Task AnOrderThatCannotBeHeldIsCancelledAtTheProviderAsItemUnavailable()
    {
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        Assert.Equal(CrossChannelReservationOutcome.Reserved, await _database.HoldElsewhereAsync(productId));
        var diverged = Guid.NewGuid().ToString("D");
        var unmapped = Guid.NewGuid().ToString("D");
        await StoreAsync(Event(diverged, "RECEIVED", "t1", sku));
        await StoreAsync(Event(unmapped, "RECEIVED", "t1", "ys-never-mapped"));

        await DrainAsync();

        foreach (var externalId in new[] { diverged, unmapped })
        {
            var update = Assert.Single(await OutboundAsync(externalId));
            Assert.Equal((YemeksepetiOutboundStatus.Cancelled, (YemeksepetiCancellationReason?)YemeksepetiCancellationReason.ItemUnavailable),
                (update.Status, update.Reason));
        }
    }

    [Fact]
    public async Task AStructurallyBrokenOrderIsLeftForReviewNotCancelledAtTheProvider()
    {
        var externalId = Guid.NewGuid().ToString("D");
        var body = Encoding.UTF8.GetBytes(
            "{\"order_id\":\"" + externalId + "\",\"status\":\"RECEIVED\",\"transport_type\":\"LOGISTICS_DELIVERY\",\"items\":[]}");
        await StoreAsync(body);

        await DrainAsync();

        Assert.Equal("Rejected", Assert.Single(await _database.InboxAsync(externalId)).Outcome);
        Assert.Empty(await OutboundAsync(externalId));
    }

    [Fact]
    public async Task AProviderCancellationRacingAHandoverClosesOneWayOnly()
    {
        for (var round = 0; round < 5; round++)
        {
            var (productId, sku, externalId, orderId) = await AcceptedOrderAsync(onHand: 1m);
            await StoreAsync(Event(externalId, "CANCELLED", "t2", sku, cancelledBy: "CUSTOMER"));
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var handover = Task.Run(async () => { await start.Task; return await Sync.HandOverAsync(orderId, Guid.NewGuid()); });
            var cancel = Task.Run(async () => { await start.Task; await DrainAsync(); });
            start.SetResult();
            var handoverOutcome = await handover;
            await cancel;

            var status = Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status;
            var holds = await _database.HoldsAsync(orderId);
            if (handoverOutcome == OnlineOrderActionOutcome.Applied)
            {
                Assert.Equal("Completed", status);
                Assert.Equal(new[] { ("Consumed", "Online") }, holds);
                Assert.Equal(0m, await _database.OnHandAsync(productId));
            }
            else
            {
                Assert.Equal(OnlineOrderActionOutcome.NotAllowed, handoverOutcome);
                Assert.Equal("Cancelled", status);
                Assert.Equal(new[] { ("Released", "Online") }, holds);
                Assert.Equal(1m, await _database.OnHandAsync(productId));
            }
        }
    }

    private static string EvidenceIdOf(string detail) =>
        System.Text.Json.JsonDocument.Parse(detail).RootElement.GetProperty("evidenceId").GetString()!;
}
