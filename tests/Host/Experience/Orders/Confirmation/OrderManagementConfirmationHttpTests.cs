using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.Confirmation.Tests;

/// <summary>
/// V1-RMD-137: found by an independent audit (2026-09-09) — an
/// age-restricted NFC order (V12-NFC-002) is deliberately parked at
/// PendingConfirmation for a staff ID check, but no HTTP action anywhere
/// could ever move it out of that state. These are the first tests of
/// POST .../orders/{orderId}/accept and .../reject.
/// </summary>
[Collection("Order confirmation PostgreSQL HTTP")]
public sealed class OrderManagementConfirmationHttpTests : IAsyncLifetime
{
    private readonly OrderManagementConfirmationTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorizedForBothActions()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var acceptResponse = await client.PostAsJsonAsync(
            AcceptPath(Guid.NewGuid(), Guid.NewGuid()), new AcceptPendingOrderRequestV1(1, null));
        Assert.Equal(HttpStatusCode.Unauthorized, acceptResponse.StatusCode);

        using var rejectResponse = await client.PostAsJsonAsync(
            RejectPath(Guid.NewGuid(), Guid.NewGuid()), new RejectPendingOrderRequestV1(1, "Kimlik kontrolü başarısız."));
        Assert.Equal(HttpStatusCode.Unauthorized, rejectResponse.StatusCode);
    }

    [Fact]
    public async Task AcceptingAPendingOrderTransitionsToAcceptedAndOccupiesTheTable()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, tableId, _) = await _database.SeedPendingConfirmationOrderAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, "Kimlik kontrol edildi.")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PendingOrderConfirmationResultV1>();
        Assert.Equal("Accepted", body!.NewStatus);
        Assert.True(body.TableReleased);

        var order = await _database.ReloadOrderAsync(orderId);
        Assert.Equal(OrderState.Accepted, order.Status);

        var (status, currentOrderId) = await _database.GetTableStateAsync(tableId);
        Assert.Equal("Occupied", status);
        Assert.Equal(orderId, currentOrderId);
    }

    /// <summary>Semih's decision (2026-09-09): Accept should really decrement stock, for every channel this store serves.</summary>
    [Fact]
    public async Task AcceptingAPendingOrderConsumesTheMappedStock()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, productId) = await _database.SeedPendingConfirmationOrderAsync(stockOnHandQuantity: 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The seeded order has exactly one item at quantity 1.
        Assert.Equal(9m, await _database.GetOnHandQuantityForProductAsync(productId));
    }

    /// <summary>Semih's decision (2026-09-09): a sold product with no stock mapping at all refuses Accept outright, it is never silently skipped.</summary>
    [Fact]
    public async Task AcceptingAnOrderForAnUnmappedProductIsRefused()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, _) = await _database.SeedPendingConfirmationOrderAsync(seedStockMapping: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PRODUCT_STOCK_NOT_CONFIGURED", body);
        Assert.Contains("stok tanımlanmamış", body);

        var order = await _database.ReloadOrderAsync(orderId);
        Assert.Equal(OrderState.PendingConfirmation, order.Status);
    }

    /// <summary>Semih's decision (2026-09-09): insufficient stock at Accept time is a clear, real refusal, never a silent negative.</summary>
    [Fact]
    public async Task AcceptingAnOrderWithInsufficientStockIsRefused()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, productId) = await _database.SeedPendingConfirmationOrderAsync(stockOnHandQuantity: 0m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INSUFFICIENT_STOCK", body);

        var order = await _database.ReloadOrderAsync(orderId);
        Assert.Equal(OrderState.PendingConfirmation, order.Status);
        // Nothing applied — the guarded delta refused the whole thing.
        Assert.Equal(0m, await _database.GetOnHandQuantityForProductAsync(productId));
    }

    /// <summary>Semih's own "kalan stok bilgisi ver garsona" (2026-09-09): viewing a pending order shows how much stock is left for each item.</summary>
    [Fact]
    public async Task ViewingAnOrderShowsTheAvailableStockForEachItem()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, _) = await _database.SeedPendingConfirmationOrderAsync(stockOnHandQuantity: 7m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        var item = Assert.Single(order!.Items);
        Assert.Equal(7m, item.AvailableStockQuantity);
    }

    [Fact]
    public async Task RejectingAPendingOrderCancelsTheKitchenTicketAndFreesTheTable()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, tableId, productId) = await _database.SeedPendingConfirmationOrderAsync();
        var order = await _database.ReloadOrderAsync(orderId);
        var orderItemId = order.Items[0].Id;
        await _database.SeedKitchenTicketAsync(orderId, orderItemId, productId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            RejectPath(terminalId, orderId), cookie,
            new RejectPendingOrderRequestV1(1, "Kimlik kontrolü başarısız.")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PendingOrderConfirmationResultV1>();
        Assert.Equal("Rejected", body!.NewStatus);
        Assert.Equal(1, body.KitchenTicketItemsCancelled);
        Assert.True(body.TableReleased);

        var reloaded = await _database.ReloadOrderAsync(orderId);
        Assert.Equal(OrderState.Rejected, reloaded.Status);

        var ticketItemStatuses = await _database.GetKitchenTicketItemStatusesAsync(orderId);
        Assert.All(ticketItemStatuses, status => Assert.Equal("Cancelled", status));

        var (status, currentOrderId) = await _database.GetTableStateAsync(tableId);
        Assert.Equal("Available", status);
        Assert.Null(currentOrderId);
    }

    [Fact]
    public async Task AcceptingAnOrderThatIsNotAwaitingConfirmationIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, _) = await _database.SeedPendingConfirmationOrderAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var first = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(2, null)));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("ORDER_NOT_PENDING_CONFIRMATION", body);
    }

    [Fact]
    public async Task RejectingAnAlreadyBilledOrderIsRefused()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, _) = await _database.SeedPendingConfirmationOrderAsync();
        var order = await _database.ReloadOrderAsync(orderId);
        await _database.SeedBillAsync(orderId, order.Items[0]);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            RejectPath(terminalId, orderId), cookie, new RejectPendingOrderRequestV1(1, "Kimlik kontrolü başarısız.")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ORDER_ALREADY_BILLED", body);

        var reloaded = await _database.ReloadOrderAsync(orderId);
        Assert.Equal(OrderState.PendingConfirmation, reloaded.Status);
    }

    [Fact]
    public async Task RejectingWithAnEmptyReasonIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, _) = await _database.SeedPendingConfirmationOrderAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            RejectPath(terminalId, orderId), cookie, new RejectPendingOrderRequestV1(1, "   ")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptingWithAStaleRowVersionIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, _) = await _database.SeedPendingConfirmationOrderAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(999, null)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CONCURRENCY_CONFLICT", body);
    }

    private static string AcceptPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/accept";

    private static string RejectPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/reject";

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
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

[CollectionDefinition("Order confirmation PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OrderConfirmationPostgresqlDefinition;
