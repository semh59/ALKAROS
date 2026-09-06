using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.TableDraft.Tests;

/// <summary>
/// Regression coverage for two Critical findings from an independent audit
/// (2026-09-06): (1) a second table-draft call for a table that already has
/// a Draft order used to replace its items instead of appending to them,
/// silently destroying whatever was already there; (2) neither production
/// client ever called submit-draft, so an order never actually left Draft
/// or reached the kitchen.
/// </summary>
[Collection("Order table-draft PostgreSQL HTTP")]
public sealed class OrderManagementTableDraftHttpTests : IAsyncLifetime
{
    private readonly OrderManagementTableDraftTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            DraftPath(Guid.NewGuid()),
            new CreateTableDraftRequest(Guid.NewGuid(), "M-01", "Garson", []));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASecondDraftForTheSameTableAppendsToTheExistingOrderInsteadOfReplacingIt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedProductAsync("Çorba", 60m);
        var dessert = await _database.SeedProductAsync("Baklava", 90m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var firstResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-05", "Garson Ahmet",
                [new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstDraft = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var secondResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-05", "Garson Ahmet",
                [new OrderItemDraftDto(Guid.NewGuid(), dessert, "Baklava", 1, 90m)])));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondDraft = await secondResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(firstDraft!.OrderId, secondDraft!.OrderId);
        Assert.Equal(2, secondDraft.Items.Count);
        Assert.Contains(secondDraft.Items, item => item.ProductName == "Çorba");
        Assert.Contains(secondDraft.Items, item => item.ProductName == "Baklava");
        Assert.Equal(150m, secondDraft.TotalAmount);
    }

    [Fact]
    public async Task SubmitDraftMovesTheOrderToSubmittedStatus()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Köfte", 280m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-07", "Garson Ahmet",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 2, 280m)])));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Submitted", submitted!.Status);
        Assert.Equal(1, await _database.KitchenTicketCountAsync(draft.OrderId));
    }

    [Fact]
    public async Task RetryingASubmitWithTheSameOperationIdReplaysWithoutASecondKitchenTicket()
    {
        // V1-RMD-113: found by an independent audit (2026-09-06) — this
        // endpoint used to be a thin, separate submit path that never
        // created a kitchen ticket and never checked idempotency at all
        // (the OperationId field was accepted and silently discarded). It
        // now delegates to the same SubmitOrderHandler the terminal-wide
        // quick-sale route uses.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Köfte", 280m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-15", "Garson Ahmet",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 2, 280m)])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();
        var submitBody = new SubmitTableOrderRequest(draft!.OrderId, draft.RowVersion, Guid.NewGuid().ToString());

        using var first = await client.SendAsync(JsonRequest(SubmitPath(terminalId, draft.OrderId), cookie, submitBody));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var retry = await client.SendAsync(JsonRequest(SubmitPath(terminalId, draft.OrderId), cookie, submitBody));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        Assert.Equal(1, await _database.KitchenTicketCountAsync(draft.OrderId));
    }

    [Fact]
    public async Task RetryingAnIdenticalDraftRequestDoesNotDuplicateItems()
    {
        // A client-generated, stable per-item id makes a retried draft
        // submission idempotent: the offline queue (WaiterPwa) or a plain
        // network retry resends the exact same payload after an ambiguous
        // (dropped-connection) failure, and that must update the existing
        // line in place rather than double it (found while verifying the
        // table-draft merge fix, 2026-09-06 — appending unconditionally on
        // every call is only safe for a genuinely new round of items).
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Kola", 45m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var payload = new CreateTableDraftRequest(tableId, "M-09", "Garson Ahmet",
            [new OrderItemDraftDto(Guid.NewGuid(), product, "Kola", 2, 45m)]);

        using var firstResponse = await client.SendAsync(JsonRequest(DraftPath(terminalId), cookie, payload));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstDraft = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var retryResponse = await client.SendAsync(JsonRequest(DraftPath(terminalId), cookie, payload));
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var retryDraft = await retryResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(firstDraft!.OrderId, retryDraft!.OrderId);
        Assert.Single(retryDraft.Items);
        Assert.Equal(90m, retryDraft.TotalAmount);
    }

    [Fact]
    public async Task ANewDraftIsAttributedToTheCreatingWaiterAsServingUser()
    {
        var terminalId = Guid.NewGuid();
        var (waiterUserId, cookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.create", "orders.send");
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-11", "Garson Ahmet",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(waiterUserId, await _database.GetServingUserIdAsync(draft!.OrderId));
    }

    [Fact]
    public async Task ASecondRoundOfItemsFromADifferentTerminalDoesNotReassignTheServer()
    {
        var openingTerminalId = Guid.NewGuid();
        var (openingWaiterId, openingCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            openingTerminalId, "waiter", "orders.create", "orders.send");
        var secondTerminalId = Guid.NewGuid();
        var (_, secondCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            secondTerminalId, "waiter", "orders.create", "orders.send");
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedProductAsync("Çorba", 60m);
        var dessert = await _database.SeedProductAsync("Baklava", 90m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var firstResponse = await client.SendAsync(JsonRequest(
            DraftPath(openingTerminalId), openingCookie,
            new CreateTableDraftRequest(tableId, "M-12", "Garson Ahmet",
                [new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var secondResponse = await client.SendAsync(JsonRequest(
            DraftPath(secondTerminalId), secondCookie,
            new CreateTableDraftRequest(tableId, "M-12", "Garson Mehmet",
                [new OrderItemDraftDto(Guid.NewGuid(), dessert, "Baklava", 1, 90m)])));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondDraft = await secondResponse.Content.ReadFromJsonAsync<OrderDto>();

        // The table's check is still attributed to whoever opened it — a
        // second terminal appending a dessert round does not silently steal
        // ownership of the check (V1-RMD-111, garson-masa design).
        Assert.Equal(openingWaiterId, await _database.GetServingUserIdAsync(secondDraft!.OrderId));
    }

    [Fact]
    public async Task ASelfTransferMovesTheWaitersOwnOpenOrdersToTheTarget()
    {
        var terminalId = Guid.NewGuid();
        var (fromWaiterId, fromCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.create", "orders.send", "orders.transfer-server");
        var (toWaiterId, _) = await _database.SeedCashierSessionWithPermissionsAsync(
            Guid.NewGuid(), "waiter", "orders.create");
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), fromCookie,
            new CreateTableDraftRequest(tableId, "M-13", "Garson Ahmet",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var transferResponse = await client.SendAsync(JsonRequest(
            TransferPath(terminalId), fromCookie,
            new TransferServingUserRequestV1(fromWaiterId, toWaiterId)));

        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);
        var result = await transferResponse.Content.ReadFromJsonAsync<TransferServingUserResultV1>();
        Assert.Equal(1, result!.OrdersReassigned);
        Assert.Equal(toWaiterId, await _database.GetServingUserIdAsync(draft!.OrderId));
    }

    [Fact]
    public async Task AWaiterWithOnlySelfTransferCannotMoveAnotherServersOrders()
    {
        var terminalId = Guid.NewGuid();
        var (actingWaiterId, actingCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.transfer-server");
        var otherWaiterId = Guid.NewGuid();
        var targetWaiterId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            TransferPath(terminalId), actingCookie,
            new TransferServingUserRequestV1(otherWaiterId, targetWaiterId)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _ = actingWaiterId;
    }

    [Fact]
    public async Task ACashierWithAnyTransferCanMoveAnotherServersOrders()
    {
        var terminalId = Guid.NewGuid();
        var (fromWaiterId, fromCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.create", "orders.send");
        var (toWaiterId, _) = await _database.SeedCashierSessionWithPermissionsAsync(
            Guid.NewGuid(), "waiter", "orders.create");
        var cashierTerminalId = Guid.NewGuid();
        var (_, cashierCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            cashierTerminalId, "cashier", "orders.transfer-server-any");
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), fromCookie,
            new CreateTableDraftRequest(tableId, "M-14", "Garson Ahmet",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var transferResponse = await client.SendAsync(JsonRequest(
            TransferPath(cashierTerminalId), cashierCookie,
            new TransferServingUserRequestV1(fromWaiterId, toWaiterId)));

        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);
        Assert.Equal(toWaiterId, await _database.GetServingUserIdAsync(draft!.OrderId));
    }

    [Fact]
    public async Task TransferringToANonexistentUserIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, cookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.transfer-server");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            TransferPath(terminalId), cookie,
            new TransferServingUserRequestV1(waiterId, Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string DraftPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/orders/table-draft";

    private static string SubmitPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/submit-draft";

    private static string TransferPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/orders/transfer-server";

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

[CollectionDefinition("Order table-draft PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OrderTableDraftPostgresqlDefinition;
