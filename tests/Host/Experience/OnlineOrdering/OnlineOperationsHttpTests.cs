using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Experience.OnlineOrdering;
using ALKAROS.Host.Experience.Orders;
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
/// V12-OUI-001: the operations queue and its online-order actions over real HTTP and Postgres. Every
/// action goes through the owning contract and carries the row version the operator saw.
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class OnlineOperationsHttpTests : IAsyncLifetime, IDisposable
{
    private const string Secret = "Bearer static-portal-token";

    private readonly OnlineOrderingTestDatabase _database = new();
    private WebApplication? _app;
    private HttpClient? _client;
    private readonly Guid _terminalId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        secrets.Set(YemeksepetiWebhookInbox.WebhookSecret, Secret);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(secrets);
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddYemeksepetiWebhookExperience();
        builder.Services.AddOnlineCatalogPublishingExperience();
        OnlineAvailabilityPublishingHostedService.AddOnlineAvailabilityPublishingExperience(builder.Services);
        builder.Services.AddOnlineOperationsExperience();
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

    private string Queue(string? source = null) =>
        $"/api/v1/terminals/{_terminalId:D}/online-operations" + (source is null ? string.Empty : "?source=" + source);

    private string Action(Guid orderId, string action) =>
        $"/api/v1/terminals/{_terminalId:D}/online-operations/orders/{orderId:D}/{action}";

    private static HttpRequestMessage Get(string path, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage Post<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<(Guid OrderId, Guid ProductId, string ExternalId)> AcceptedOnlineOrderAsync(string sku, Guid productId, string? comment = null)
    {
        var externalId = Guid.NewGuid().ToString("D");
        var body = "{\"order_id\":\"" + externalId + "\",\"external_order_id\":\"YS-77\",\"status\":\"RECEIVED\",\"transport_type\":\"LOGISTICS_DELIVERY\","
                   + (comment is null ? string.Empty : "\"comment\":\"" + comment + "\",")
                   + "\"items\":[{\"_id\":\"i1\",\"sku\":\"" + sku + "\",\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":1,\"unit_price\":150}}],"
                   + "\"sys\":{\"updated_at\":\"t1\"}}";
        var inbox = _app!.Services.GetRequiredService<YemeksepetiWebhookInbox>();
        Assert.Equal(WebhookReceiptOutcome.Stored, (await inbox.ReceiveAsync(Secret, Encoding.UTF8.GetBytes(body))).Outcome);
        var intake = _app.Services.GetRequiredService<YemeksepetiOrderIntakeService>();
        while (await intake.ProcessNextAsync())
        {
        }

        return (Assert.Single(await _database.OnlineOrdersAsync(externalId)).OrderId, productId, externalId);
    }

    [Fact]
    public async Task TheCustomerNoteIsOpenedOnlyOnRequestAndEveryOpeningIsAudited()
    {
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 3m);
        var (withNote, _, _) = await AcceptedOnlineOrderAsync(sku, productId, "Zile basmayın, 0555 111 22 33");
        var (withoutNote, _, _) = await AcceptedOnlineOrderAsync(sku, productId);
        var (qrOrder, _) = await _database.SeedQrPendingOrderAsync();

        Assert.Equal("Yemeksepeti YS-77", await _database.OrderNotesAsync(withNote));
        using var note = await _client!.SendAsync(Get(Action(withNote, "customer-note"), cookie));
        using var none = await _client.SendAsync(Get(Action(withoutNote, "customer-note"), cookie));
        using var anonymous = await _client.SendAsync(Get(Action(withNote, "customer-note"), null));
        using var qr = await _client.SendAsync(Get(Action(qrOrder, "customer-note"), cookie));

        Assert.Equal(HttpStatusCode.OK, note.StatusCode);
        Assert.Equal("Zile basmayın, 0555 111 22 33", (await note.Content.ReadFromJsonAsync<OnlineOrderCustomerNoteV1>())!.Note);
        Assert.Null((await none.Content.ReadFromJsonAsync<OnlineOrderCustomerNoteV1>())!.Note);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, qr.StatusCode);
        Assert.Single(await _database.AuditActorsAsync(withNote, "Order.CustomerNoteViewed"));
        Assert.Single(await _database.AuditActorsAsync(withoutNote, "Order.CustomerNoteViewed"));
        Assert.Empty(await _database.AuditActorsAsync(qrOrder, "Order.CustomerNoteViewed"));
    }

    [Fact]
    public async Task TheQueueShowsWaitingQrOrdersActiveOnlineOrdersAndProblemsFilterable()
    {
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var (qrOrder, tableNumber) = await _database.SeedQrPendingOrderAsync();
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var (onlineOrder, _, _) = await AcceptedOnlineOrderAsync(sku, productId);
        var brokenId = Guid.NewGuid().ToString("D");
        var inbox = _app!.Services.GetRequiredService<YemeksepetiWebhookInbox>();
        await inbox.ReceiveAsync(Secret, Encoding.UTF8.GetBytes(
            "{\"order_id\":\"" + brokenId + "\",\"status\":\"RECEIVED\",\"transport_type\":\"LOGISTICS_DELIVERY\",\"items\":[{\"sku\":\"ys-nope\",\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":1,\"unit_price\":1}}]}"));
        // V12-RMD-006: a delivery kind the mapper does not know is a problem a person must see too.
        var unknownId = Guid.NewGuid().ToString("D");
        await inbox.ReceiveAsync(Secret, Encoding.UTF8.GetBytes(
            "{\"order_id\":\"" + unknownId + "\",\"status\":\"RECEIVED\",\"transport_type\":\"PICKUP\",\"items\":[{\"sku\":\"ys-nope\",\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":1,\"unit_price\":1}}]}"));
        while (await _app.Services.GetRequiredService<YemeksepetiOrderIntakeService>().ProcessNextAsync())
        {
        }

        using var all = await _client!.SendAsync(Get(Queue(), cookie));
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        var queue = (await all.Content.ReadFromJsonAsync<OnlineOperationsQueueV1>())!;
        var qr = Assert.Single(queue.Orders, o => o.OrderId == qrOrder);
        Assert.Equal(("Qr", "PendingConfirmation", (string?)tableNumber), (qr.Source, qr.Status, qr.TableNumber));
        var online = Assert.Single(queue.Orders, o => o.OrderId == onlineOrder);
        Assert.Equal(("Online", "Accepted", (string?)"YS-77"), (online.Source, online.Status, online.DisplayCode));
        var problem = Assert.Single(queue.Problems, p => p.ExternalOrderId == brokenId);
        Assert.Equal(("Rejected", (string?)"UnmappedSku"), (problem.Outcome, problem.Reason));
        Assert.Equal("UnknownStatus", Assert.Single(queue.Problems, p => p.ExternalOrderId == unknownId).Outcome);

        using var qrOnly = await _client.SendAsync(Get(Queue("qr"), cookie));
        var qrQueue = (await qrOnly.Content.ReadFromJsonAsync<OnlineOperationsQueueV1>())!;
        Assert.All(qrQueue.Orders, o => Assert.Equal("Qr", o.Source));
        Assert.Empty(qrQueue.Problems);

        using var bad = await _client.SendAsync(Get(Queue("pos"), cookie));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task HandingOverUsesTheStatusSyncAndShowsItsPersistedResult()
    {
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var (orderId, _, externalId) = await AcceptedOnlineOrderAsync(sku, productId);
        var version = await _database.RowVersionAsync(orderId);

        using var first = await _client!.SendAsync(Post(Action(orderId, "hand-over"), cookie, new OnlineOrderActionRequestV1(version)));
        using var again = await _client.SendAsync(Post(Action(orderId, "hand-over"), cookie, new OnlineOrderActionRequestV1(version)));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("Applied", (await first.Content.ReadFromJsonAsync<OnlineOrderActionResultV1>())!.Outcome);
        Assert.Equal("AlreadyApplied", (await again.Content.ReadFromJsonAsync<OnlineOrderActionResultV1>())!.Outcome);
        Assert.Equal("Completed", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
        Assert.Equal(new[] { ("Consumed", "Online") }, await _database.HoldsAsync(orderId));
    }

    [Fact]
    public async Task AHandoverThatCannotBeReportedSaysWhyInsteadOfBlamingAConcurrentChange()
    {
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var (orderId, _, externalId) = await AcceptedOnlineOrderAsync(sku, productId);
        await _database.ExecAsync(
            "UPDATE online_ordering.provider_inbox SET outcome_detail = outcome_detail - 'transportType' WHERE order_id = @id;",
            ("id", orderId));

        using var response = await _client!.SendAsync(
            Post(Action(orderId, "hand-over"), cookie, new OnlineOrderActionRequestV1(await _database.RowVersionAsync(orderId))));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("HANDOVER_NOT_SUPPORTED", body);
        Assert.Contains("teslimat türü tanınmadığı", body);
        Assert.Equal("Accepted", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
    }

    [Fact]
    public async Task AHandoverWhoseStockCannotBeTakenSaysSo()
    {
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var (orderId, _, externalId) = await AcceptedOnlineOrderAsync(sku, productId);
        await _database.ExecAsync("DELETE FROM inventory.product_stock_mappings WHERE product_id = @id;", ("id", productId));

        using var response = await _client!.SendAsync(
            Post(Action(orderId, "hand-over"), cookie, new OnlineOrderActionRequestV1(await _database.RowVersionAsync(orderId))));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("STOCK_NOT_AVAILABLE", await response.Content.ReadAsStringAsync());
        Assert.Equal("Accepted", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
    }

    [Fact]
    public async Task AnActionFromAnOutdatedScreenChangesNothing()
    {
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var (orderId, _, externalId) = await AcceptedOnlineOrderAsync(sku, productId);
        var stale = await _database.RowVersionAsync(orderId) - 1;

        using var handOver = await _client!.SendAsync(Post(Action(orderId, "hand-over"), cookie, new OnlineOrderActionRequestV1(stale)));
        using var cancel = await _client.SendAsync(Post(Action(orderId, "cancel"), cookie, new CancelOnlineOrderRequestV1(stale, "TooBusy")));

        Assert.Equal(HttpStatusCode.Conflict, handOver.StatusCode);
        Assert.Contains("CONCURRENCY_CONFLICT", await handOver.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Contains("Listeyi yenileyin", await cancel.Content.ReadAsStringAsync());
        Assert.Equal("Accepted", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
        Assert.Equal(new[] { ("Reserved", "Online") }, await _database.HoldsAsync(orderId));
    }

    [Fact]
    public async Task CancellingNeedsADocumentedReasonAndThenReleasesTheHold()
    {
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 1m);
        var (orderId, _, externalId) = await AcceptedOnlineOrderAsync(sku, productId);
        var version = await _database.RowVersionAsync(orderId);

        using var invalid = await _client!.SendAsync(Post(Action(orderId, "cancel"), cookie, new CancelOnlineOrderRequestV1(version, "Whatever")));
        using var valid = await _client.SendAsync(Post(Action(orderId, "cancel"), cookie, new CancelOnlineOrderRequestV1(version, "ItemUnavailable")));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.Equal("Cancelled", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
        Assert.Equal(new[] { ("Released", "Online") }, await _database.HoldsAsync(orderId));
        Assert.Equal(1m, await _database.AvailableAsync(productId));
    }

    [Fact]
    public async Task UnauthenticatedOrUnauthorizedCallersChangeNothing()
    {
        var clerk = await _database.SeedStaffSessionAsync(_terminalId, "reports.view");
        var (productId, sku) = await _database.SeedSellableProductAsync(onHand: 2m);
        var (orderId, _, externalId) = await AcceptedOnlineOrderAsync(sku, productId);
        var version = await _database.RowVersionAsync(orderId);

        using var anonymousQueue = await _client!.SendAsync(Get(Queue(), null));
        using var forbiddenQueue = await _client.SendAsync(Get(Queue(), clerk));
        using var forbiddenAction = await _client.SendAsync(Post(Action(orderId, "hand-over"), clerk, new OnlineOrderActionRequestV1(version)));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousQueue.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenQueue.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAction.StatusCode);
        Assert.Equal("Accepted", Assert.Single(await _database.OnlineOrdersAsync(externalId)).Status);
    }

    [Fact]
    public async Task OnlineActionsRefuseAQrOrder()
    {
        var cookie = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        var (qrOrder, _) = await _database.SeedQrPendingOrderAsync();

        using var response = await _client!.SendAsync(Post(Action(qrOrder, "hand-over"), cookie, new OnlineOrderActionRequestV1(1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("NOT_AN_ONLINE_ORDER", await response.Content.ReadAsStringAsync());
    }
}
