using System.Net;
using System.Net.Http.Json;
using System.Text;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.TableDraft.Tests;

/// <summary>
/// V1-ORD-006: a check belongs to a party, not to a table.
///
/// The scenario these cover is Semih's own (2026-09-10): the party has eaten,
/// got up, and is queueing at the till while new guests are already waiting
/// for the table. Before this task the system answered that badly — the new
/// party's first order repointed the table away from the unpaid check, which
/// then appeared on no table at all and dropped out of the table total.
///
/// The same root cause produced the more expensive defect: the merge lookup
/// matched <c>status = 'Draft'</c>, and the waiter client never leaves an
/// order in Draft (it draft-then-submits in one call), so every round after
/// the first opened a second order while the read path returned only the
/// newest. A party ordering starters and then mains was billed for the mains
/// alone.
/// </summary>
[Collection("Order table-draft PostgreSQL HTTP")]
public sealed class CheckLifecycleHttpTests : IAsyncLifetime
{
    private readonly OrderManagementTableDraftTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ASecondRoundAfterSubmitStaysOnTheSameCheck()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedStockedProductAsync("Çorba", 60m, 50m);
        var main = await _database.SeedStockedProductAsync("Köfte", 280m, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var round1 = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 2, 60m));
        var round2 = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), main, "Köfte", 1, 280m));

        Assert.Equal(round1.OrderId, round2.OrderId);

        using var read = await client.SendAsync(GetRequest(TablePath(terminalId, tableId), cookie));
        var check = await read.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(2, check!.Items.Count);
        // The whole point: the starters are still on the bill.
        Assert.Equal(120m + 280m, check.Items.Sum(item => item.TotalPrice));
    }

    [Fact]
    public async Task ASecondRoundFiresOnlyItsOwnLineAndConsumesOnlyItsOwnStock()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedProductAsync("Çorba", 60m);
        var starterStock = await _database.SeedStockForProductAsync(starter, 50m);
        var main = await _database.SeedProductAsync("Köfte", 280m);
        var mainStock = await _database.SeedStockForProductAsync(main, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var round1 = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 2, 60m));
        Assert.Equal(48m, await _database.OnHandQuantityAsync(starterStock));
        Assert.Equal(1, await _database.KitchenTicketItemCountAsync(round1.OrderId));

        await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), main, "Köfte", 1, 280m));

        // Round one must not be cooked or consumed a second time. Deriving the
        // dispatch list from "every active line on the order" rather than from
        // the round that was just fired is exactly what would break both of
        // these assertions.
        Assert.Equal(48m, await _database.OnHandQuantityAsync(starterStock));
        Assert.Equal(49m, await _database.OnHandQuantityAsync(mainStock));
        Assert.Equal(2, await _database.KitchenTicketItemCountAsync(round1.OrderId));
    }

    [Fact]
    public async Task AnOrderOnATableThatStillCarriesACheckJoinsThatCheckInsteadOfOrphaningIt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var openCheck = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));

        var later = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));

        // Nothing on the server can tell "the same party's next round" from "a
        // new party sat down" — only the waiter knows, and they say so by
        // sending the check to the cashier (covered by the test below). What
        // matters here is the property that used to be violated: the earlier
        // check is never repointed away and orphaned. Both rounds are on one
        // check and the table still points at it.
        Assert.Equal(openCheck.OrderId, later.OrderId);
        Assert.True(await _database.TablePointsAtAsync(tableId, openCheck.OrderId));

        using var read = await client.SendAsync(GetRequest(TablePath(terminalId, tableId), cookie));
        var check = await read.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(560m, check!.Items.Sum(item => item.TotalPrice));
    }

    [Fact]
    public async Task SendingACheckToTheCashierReleasesTheTableAndLeavesTheCheckOpen()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var check = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));

        using var sent = await client.SendAsync(JsonRequest(
            SendToCashierPath(terminalId, check.OrderId), cookie, new SendCheckToCashierRequestV1(tableId)));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.False((await sent.Content.ReadFromJsonAsync<SendCheckToCashierResultV1>())!.AlreadySent);

        // The table is free for the next party...
        Assert.False(await _database.TableHasOpenCheckAsync(tableId));
        Assert.Equal("Cleaning", await _database.TableStatusAsync(tableId));

        // ...and the check is waiting at the till, still carrying its money.
        using var queue = await client.SendAsync(GetRequest(AwaitingPaymentPath(terminalId), cookie));
        var awaiting = await queue.Content.ReadFromJsonAsync<List<PendingCheckSummaryV1>>();
        Assert.Contains(awaiting!, c => c.OrderId == check.OrderId && c.Total == 280m);
    }

    // V1-RMD-279: the till's queue follows the BILL. A part-paid check shows how much is collected so the
    // cashier resumes it; a fully paid check leaves the queue (the order itself stays Submitted).
    [Fact]
    public async Task TheTillQueueShowsTheBillAndWhatIsPaidAndDropsAFullyPaidCheck()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var check = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));
        using var sent = await client.SendAsync(JsonRequest(
            SendToCashierPath(terminalId, check.OrderId), cookie, new SendCheckToCashierRequestV1(tableId)));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        async Task<PendingCheckSummaryV1?> InQueue()
        {
            using var queue = await client.SendAsync(GetRequest(AwaitingPaymentPath(terminalId), cookie));
            return (await queue.Content.ReadFromJsonAsync<List<PendingCheckSummaryV1>>())!.SingleOrDefault(c => c.OrderId == check.OrderId);
        }

        var beforeBill = await InQueue();
        Assert.NotNull(beforeBill);
        Assert.Null(beforeBill!.BillId);

        var billId = await _database.SeedBillForOrderAsync(check.OrderId, 280m, "Open", allocated: 100m);
        var partPaid = await InQueue();
        Assert.Equal(billId, partPaid!.BillId);
        Assert.Equal(100m, partPaid.PaidAmount);

        await _database.SetBillStatusAsync(billId, "Paid");
        Assert.Null(await InQueue());
    }

    [Fact]
    public async Task ANewPartyCanBeSeatedOnceTheCheckHasGoneToTheCashier()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var firstParty = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));
        using var sent = await client.SendAsync(JsonRequest(
            SendToCashierPath(terminalId, firstParty.OrderId), cookie, new SendCheckToCashierRequestV1(tableId)));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        var secondParty = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 2, 280m));

        // The till still holds the first check, the table is already working
        // for the next party, and the two never mix.
        Assert.NotEqual(firstParty.OrderId, secondParty.OrderId);
        using var read = await client.SendAsync(GetRequest(TablePath(terminalId, tableId), cookie));
        var visible = await read.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(secondParty.OrderId, visible!.OrderId);
        Assert.Equal(560m, visible.Items.Sum(item => item.TotalPrice));
    }

    [Fact]
    public async Task SendingACheckToTheCashierTwiceIsReportedRatherThanFailing()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var check = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));

        using var first = await client.SendAsync(JsonRequest(
            SendToCashierPath(terminalId, check.OrderId), cookie, new SendCheckToCashierRequestV1(tableId)));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.SendAsync(JsonRequest(
            SendToCashierPath(terminalId, check.OrderId), cookie, new SendCheckToCashierRequestV1(tableId)));

        // A double tap is the outcome the caller wanted, already true.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True((await second.Content.ReadFromJsonAsync<SendCheckToCashierResultV1>())!.AlreadySent);
    }

    // ── V1-RMD-154: the kitchen wall, end to end ─────────────────────────
    //
    // The handler's "already sent to the kitchen" rule was always there and
    // its unit tests always passed — because they seed an item with a chosen
    // KitchenState. Nothing ever checked what a line looks like after the
    // real submit path, and the answer was NotSent: the order went to the
    // kitchen, a ticket printed, stock came out of the depot, and the line
    // still claimed it had never been sent. So the wall never fired on a real
    // order and a plated dish could be voided for free.

    [Fact]
    public async Task AnItemThatReallyWentToTheKitchenCannotBeVoidedOnTheCheapPath()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Köfte", 280m);
        var stock = await _database.SeedStockForProductAsync(product, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var check = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));
        var itemId = check.Items.Single().ItemId;

        using var read = await client.SendAsync(GetRequest(TablePath(terminalId, tableId), cookie));
        var live = await read.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Sent", live!.Items.Single().KitchenState);

        using var voided = await client.SendAsync(JsonRequest(
            VoidPath(terminalId, check.OrderId, itemId), cookie,
            new VoidOrderItemRequestV1(live.RowVersion, "CustomerChange")));

        // Refused here. The eaten dish has to go through /void-sent, which is
        // grant-gated, cancels the kitchen ticket and gives the stock back.
        Assert.Equal(HttpStatusCode.Conflict, voided.StatusCode);
        Assert.Equal(49m, await _database.OnHandQuantityAsync(stock));
    }

    [Fact]
    public async Task ALineAddedButNotYetFiredCanBeVoidedAndConsumedNoStock()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Köfte", 280m);
        var stock = await _database.SeedStockForProductAsync(product, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        // Draft only — no submit, so nothing was fired.
        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-05",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m)], Id: Guid.NewGuid())));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Draft", draft!.Items.Single().Status);
        Assert.Equal(50m, await _database.OnHandQuantityAsync(stock));

        using var voided = await client.SendAsync(JsonRequest(
            VoidPath(terminalId, draft.OrderId, draft.Items.Single().ItemId), cookie,
            new VoidOrderItemRequestV1(draft.RowVersion, "OperatorError")));

        // This used to be refused outright — and reported as 409 "another
        // process changed it", which no retry could ever fix. It is the
        // cheapest void there is: no ticket printed, no stock moved.
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        Assert.Equal("Cancelled", (await voided.Content.ReadFromJsonAsync<VoidOrderItemResultV1>())!.NewItemStatus);
        Assert.Equal(50m, await _database.OnHandQuantityAsync(stock));
    }

    // ── V1-RMD-155: idempotency across rounds and across retries ─────────

    [Fact]
    public async Task ARetryOfTheSameRoundReplaysInsteadOfBeingRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Köfte", 280m);
        var stock = await _database.SeedStockForProductAsync(product, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var submissionId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var body = new CreateTableDraftRequest(tableId, "M-05",
            [new OrderItemDraftDto(lineId, product, "Köfte", 1, 280m)], Id: submissionId);

        using var firstDraft = await client.SendAsync(JsonRequest(DraftPath(terminalId), cookie, body));
        var draft = await firstDraft.Content.ReadFromJsonAsync<OrderDto>();
        using var firstSubmit = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, $"{draft.OrderId}:{submissionId}")));
        Assert.Equal(HttpStatusCode.OK, firstSubmit.StatusCode);

        // The response was lost on the way back, so the offline queue resends
        // the identical payload. The draft correctly replays — and hands back
        // the order's CURRENT row version, which is why hashing that version
        // turned every real retry into a 409.
        using var replayDraft = await client.SendAsync(JsonRequest(DraftPath(terminalId), cookie, body));
        var replayed = await replayDraft.Content.ReadFromJsonAsync<OrderDto>();
        using var retrySubmit = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, replayed!.OrderId), cookie,
            new SubmitTableOrderRequest(replayed.OrderId, replayed.RowVersion, $"{replayed.OrderId}:{submissionId}")));

        Assert.Equal(HttpStatusCode.OK, retrySubmit.StatusCode);
        // And nothing was cooked or consumed twice.
        Assert.Equal(49m, await _database.OnHandQuantityAsync(stock));
        Assert.Equal(1, await _database.KitchenTicketItemCountAsync(draft.OrderId));
    }

    [Fact]
    public async Task EachRoundOnACheckIsItsOwnOperationSoTheSecondOneIsNotRejectedAsAReplay()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Köfte", 280m);
        var stock = await _database.SeedStockForProductAsync(product, 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var first = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));
        var second = await SendRoundAsync(client, cookie, terminalId, tableId,
            new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m));

        // With the old `${orderId}:submit` key both rounds carried the same
        // operation id and the second came back 409 IDEMPOTENCY_KEY_REUSED —
        // SendRoundAsync asserts OK, so this test is the guard.
        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal(48m, await _database.OnHandQuantityAsync(stock));
        Assert.Equal(2, await _database.KitchenTicketItemCountAsync(first.OrderId));
    }

    private static string VoidPath(Guid terminalId, Guid orderId, Guid itemId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items/{itemId:D}/void";

    /// <summary>
    /// Draft then submit in one go, exactly as the waiter client does —
    /// including how it builds the operation id.
    /// </summary>
    /// <remarks>
    /// V1-RMD-155: this helper used to invent a random operation id per round.
    /// The real client derives one from the round's own submission id, and
    /// before this task derived it from the ORDER id, meaning every round of a
    /// check reused one key and the second came back 409. The tests passed
    /// anyway, because the helper's randomness hid exactly the collision the
    /// client would hit. A helper that does not send what the client sends is
    /// not testing the client.
    /// </remarks>
    private static async Task<OrderDto> SendRoundAsync(
        HttpClient client, string cookie, Guid terminalId, Guid tableId, params OrderItemDraftDto[] items)
    {
        var submissionId = Guid.NewGuid();

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-05", items, Id: submissionId)));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, $"{draft.OrderId}:{submissionId}")));
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        return draft;
    }

    private static string DraftPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/orders/table-draft";

    private static string SubmitPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/submit-draft";

    private static string TablePath(Guid terminalId, Guid tableId)
        => $"/api/v1/terminals/{terminalId:D}/orders/table/{tableId:D}";

    private static string SendToCashierPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/send-to-cashier";

    private static string AwaitingPaymentPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/orders/awaiting-payment";

    private static HttpRequestMessage GetRequest(string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Cookie", cookie);
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
