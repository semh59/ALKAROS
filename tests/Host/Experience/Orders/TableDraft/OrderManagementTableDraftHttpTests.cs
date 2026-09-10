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
            new CreateTableDraftRequest(Guid.NewGuid(), "M-01", []));

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
            new CreateTableDraftRequest(tableId, "M-05",
                [new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstDraft = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var secondResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-05",
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
        // V1-RMD-144: submitting consumes stock, and an unmapped product
        // refuses the whole submission — every submit-draft test's product
        // must therefore be mapped to real stock.
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-07",
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
        // V1-RMD-144: submitting consumes stock, and an unmapped product
        // refuses the whole submission — every submit-draft test's product
        // must therefore be mapped to real stock.
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-15",
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
        var payload = new CreateTableDraftRequest(tableId, "M-09",
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
    public async Task RetryingATableDraftAfterTheOrderWasAlreadySubmittedReplaysTheExistingOrderInsteadOfDuplicatingIt()
    {
        // V1-RMD-123: found by an independent audit (2026-09-07) — the
        // offline queue (or a plain network retry) resends the identical
        // table-draft payload, carrying the same client-generated Id, when a
        // response is lost — even after the order it created has already
        // been fully submitted and dispatched to the kitchen. Before this
        // fix, the Draft-only lookup could not see it, so the retry silently
        // started and submitted a second, duplicate order for the table.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        // V1-RMD-144: submitting consumes stock, and an unmapped product
        // refuses the whole submission — every submit-draft test's product
        // must therefore be mapped to real stock.
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var payload = new CreateTableDraftRequest(tableId, "M-16",
            [new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 2, 280m)], Id: Guid.NewGuid());

        using var draftResponse = await client.SendAsync(JsonRequest(DraftPath(terminalId), cookie, payload));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        // The client never saw the submit response (dropped connection) and
        // retries the whole draft+submit sequence from scratch.
        using var retryDraftResponse = await client.SendAsync(JsonRequest(DraftPath(terminalId), cookie, payload));

        Assert.Equal(HttpStatusCode.OK, retryDraftResponse.StatusCode);
        var retryDraft = await retryDraftResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(draft.OrderId, retryDraft!.OrderId);
        Assert.Equal("Submitted", retryDraft.Status);
        Assert.Equal(1, await _database.KitchenTicketCountAsync(draft.OrderId));
    }

    [Fact]
    public async Task ConcurrentIdenticalFirstSubmissionsForATableResolveToTheSameOrder()
    {
        // The database's own partial unique index — not application-level
        // locking, since the submission-id lookup is a plain (non-locking)
        // read — is what prevents two near-simultaneous first attempts
        // carrying the same client-generated Id from creating two orders;
        // the loser's PostgresException is caught and recovers the winner's
        // order instead of surfacing a 500.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Ayran", 20m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var payload = new CreateTableDraftRequest(tableId, "M-17",
            [new OrderItemDraftDto(Guid.NewGuid(), product, "Ayran", 1, 20m)], Id: Guid.NewGuid());

        var first = client.SendAsync(JsonRequest(DraftPath(terminalId), cookie, payload));
        var second = client.SendAsync(JsonRequest(DraftPath(terminalId), cookie, payload));
        var responses = await Task.WhenAll(first, second);

        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            var firstOrder = await responses[0].Content.ReadFromJsonAsync<OrderDto>();
            var secondOrder = await responses[1].Content.ReadFromJsonAsync<OrderDto>();
            Assert.Equal(firstOrder!.OrderId, secondOrder!.OrderId);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
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
            new CreateTableDraftRequest(tableId, "M-11",
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
            new CreateTableDraftRequest(tableId, "M-12",
                [new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var secondResponse = await client.SendAsync(JsonRequest(
            DraftPath(secondTerminalId), secondCookie,
            new CreateTableDraftRequest(tableId, "M-12",
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
            new CreateTableDraftRequest(tableId, "M-13",
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
            new CreateTableDraftRequest(tableId, "M-14",
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

    [Fact]
    public async Task DraftingAnUnavailableProductIsRejected()
    {
        // V1-RMD-128: found by an independent audit (2026-09-09) — this
        // endpoint checked catalog.products.active but not is_available
        // (the real-time 86/suspend toggle a manager flips through
        // CatalogManagementStore.SetProductAvailabilityV1), so a suspended
        // item could still be added to a table draft and reach the
        // kitchen.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var suspended = await _database.SeedProductAsync("86'd Ürün", 100m, isAvailable: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-20",
                [new OrderItemDraftDto(Guid.NewGuid(), suspended, "86'd Ürün", 1, 100m)])));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubmittingAWaiterOrderReallyConsumesItsStock()
    {
        // V1-RMD-144: Semih's decision (2026-09-10) — an order the staff take
        // themselves consumes stock the moment it is sent to the kitchen.
        // V1-RMD-143 had attached consumption to Accepted, which a waiter
        // order never reaches (it stops at Submitted), so nothing moved.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Adana", 520m);
        var stockItemId = await _database.SeedStockForProductAsync(product, 5m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var itemId = Guid.NewGuid();
        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-31",
                [new OrderItemDraftDto(itemId, product, "Adana", 2, 520m)])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(5m, await _database.OnHandQuantityAsync(stockItemId));

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        Assert.Equal(3m, await _database.OnHandQuantityAsync(stockItemId));
        Assert.Equal(1, await _database.ConsumptionMovementCountAsync(itemId));
    }

    [Fact]
    public async Task ASecondRoundOfItemsOnlyConsumesTheNewLine()
    {
        // V1-RMD-144: a dessert round after the starters were already sent
        // must not charge inventory for the starters a second time. What keeps
        // that true is that CreateOrUpdateTableDraftAsync only appends to an
        // order still in Draft, so a table whose order already left Draft
        // starts a NEW order carrying only the new lines. This test pins that
        // behaviour: widen that lookup past 'Draft' and the first round's
        // stock is consumed twice.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Adana", 520m);
        var stockItemId = await _database.SeedStockForProductAsync(product, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var firstItemId = Guid.NewGuid();
        using var firstDraft = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-32",
                [new OrderItemDraftDto(firstItemId, product, "Adana", 2, 520m)])));
        var first = await firstDraft.Content.ReadFromJsonAsync<OrderDto>();
        using var firstSubmit = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, first!.OrderId), cookie,
            new SubmitTableOrderRequest(first.OrderId, first.RowVersion, Guid.NewGuid().ToString())));
        Assert.Equal(HttpStatusCode.OK, firstSubmit.StatusCode);
        Assert.Equal(8m, await _database.OnHandQuantityAsync(stockItemId));

        var secondItemId = Guid.NewGuid();
        using var secondDraft = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-32",
                [new OrderItemDraftDto(secondItemId, product, "Adana", 1, 520m)])));
        var second = await secondDraft.Content.ReadFromJsonAsync<OrderDto>();
        using var secondSubmit = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, second!.OrderId), cookie,
            new SubmitTableOrderRequest(second.OrderId, second.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.OK, secondSubmit.StatusCode);
        // 10 - 2 (first round) - 1 (second round only), not - 2 again.
        Assert.Equal(7m, await _database.OnHandQuantityAsync(stockItemId));
        Assert.Equal(1, await _database.ConsumptionMovementCountAsync(firstItemId));
        Assert.Equal(1, await _database.ConsumptionMovementCountAsync(secondItemId));
    }

    [Fact]
    public async Task RetryingASubmitWithTheSameOperationIdDoesNotConsumeStockTwice()
    {
        // V1-RMD-144: the offline queue resends a submit whose response was
        // lost. SubmitOrderHandler replays the stored response without
        // re-running the dispatcher at all, so no second consumption happens.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Adana", 520m);
        var stockItemId = await _database.SeedStockForProductAsync(product, 5m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var itemId = Guid.NewGuid();
        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-33",
                [new OrderItemDraftDto(itemId, product, "Adana", 2, 520m)])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();
        var submitBody = new SubmitTableOrderRequest(draft!.OrderId, draft.RowVersion, Guid.NewGuid().ToString());

        using var first = await client.SendAsync(JsonRequest(SubmitPath(terminalId, draft.OrderId), cookie, submitBody));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var retry = await client.SendAsync(JsonRequest(SubmitPath(terminalId, draft.OrderId), cookie, submitBody));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        Assert.Equal(3m, await _database.OnHandQuantityAsync(stockItemId));
        Assert.Equal(1, await _database.ConsumptionMovementCountAsync(itemId));
    }

    [Fact]
    public async Task SubmittingMoreThanTheRemainingStockIsRejectedAndChangesNothing()
    {
        // V1-RMD-144: same all-or-nothing rule as the Accept path — the order
        // stays Draft, no stock moves, and no kitchen ticket is written.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Adana", 520m);
        var stockItemId = await _database.SeedStockForProductAsync(product, 1m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-34",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana", 3, 520m)])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.Conflict, submitResponse.StatusCode);
        Assert.Equal(1m, await _database.OnHandQuantityAsync(stockItemId));
        Assert.Equal(0, await _database.KitchenTicketCountAsync(draft.OrderId));
    }

    [Fact]
    public async Task SubmittingAProductWithNoStockMappingIsRejected()
    {
        // V1-RMD-144: Semih's decision (2026-09-10) — an unmapped product
        // refuses the whole submission rather than being treated as
        // "not stock-tracked". A product must be mapped before it can sell.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var unmapped = await _database.SeedProductAsync("Eşlenmemiş Ürün", 120m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-35",
                [new OrderItemDraftDto(Guid.NewGuid(), unmapped, "Eşlenmemiş Ürün", 1, 120m)])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.Conflict, submitResponse.StatusCode);
        Assert.Equal(0, await _database.KitchenTicketCountAsync(draft.OrderId));
    }

    [Fact]
    public async Task AHalfPortionSurvivesDraftSubmitAndReadBack()
    {
        // V1-RMD-146: the projection used to write (int)i.Quantity, so a half
        // portion came back as 0 even though the column is NUMERIC(18,3) and
        // the aggregate holds a decimal.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Tavuk şiş", 420m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-40",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Tavuk şiş", 0.5m, 420m)])));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(0.5m, draft!.Items.Single().Quantity);

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var submitted = await submitResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(0.5m, submitted!.Items.Single().Quantity);
    }

    [Fact]
    public async Task AQuantityThatWouldRoundAwayToZeroIsRejected()
    {
        // V1-RMD-146: opening the contract to decimal also opens the door to
        // a value below the column's own precision, which would round to zero
        // before the aggregate's quantity > 0 rule ever ran.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Tavuk şiş", 420m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-41",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Tavuk şiş", 0.0001m, 420m)])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ASubmittedItemReportsItsStatusKitchenStateAndCreationTime()
    {
        // V1-RMD-146: the projection dropped all three, so a waiter could not
        // tell a cancelled line from a live one, could not see what the
        // kitchen was doing, and could not tell one round from the next.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-42",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m)])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Draft", draft!.Items.Single().Status);

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));
        var submitted = await submitResponse.Content.ReadFromJsonAsync<OrderDto>();

        var item = submitted!.Items.Single();
        Assert.Equal("Active", item.Status);
        // V1-RMD-154: this used to expect "NotSent", which was the defect
        // rather than the contract — the line had just been sent to the
        // kitchen and a ticket printed for it, and it still claimed otherwise.
        // Nothing advanced KitchenState except the KDS live-sync path, which
        // is off by default, so the "already sent" wall on the cheap void
        // endpoint never fired and a plated dish could be voided without the
        // bills.void approval, without cancelling the ticket and without
        // giving its stock back.
        Assert.Equal("Sent", item.KitchenState);
        Assert.True(item.CreatedAt > before, $"CreatedAt was {item.CreatedAt}");
    }

    [Fact]
    public async Task ModifiersSurviveTheDraftAndAreResolvedFromTheCatalog()
    {
        // V1-RMD-147: the store built every item with modifiers: null, so an
        // extra a waiter picked never reached the order at all. The name and
        // price must come from the catalog, not from whatever the client sent.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Adana kebap", 520m, 10m);
        var extraRice = await _database.SeedModifierAsync(product, "Ekstra pilav", 120m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-50",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana kebap", 2, 520m, [new OrderItemModifierSelectionDto(extraRice)])])));

        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();
        var item = draft!.Items.Single();

        var modifier = Assert.Single(item.Modifiers!);
        Assert.Equal(extraRice, modifier.ModifierId);
        Assert.Equal("Ekstra pilav", modifier.Name);
        Assert.Equal(120m, modifier.PriceDelta);

        // OrderItem.LineSubtotal(): UnitPrice * Quantity + the modifier's own
        // total, so 520*2 + 120. TotalPrice is gross, so it is at least that.
        Assert.True(item.TotalPrice >= 1160m, $"TotalPrice was {item.TotalPrice}");
    }

    [Fact]
    public async Task AModifierThatDoesNotBelongToTheProductIsRejected()
    {
        // V1-RMD-147: silently dropping an unknown id is exactly the defect
        // this closed, so an id the product does not own must be loud.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Adana kebap", 520m, 10m);
        var foreign = await _database.SeedModifierAsync(product, "Başka ürünün eklentisi", 50m, assignToProduct: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-51",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana kebap", 1, 520m, [new OrderItemModifierSelectionDto(foreign)])])));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnInactiveModifierIsRejected()
    {
        // V1-RMD-147: a manager retiring a modifier must stop it being ordered,
        // the same way catalog.products.is_available already stops a product.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Adana kebap", 520m, 10m);
        var retired = await _database.SeedModifierAsync(product, "Kaldırılmış eklenti", 30m, active: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-52",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana kebap", 1, 520m, [new OrderItemModifierSelectionDto(retired)])])));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ModifiersStillReadBackAfterSubmit()
    {
        // V1-RMD-147: the repository already persisted order_item_modifiers;
        // this pins that the round trip through submit keeps them.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Kuzu şiş", 620m, 10m);
        var wellDone = await _database.SeedModifierAsync(product, "İyi pişmiş", 0m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-53",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Kuzu şiş", 1, 620m, [new OrderItemModifierSelectionDto(wellDone)])])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<OrderDto>();
        var modifier = Assert.Single(submitted!.Items.Single().Modifiers!);
        Assert.Equal("İyi pişmiş", modifier.Name);
    }

    [Fact]
    public async Task AModifierOnATwoPortionLineIsChargedTwice()
    {
        // V1-RMD-150: Semih's question — two portions of Adana with extra
        // rice. Two plates go out, so two portions of rice are prepared and
        // charged. The quantity was hard-coded to 1 before this.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Adana kebap", 520m, 10m);
        var extraRice = await _database.SeedModifierAsync(product, "Ekstra pilav", 120m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-60",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana kebap", 2, 520m,
                    [new OrderItemModifierSelectionDto(extraRice)])])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<OrderDto>())!.Items.Single();
        Assert.Equal(2m, Assert.Single(item.Modifiers!).Quantity);
        // 520*2 + 120*2 = 1280, not 1160 as it was when quantity was pinned to 1.
        Assert.True(item.TotalPrice >= 1280m, $"TotalPrice was {item.TotalPrice}");
    }

    [Fact]
    public async Task AModifierOnAHalfPortionIsStillChargedOnce()
    {
        // V1-RMD-150: half a portion is still one plate, and the rice on it
        // is not half. Scaling the modifier by the raw quantity would have
        // produced 0,5 — which is why the default is the ceiling.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Adana kebap", 520m, 10m);
        var extraRice = await _database.SeedModifierAsync(product, "Ekstra pilav", 120m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-61",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana kebap", 0.5m, 520m,
                    [new OrderItemModifierSelectionDto(extraRice)])])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<OrderDto>())!.Items.Single();
        Assert.Equal(1m, Assert.Single(item.Modifiers!).Quantity);
    }

    [Fact]
    public async Task AnExplicitModifierQuantityOverridesTheDefault()
    {
        // V1-RMD-150: the default is a default, not a rule — one plate can
        // want two helpings, and the waiter decides that at order time.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Adana kebap", 520m, 10m);
        var extraRice = await _database.SeedModifierAsync(product, "Ekstra pilav", 120m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-62",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana kebap", 1, 520m,
                    [new OrderItemModifierSelectionDto(extraRice, 3m)])])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<OrderDto>())!.Items.Single();
        Assert.Equal(3m, Assert.Single(item.Modifiers!).Quantity);
    }

    [Fact]
    public async Task AMappedModifierLeavesTheStoreRoomWhenTheOrderIsSent()
    {
        // V1-RMD-152: extra cheese is real cheese. V1-RMD-143 left this open
        // and the consumption service only ever looked at item.ProductId.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Adana kebap", 520m, 10m);
        var extraCheese = await _database.SeedModifierAsync(product, "Ekstra peynir", 85m);
        var cheeseStock = await _database.SeedStockForModifierAsync(extraCheese, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-70",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana kebap", 2, 520m,
                    [new OrderItemModifierSelectionDto(extraCheese)])])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(10m, await _database.OnHandQuantityAsync(cheeseStock));

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        // Two plates, two helpings of cheese — the same number V1-RMD-150
        // charges for and the kitchen ticket prints.
        Assert.Equal(8m, await _database.OnHandQuantityAsync(cheeseStock));
    }

    [Fact]
    public async Task AModifierWithNoStockMappingDoesNotBlockTheOrder()
    {
        // V1-RMD-152: deliberately unlike a product. Most modifiers are an
        // instruction (a cooking preference), not an ingredient, and demanding a stock
        // item for every free choice would bloat configuration for nothing.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Kuzu şiş", 620m, 10m);
        var wellDone = await _database.SeedModifierAsync(product, "İyi pişmiş", 0m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-71",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Kuzu şiş", 1, 620m,
                    [new OrderItemModifierSelectionDto(wellDone)])])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
    }

    [Fact]
    public async Task AModifierWhoseStockIsShortRefusesTheWholeSubmission()
    {
        // V1-RMD-152: all-or-nothing, exactly like the product side — the
        // product's own delta must roll back with the modifier's refusal.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Adana kebap", 520m);
        var productStock = await _database.SeedStockForProductAsync(product, 10m);
        var extraCheese = await _database.SeedModifierAsync(product, "Ekstra peynir", 85m);
        var cheeseStock = await _database.SeedStockForModifierAsync(extraCheese, 1m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-72",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Adana kebap", 3, 520m,
                    [new OrderItemModifierSelectionDto(extraCheese)])])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.Conflict, submitResponse.StatusCode);
        Assert.Equal(10m, await _database.OnHandQuantityAsync(productStock));
        Assert.Equal(1m, await _database.OnHandQuantityAsync(cheeseStock));
        Assert.Equal(0, await _database.KitchenTicketCountAsync(draft.OrderId));
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
