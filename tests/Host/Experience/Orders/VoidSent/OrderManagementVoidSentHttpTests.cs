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

    private static HttpRequestMessage JsonRequest(string path, string cookie, VoidSentItemRequestV1 body)
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

[CollectionDefinition("Order void-sent PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OrderVoidSentPostgresqlDefinition;
