using System.Net;
using System.Net.Http.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.VoidSent.Tests;

/// <summary>
/// V1-IAM-027: codes the V0-DOM-006 amendment (Semih, 2026-09-04) — a
/// sent-but-unserved item may be voided under the bills.void grant, and the
/// void also cancels the matching kitchen ticket item and converts a billed
/// line to BillLineType.Waste.
/// </summary>
[Collection("Order void-sent PostgreSQL HTTP")]
public sealed class OrderManagementVoidSentHttpTests : IAsyncLifetime
{
    private readonly OrderManagementVoidSentTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            VoidSentPath(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnInvalidReasonCodeIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Preparing);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "NotARealReason")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnItemStillNotSentIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.NotSent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AnItemAlreadyServedIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Served);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ARoleThatHoldsBillsVoidOutrightAppliesDirectlyAndCancelsKitchenAndBill()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, productId, item) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Preparing);
        await _database.SeedKitchenTicketAsync(orderId, itemId, productId, KitchenTicketItemState.Preparing);
        await _database.SeedOpenBillAsync(orderId, item);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange", "sent by mistake")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoidSentItemResultV1>();
        Assert.Equal("Applied", body!.Status);
        Assert.Equal(2, body.NewOrderRowVersion);
        Assert.True(body.KitchenTicketItemCancelled);
        Assert.True(body.BillLineConvertedToWaste);
    }

    [Fact]
    public async Task CanVoidAndCanVoidSentMatchEachHandlersOwnEligibilityCheck()
    {
        // V1-RMD-168: found by the 2026-09-10 Garson audit (foundations.md
        // §0.2) — the waiter client used to derive these two flags itself
        // from kitchenState alone. Pins that the three kitchen states this
        // very test file already exercises against the real endpoints
        // (AnItemStillNotSentIsRejected, the Preparing success case above,
        // AnItemAlreadyServedIsRejected) produce exactly the canVoid/
        // canVoidSent the DTO now computes server-side.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(
            terminalId, "supervisor", "bills.void", "orders.create");

        var (notSentOrderId, _, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.NotSent);
        var (preparingOrderId, _, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Preparing);
        var (servedOrderId, _, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Served);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var notSentResponse = await client.SendAsync(GetRequest(OrderPath(terminalId, notSentOrderId), cookie));
        var notSentOrder = await notSentResponse.Content.ReadFromJsonAsync<OrderDto>();
        var notSentItem = Assert.Single(notSentOrder!.Items);
        Assert.True(notSentItem.CanVoid);
        Assert.False(notSentItem.CanVoidSent);
        // V1-RMD-177: found by the 2026-09-10 Garson audit — /comp had no
        // client at all. Unlike CanVoid/CanVoidSent, CanComp does not care
        // about KitchenState at all (a served dish can still be comp'd) -
        // true across all three states pinned here is the point of the
        // assertion, not incidental.
        Assert.True(notSentItem.CanComp);

        using var preparingResponse = await client.SendAsync(GetRequest(OrderPath(terminalId, preparingOrderId), cookie));
        var preparingOrder = await preparingResponse.Content.ReadFromJsonAsync<OrderDto>();
        var preparingItem = Assert.Single(preparingOrder!.Items);
        Assert.False(preparingItem.CanVoid);
        Assert.True(preparingItem.CanVoidSent);
        Assert.True(preparingItem.CanComp);

        using var servedResponse = await client.SendAsync(GetRequest(OrderPath(terminalId, servedOrderId), cookie));
        var servedOrder = await servedResponse.Content.ReadFromJsonAsync<OrderDto>();
        var servedItem = Assert.Single(servedOrder!.Items);
        Assert.False(servedItem.CanVoid);
        Assert.False(servedItem.CanVoidSent);
        Assert.True(servedItem.CanComp);
    }

    /// <summary>
    /// Bağımsız denetimde bulundu (2026-09-05): önceki sıralamada Order ve
    /// Kitchen zaten kalıcı olarak yazılıyordu, Bill kapalıysa (Paid/
    /// Allocated) SONRA 409 dönülüyordu — ama Order/Kitchen mutasyonu geri
    /// alınmıyordu. Bu test tam o senaryoyu kurar (Bill Allocated) ve iki
    /// şeyi doğrular: istek 409 BILL_NOT_MODIFIABLE döner VE sipariş kalemi
    /// tamamen dokunulmamış kalır (hâlâ Active/Preparing) — yani düzeltme
    /// sonrası hiçbir şey kısmen uygulanmıyor, ya hepsi ya hiçbiri.
    /// </summary>
    [Fact]
    public async Task WhenTheBillIsAlreadyClosedNothingIsMutatedAndTheRequestIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, productId, item) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Preparing);
        await _database.SeedKitchenTicketAsync(orderId, itemId, productId, KitchenTicketItemState.Preparing);
        await _database.SeedOpenBillAsync(orderId, item, BillState.Allocated);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("BILL_NOT_MODIFIABLE", body);

        var (status, kitchenState) = await _database.ReloadItemStateAsync(orderId, itemId);
        Assert.Equal(OrderItemState.Active, status);
        Assert.Equal(KitchenState.Preparing, kitchenState);
    }

    [Fact]
    public async Task WithNoMatchingKitchenTicketOrBillAppliesWithBothFlagsFalse()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Sent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoidSentItemResultV1>();
        Assert.Equal("Applied", body!.Status);
        Assert.False(body.KitchenTicketItemCancelled);
        Assert.False(body.BillLineConvertedToWaste);
        // Nothing was ever seeded as consumed for this item — nothing to
        // restore, and that is not an error (V1-RMD-143 follow-up).
        Assert.False(body.StockRestored);
    }

    /// <summary>
    /// V1-RMD-143 follow-up (2026-09-09 deep review): docs/domain/
    /// void-complimentary-discount-policy.md's own Waste definition
    /// ("hazırlanmış ancak satılamayan ürünün stoktan çıkması") assumes the
    /// kitchen had actually started — Semih's revision: a merely-Sent item
    /// (ticket dispatched, nothing physically used yet) gets its stock back
    /// on void instead of staying a permanent, unearned Waste deduction.
    /// </summary>
    [Fact]
    public async Task VoidingASentItemBeforeTheKitchenStartedRestoresItsStock()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Sent);
        var (stockItemId, _) = await _database.SeedConsumedStockForItemAsync(itemId, onHandAfterConsumption: 9m, consumedQuantity: 1m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoidSentItemResultV1>();
        Assert.Equal("Applied", body!.Status);
        Assert.True(body.StockRestored);
        Assert.Equal(10m, await _database.GetOnHandQuantityAsync(stockItemId));
    }

    /// <summary>
    /// V1-RMD-152: a line's extras consume their own stock, and their
    /// movements are written against the SAME order item id as the product's.
    /// That is what lets this restore path give them back with the rest of
    /// the line instead of needing its own code — this pins that a line
    /// carrying more than one consumption gets all of it back.
    /// </summary>
    [Fact]
    public async Task VoidingASentItemRestoresItsModifierStockToo()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Sent);
        var (productStockId, _) = await _database.SeedConsumedStockForItemAsync(
            itemId, onHandAfterConsumption: 9m, consumedQuantity: 1m);
        var (modifierStockId, _) = await _database.SeedConsumedStockForItemAsync(
            itemId, onHandAfterConsumption: 8m, consumedQuantity: 2m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoidSentItemResultV1>();
        Assert.True(body!.StockRestored);
        Assert.Equal(10m, await _database.GetOnHandQuantityAsync(productStockId));
        Assert.Equal(10m, await _database.GetOnHandQuantityAsync(modifierStockId));
    }

    /// <summary>
    /// The mirror case: once the kitchen has actually started (Preparing or
    /// Ready), the ingredients are genuinely gone — the void still applies
    /// (Order/Kitchen/Bill unchanged from the existing behaviour), but stock
    /// stays consumed, matching the documented Waste policy.
    /// </summary>
    [Fact]
    public async Task VoidingAPreparingItemDoesNotRestoreItsStock()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.void");
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Preparing);
        var (stockItemId, _) = await _database.SeedConsumedStockForItemAsync(itemId, onHandAfterConsumption: 9m, consumedQuantity: 1m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoidSentItemResultV1>();
        Assert.Equal("Applied", body!.Status);
        Assert.False(body.StockRestored);
        Assert.Equal(9m, await _database.GetOnHandQuantityAsync(stockItemId));
    }

    [Fact]
    public async Task ARoleWithoutBillsVoidEscalatesToPending()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter");
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Preparing);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoidSentItemResultV1>();
        Assert.Equal("Pending", body!.Status);
        Assert.NotNull(body.GrantId);
    }

    [Fact]
    public async Task AWaiterVoidingAnotherServersSentItemIsRefusedByTheOwnCheckGuard()
    {
        // V1-RMD-111: same wiring proof as the /comp sibling test — before
        // this task the endpoint fetched no order and always passed
        // SubjectServingUserId: null, so this 403 never actually happened.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedRealWaiterSessionAsync(terminalId);
        var otherServerId = Guid.NewGuid();
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Preparing, otherServerId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AWaiterVoidingTheirOwnSentItemIsNotBlockedByTheOwnCheckGuard()
    {
        var terminalId = Guid.NewGuid();
        var (waiterUserId, cookie) = await _database.SeedRealWaiterSessionAsync(terminalId);
        var (orderId, itemId, _, _) = await _database.SeedActiveOrderWithOneItemAsync(KitchenState.Preparing, waiterUserId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            VoidSentPath(terminalId, orderId, itemId), cookie,
            new VoidSentItemRequestV1(Guid.NewGuid().ToString(), 1, "CustomerChange")));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoidSentItemResultV1>();
        Assert.Equal("Pending", body!.Status);
    }

    private static string VoidSentPath(Guid terminalId, Guid orderId, Guid itemId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items/{itemId:D}/void-sent";

    private static string OrderPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}";

    private static HttpRequestMessage JsonRequest(string path, string cookie, VoidSentItemRequestV1 body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage GetRequest(string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddOrderManagementExperience();
        var app = builder.Build();
        app.MapOrderManagementApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}

[CollectionDefinition("Order void-sent PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OrderVoidSentPostgresqlDefinition;
