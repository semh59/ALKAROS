using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.OnlineOrdering.OrderLinks;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.Provider;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-ONL-010 over real Postgres: the shared inbox and intake serve every registered platform. A second platform
/// (a test adapter that reads Yemeksepeti-shaped payloads under its own identity) gets its own orders, its own
/// cancellations and its own deduplication; an unregistered platform's events wait untouched.
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class ProviderNeutralIntakeTests : IAsyncLifetime
{
    private const string Secret = "Bearer static-portal-token";
    private const string TestPlatform = "test-platform";

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
        builder.Services.AddTransient<IOnlineOrderProvider>(services =>
            new SecondPlatform(ActivatorUtilities.CreateInstance<YemeksepetiOnlineOrderProvider>(services)));
        _app = builder.Build();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private ProviderInbox Inbox => _app!.Services.GetRequiredService<ProviderInbox>();

    private YemeksepetiOrderIntakeService Intake => _app!.Services.GetRequiredService<YemeksepetiOrderIntakeService>();

    private static string Payload(string orderId, string status, string updatedAt, string sku, string? cancelledBy = null) =>
        "{\"order_id\":\"" + orderId + "\",\"external_order_id\":\"EX-" + orderId[..6] + "\",\"status\":\"" + status + "\","
        + "\"transport_type\":\"LOGISTICS_DELIVERY\","
        + "\"items\":[{\"_id\":\"i1\",\"sku\":\"" + sku + "\",\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":1,\"unit_price\":150.00}}]"
        + (cancelledBy is null ? "" : ",\"cancellation\":{\"cancelled_by\":\"" + cancelledBy + "\",\"reason\":\"CUSTOMER_REQUEST\",\"post_picked_up\":false}")
        + ",\"sys\":{\"updated_at\":\"" + updatedAt + "\"}}";

    private static string Key(string provider, string orderId, string status, string updatedAt) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{provider}|{orderId}|{status}|{updatedAt}"))).ToLowerInvariant();

    private Task<ProviderInboxReceipt> StoreAsync(string provider, string orderId, string status, string updatedAt, string payload) =>
        Inbox.StoreAsync(new ProviderInboxEvent(provider, Key(provider, orderId, status, updatedAt), orderId, status, updatedAt, Encoding.UTF8.GetBytes(payload)));

    private async Task<int> DrainAsync()
    {
        var processed = 0;
        while (await Intake.ProcessNextAsync())
            processed++;
        return processed;
    }

    private async Task<string?> LinkedProviderAsync(Guid orderId) =>
        await _database.ScalarTextAsync($"SELECT provider FROM online_ordering.online_orders WHERE order_id = '{orderId:D}';");

    [Fact]
    public async Task EachPlatformsEventBecomesItsOwnOrderEvenWithTheSameProviderOrderNumber()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var orderId = Guid.NewGuid().ToString("D");
        Assert.Equal(ProviderInboxStoreOutcome.Stored, (await StoreAsync(TestPlatform, orderId, "RECEIVED", "t1", Payload(orderId, "RECEIVED", "t1", sku))).Outcome);
        var webhook = _app!.Services.GetRequiredService<YemeksepetiWebhookInbox>();
        Assert.Equal(WebhookReceiptOutcome.Stored,
            (await webhook.ReceiveAsync(Secret, Encoding.UTF8.GetBytes(Payload(orderId, "RECEIVED", "t1", sku)))).Outcome);

        Assert.Equal(2, await DrainAsync());

        var orders = await _database.OnlineOrdersAsync(orderId);
        Assert.Equal(2, orders.Count);
        var second = Assert.Single(orders, o => o.OrderNumber.StartsWith("TP-", StringComparison.Ordinal));
        var yemeksepeti = Assert.Single(orders, o => o.OrderNumber.StartsWith("YS-", StringComparison.Ordinal));
        Assert.Equal(("Accepted", "Test Platform EX-" + orderId[..6]), (second.Status, second.Notes));
        Assert.Equal(TestPlatform, await LinkedProviderAsync(second.OrderId));
        Assert.Equal("yemeksepeti", await LinkedProviderAsync(yemeksepeti.OrderId));
        Assert.All(await _database.InboxAsync(orderId), e => Assert.Equal("OrderCreated", e.Outcome));
    }

    [Fact]
    public async Task APlatformsCancellationCancelsOnlyThatPlatformsOrder()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var orderId = Guid.NewGuid().ToString("D");
        await StoreAsync(TestPlatform, orderId, "RECEIVED", "t1", Payload(orderId, "RECEIVED", "t1", sku));
        await StoreAsync(OnlineOrderProviders.Yemeksepeti, orderId, "RECEIVED", "t1", Payload(orderId, "RECEIVED", "t1", sku));
        Assert.Equal(2, await DrainAsync());

        await StoreAsync(TestPlatform, orderId, "CANCELLED", "t2", Payload(orderId, "CANCELLED", "t2", sku, cancelledBy: "CUSTOMER"));
        Assert.Equal(1, await DrainAsync());

        var orders = await _database.OnlineOrdersAsync(orderId);
        Assert.Equal("Cancelled", Assert.Single(orders, o => o.OrderNumber.StartsWith("TP-", StringComparison.Ordinal)).Status);
        Assert.Equal("Accepted", Assert.Single(orders, o => o.OrderNumber.StartsWith("YS-", StringComparison.Ordinal)).Status);
    }

    [Fact]
    public async Task AnUnregisteredPlatformsEventWaitsUntouched()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var orderId = Guid.NewGuid().ToString("D");
        await StoreAsync("not-registered", orderId, "RECEIVED", "t1", Payload(orderId, "RECEIVED", "t1", sku));

        Assert.Equal(0, await DrainAsync());
        var waiting = Assert.Single(await _database.InboxAsync(orderId));
        Assert.Equal(((string?)null, 0), (waiting.Outcome, waiting.Attempts));
        Assert.Empty(await _database.OnlineOrdersAsync(orderId));
    }

    [Fact]
    public async Task TheSameEventKeyIsStoredOncePerPlatformAndAYemeksepetiEventPolledLaterIsADuplicate()
    {
        var orderId = Guid.NewGuid().ToString("D");
        var body = Payload(orderId, "RECEIVED", "t1", "SKU-1");
        var first = await StoreAsync(TestPlatform, orderId, "RECEIVED", "t1", body);
        var again = await StoreAsync(TestPlatform, orderId, "RECEIVED", "t1", body);
        Assert.Equal((ProviderInboxStoreOutcome.Duplicate, first.InboxId), (again.Outcome, again.InboxId));
        Assert.Equal(ProviderInboxStoreOutcome.Stored, (await StoreAsync("other-platform", orderId, "RECEIVED", "t1", body)).Outcome);

        // A webhook delivery and a later copy of the same Yemeksepeti event (for example by polling) share one key.
        var webhook = _app!.Services.GetRequiredService<YemeksepetiWebhookInbox>();
        var delivered = await webhook.ReceiveAsync(Secret, Encoding.UTF8.GetBytes(body));
        var polled = await Inbox.StoreAsync(new ProviderInboxEvent(
            OnlineOrderProviders.Yemeksepeti, YemeksepetiWebhookInbox.EventKey(orderId, "RECEIVED", "t1"), orderId, "RECEIVED", "t1",
            Encoding.UTF8.GetBytes(body)));
        Assert.Equal((ProviderInboxStoreOutcome.Duplicate, delivered.InboxId!.Value), (polled.Outcome, polled.InboxId));

        // The payload is only kept encrypted and opens through the shared inbox.
        await using var command = _database.DataSource.CreateCommand(
            "SELECT payload_envelope FROM online_ordering.provider_inbox WHERE inbox_id = $1;");
        command.Parameters.AddWithValue(first.InboxId);
        var envelope = (byte[])(await command.ExecuteScalarAsync())!;
        Assert.Equal(-1, envelope.AsSpan().IndexOf(Encoding.UTF8.GetBytes("EX-" + orderId[..6])));
        Assert.Equal(body, Inbox.OpenPayload(envelope));
    }

    [Theory]
    [InlineData("ABCDEF", "RECEIVED")]
    [InlineData(null, "RECEIVED")]
    [InlineData("ok", "")]
    public async Task AnInvalidEventIsRefusedBeforeAnythingIsStored(string? key, string status)
    {
        var orderId = Guid.NewGuid().ToString("D");
        var eventKey = key switch
        {
            null => Key(TestPlatform, orderId, status, "t1").ToUpperInvariant(),
            "ok" => Key(TestPlatform, orderId, status, "t1"),
            _ => key,
        };
        await Assert.ThrowsAsync<ArgumentException>(() => Inbox.StoreAsync(
            new ProviderInboxEvent(TestPlatform, eventKey, orderId, status, "t1", Encoding.UTF8.GetBytes("{}"))));
        await Assert.ThrowsAsync<ArgumentException>(() => Inbox.StoreAsync(
            new ProviderInboxEvent(TestPlatform, Key(TestPlatform, orderId, "RECEIVED", "t1"), new string('x', ProviderInbox.MaxIdentifierLength + 1),
                "RECEIVED", "t1", Encoding.UTF8.GetBytes("{}"))));
        Assert.Empty(await _database.InboxAsync(orderId));
    }

    /// <summary>A second platform: Yemeksepeti's payload reading under another identity, name and order prefix.</summary>
    private sealed class SecondPlatform(IOnlineOrderProvider inner) : IOnlineOrderProvider
    {
        public string Provider => TestPlatform;

        public string DisplayName => "Test Platform";

        public string OrderNumberPrefix => "TP-";

        public StatusMappingResult MapStatus(string externalOrderId, string providerStatus, string rawPayload) =>
            inner.MapStatus(externalOrderId, providerStatus, rawPayload);

        public Task<NormalizationResult> NormalizeAsync(string rawPayload, DateTimeOffset receivedAt, CancellationToken cancellationToken = default) =>
            inner.NormalizeAsync(rawPayload, receivedAt, cancellationToken);

        public IReadOnlyList<OnlineOrderLineReference> ReadItemReferences(string rawPayload) => inner.ReadItemReferences(rawPayload);

        public Task<OnlineOutboundStatus?> HandoverStatusAsync(
            Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult<OnlineOutboundStatus?>(OnlineOutboundStatus.ReadyForPickup);

        public Task RequestStatusAsync(
            OnlineOrderStatusRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
