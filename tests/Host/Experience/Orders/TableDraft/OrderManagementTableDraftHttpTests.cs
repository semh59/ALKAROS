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

    // V1-WTR-015 (garson karşılaştırma dokümanı, "Katman B" eksiği): party
    // size - every rival POS surveyed tracked it, ALKAROS tracked none.

    [Fact]
    public async Task ATableDraftWithAPartySizeStoresItOnTheOrder()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-06",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m)],
                PartySize: 4)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(4, draft!.PartySize);
    }

    [Fact]
    public async Task APartySizeSurvivesASecondRoundOnTheSameTable()
    {
        // V1-WTR-015: Order.TransitionTo and RebuildWith both reconstruct
        // the aggregate by hand from its own current fields - the exact
        // shape of bug that already silently dropped ServingUserId once
        // (V1-RMD-154, see that fix's own comment in ItemExceptionHandler
        // .cs). This proves PartySize was threaded through correctly, not
        // just accepted on the first call and then lost.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedProductAsync("Çorba", 60m);
        var dessert = await _database.SeedProductAsync("Baklava", 90m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var firstResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-07",
                [new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 1, 60m)],
                PartySize: 3)));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // The second round deliberately omits PartySize - matching what
        // the real waiter client actually sends (it resends the current
        // draftPartySize every round, but a fresh client session or an
        // older app build might not); the point is the SERVER never
        // re-derives it from a later request once an order exists.
        using var secondResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-07",
                [new OrderItemDraftDto(Guid.NewGuid(), dessert, "Baklava", 1, 90m)])));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondDraft = await secondResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(3, secondDraft!.PartySize);
    }

    [Fact]
    public async Task AnOutOfRangePartySizeIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-08",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m)],
                PartySize: 0)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // V1-WTR-022 (garson karşılaştırma dokümanı, "koltuk bazlı atama"): a
    // waiter can tag an order line with the table seat it was ordered for.

    [Fact]
    public async Task AnItemTaggedWithARealTableSeatStoresAndReturnsIt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var seatId = await _database.SeedSeatAsync(tableId, seatNumber: 3);
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-09",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m, SeatId: seatId)])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(seatId, Assert.Single(draft!.Items).SeatId);
    }

    [Fact]
    public async Task ASeatIdFromAnotherTableIsIgnoredRatherThanTrusted()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var otherTableId = await _database.SeedTableAsync();
        var foreignSeatId = await _database.SeedSeatAsync(otherTableId, seatNumber: 1);
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-10",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m, SeatId: foreignSeatId)])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Null(Assert.Single(draft!.Items).SeatId);
    }

    /// <summary>
    /// Found in an independent review (2026-09-11): ItemContentUnchanged
    /// compared quantity/notes/modifiers but not SeatId, so a resend that
    /// only changed the seat (same everything else) read as "nothing to do"
    /// and never reached ReconcileRound - the seat change was silently
    /// dropped. Same "field reset by a reconstruction path" bug class
    /// V1-WTR-022 already fixed for void/comp/ChangeQuantity, missed here.
    /// </summary>
    [Fact]
    public async Task ResendingTheSameLineWithOnlyTheSeatChangedUpdatesTheStoredSeat()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var firstSeatId = await _database.SeedSeatAsync(tableId, seatNumber: 1);
        var secondSeatId = await _database.SeedSeatAsync(tableId, seatNumber: 2);
        var product = await _database.SeedProductAsync("Çorba", 60m);
        var lineId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var firstResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-11",
                [new OrderItemDraftDto(lineId, product, "Çorba", 1, 60m, SeatId: firstSeatId)])));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstDraft = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(firstSeatId, Assert.Single(firstDraft!.Items).SeatId);

        // Same table-draft call, SAME line id, identical quantity/notes/
        // modifiers - only the seat differs.
        using var reseatedResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-11",
                [new OrderItemDraftDto(lineId, product, "Çorba", 1, 60m, SeatId: secondSeatId)])));
        Assert.Equal(HttpStatusCode.OK, reseatedResponse.StatusCode);
        var reseatedDraft = await reseatedResponse.Content.ReadFromJsonAsync<OrderDto>();

        var item = Assert.Single(reseatedDraft!.Items);
        Assert.Equal(lineId, item.ItemId);
        Assert.Equal(secondSeatId, item.SeatId);
    }

    [Fact]
    public async Task ResendingTheSameLineWithACorrectedQuantityUpdatesTheStoredItem()
    {
        // V1-RMD-164: found by the 2026-09-10 Garson audit — the exact
        // scenario: table-draft succeeds (the item now exists, Draft,
        // NotSent), the caller (in production: submit-draft failing on the
        // same round) then resends the SAME client-generated line id with a
        // corrected quantity. The old merge logic saw the id already
        // existed and dropped the correction outright, permanently keeping
        // the stale quantity - the kitchen would get the wrong amount and
        // the screen would show the round as sent.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        var lineId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var firstResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-06",
                [new OrderItemDraftDto(lineId, product, "Çorba", 2, 60m)])));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstDraft = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(2m, Assert.Single(firstDraft!.Items).Quantity);

        // Same table-draft call, SAME line id, corrected quantity - exactly
        // what a waiter resending a failed round after fixing a typo sends.
        using var correctedResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-06",
                [new OrderItemDraftDto(lineId, product, "Çorba", 3, 60m)])));
        Assert.Equal(HttpStatusCode.OK, correctedResponse.StatusCode);
        var correctedDraft = await correctedResponse.Content.ReadFromJsonAsync<OrderDto>();

        var item = Assert.Single(correctedDraft!.Items);
        Assert.Equal(lineId, item.ItemId);
        Assert.Equal(3m, item.Quantity);
        Assert.Equal(180m, correctedDraft.TotalAmount);
        Assert.Equal(firstDraft.OrderId, correctedDraft.OrderId);

        // A third resend with the SAME corrected content must be the pure-
        // retry path (no change), not a second silent overwrite loop.
        using var repeatResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-06",
                [new OrderItemDraftDto(lineId, product, "Çorba", 3, 60m)])));
        Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
        var repeatDraft = await repeatResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal(3m, Assert.Single(repeatDraft!.Items).Quantity);
        Assert.Equal(correctedDraft.RowVersion, repeatDraft.RowVersion);
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

    /// <summary>
    /// V1-WTR-025: submitting a whole multi-course draft in one round
    /// commits every course (all Active) but only sends the lowest course
    /// number to the kitchen; firing the next course promotes just that
    /// course to Sent and prints a second ticket for it alone.
    /// </summary>
    [Fact]
    public async Task SubmittingAMultiCourseDraftHoldsLaterCoursesUntilFired()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedStockedProductAsync("Çorba", 90m, 10m);
        var main = await _database.SeedStockedProductAsync("Izgara", 350m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-09",
            [
                new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 1, 90m, CourseNumber: 1),
                new OrderItemDraftDto(Guid.NewGuid(), main, "Izgara", 1, 350m, CourseNumber: 2),
            ])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<OrderDto>();

        var starterItem = submitted!.Items.Single(i => i.CourseNumber == 1);
        var mainItem = submitted.Items.Single(i => i.CourseNumber == 2);
        Assert.Equal("Sent", starterItem.KitchenState);
        Assert.Equal("Held", mainItem.KitchenState);
        // Both courses are already committed to the check, not just course 1.
        Assert.Equal("Active", starterItem.Status);
        Assert.Equal("Active", mainItem.Status);
        // The whole-plan ticket is one ticket, showing both courses.
        Assert.Equal(1, await _database.KitchenTicketCountAsync(draft.OrderId));

        using var fireResponse = await client.SendAsync(JsonRequest(
            FireCoursePath(terminalId, draft.OrderId), cookie, new FireCourseRequestV1(2)));

        Assert.Equal(HttpStatusCode.OK, fireResponse.StatusCode);
        var fired = await fireResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Sent", fired!.Items.Single(i => i.CourseNumber == 2).KitchenState);
        // Firing the course prints its own dedicated ticket.
        Assert.Equal(2, await _database.KitchenTicketCountAsync(draft.OrderId));
    }

    [Fact]
    public async Task FiringACourseWithNothingHeldForItReturnsAConflict()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Köfte", 280m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-10",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Köfte", 1, 280m)])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();
        await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        using var fireResponse = await client.SendAsync(JsonRequest(
            FireCoursePath(terminalId, draft.OrderId), cookie, new FireCourseRequestV1(2)));

        Assert.Equal(HttpStatusCode.Conflict, fireResponse.StatusCode);
    }

    [Fact]
    public async Task RetryingAndAlreadyFiredCourseReplaysWithoutASecondTicket()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedStockedProductAsync("Çorba", 90m, 10m);
        var main = await _database.SeedStockedProductAsync("Izgara", 350m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-11",
            [
                new OrderItemDraftDto(Guid.NewGuid(), starter, "Çorba", 1, 90m, CourseNumber: 1),
                new OrderItemDraftDto(Guid.NewGuid(), main, "Izgara", 1, 350m, CourseNumber: 2),
            ])));
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();
        await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion, Guid.NewGuid().ToString())));

        using var first = await client.SendAsync(JsonRequest(
            FireCoursePath(terminalId, draft.OrderId), cookie, new FireCourseRequestV1(2)));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(2, await _database.KitchenTicketCountAsync(draft.OrderId));

        // A second fire of an already-Sent course finds no Held items left
        // for it and is refused, not silently replayed as success -
        // Order.FireCourse's own guard, same as re-voiding an already-voided
        // item elsewhere in this file.
        using var second = await client.SendAsync(JsonRequest(
            FireCoursePath(terminalId, draft.OrderId), cookie, new FireCourseRequestV1(2)));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(2, await _database.KitchenTicketCountAsync(draft.OrderId));
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

    // V1-RMD-177: found by the 2026-09-10 Garson audit — /transfer-server
    // had no client anywhere, and the reason was deeper than "nobody built
    // the button": no session below manager level had any way to list
    // staff at all to pick a hand-off target from. This is the first real
    // caller of GET /staff.

    [Fact]
    public async Task StaffListsOtherActiveUsersButExcludesTheCallerThemselves()
    {
        var terminalId = Guid.NewGuid();
        var (callerId, callerCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.create");
        var (colleagueId, _) = await _database.SeedCashierSessionWithPermissionsAsync(
            Guid.NewGuid(), "waiter", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(GetRequest(StaffPath(terminalId), callerCookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var staff = await response.Content.ReadFromJsonAsync<StaffMemberV1[]>();
        // The helper hardcodes display_name to "Table Draft API Test" for
        // every seeded user - asserting on it here is still a real check
        // that DisplayName round-trips from identity.users, not a filler.
        Assert.Contains(staff!, member => member.UserId == colleagueId && member.DisplayName == "Table Draft API Test");
        Assert.DoesNotContain(staff!, member => member.UserId == callerId);
    }

    [Fact]
    public async Task StaffWithoutASessionCookieIsUnauthorized()
    {
        var terminalId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.GetAsync(StaffPath(terminalId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // V1-WTR-013: an optional context note a departing waiter can leave on
    // /transfer-server - "table 5 is waiting on dessert" - popped (read and
    // marked seen in one round trip) exactly once by the receiving waiter.

    [Fact]
    public async Task ATransferWithAHandoffNoteLetsTheReceiverPopItExactlyOnce()
    {
        var fromTerminalId = Guid.NewGuid();
        var toTerminalId = Guid.NewGuid();
        var (fromWaiterId, fromCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            fromTerminalId, "waiter", "orders.transfer-server");
        var (_, toCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            toTerminalId, "waiter", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        // ToUserId must be a real, known target - read it back from /staff
        // rather than re-deriving the seeded id, so this test exercises the
        // same lookup the real client uses.
        using var staffResponse = await client.SendAsync(GetRequest(StaffPath(fromTerminalId), fromCookie));
        var staff = await staffResponse.Content.ReadFromJsonAsync<StaffMemberV1[]>();
        var toUserId = staff!.Single().UserId;

        using var transfer = await client.SendAsync(JsonRequest(TransferPath(fromTerminalId), fromCookie,
            new TransferServingUserRequestV1(fromWaiterId, toUserId, "5 nolu masa tatlı bekliyor")));
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        using var firstPop = await client.SendAsync(PostRequest(HandoffNotePopPath(toTerminalId), toCookie));
        Assert.Equal(HttpStatusCode.OK, firstPop.StatusCode);
        var note = await firstPop.Content.ReadFromJsonAsync<ServingHandoffNoteV1>();
        Assert.Equal("5 nolu masa tatlı bekliyor", note!.Note);
        Assert.Equal("Table Draft API Test", note.FromDisplayName);

        // Read once: the same note must not come back a second time.
        using var secondPop = await client.SendAsync(PostRequest(HandoffNotePopPath(toTerminalId), toCookie));
        Assert.Equal(HttpStatusCode.NoContent, secondPop.StatusCode);
    }

    [Fact]
    public async Task PoppingWithNoPendingNoteReturnsNoContent()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(PostRequest(HandoffNotePopPath(terminalId), cookie));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ATransferWithNoHandoffNoteLeavesNothingToPop()
    {
        var terminalId = Guid.NewGuid();
        var toTerminalId = Guid.NewGuid();
        var (waiterId, cookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.transfer-server");
        var (toWaiterId, toCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            toTerminalId, "waiter", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var transfer = await client.SendAsync(JsonRequest(TransferPath(terminalId), cookie,
            new TransferServingUserRequestV1(waiterId, toWaiterId)));
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        using var pop = await client.SendAsync(PostRequest(HandoffNotePopPath(toTerminalId), toCookie));
        Assert.Equal(HttpStatusCode.NoContent, pop.StatusCode);
    }

    [Fact]
    public async Task ATooLongHandoffNoteIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, cookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.transfer-server");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var transfer = await client.SendAsync(JsonRequest(TransferPath(terminalId), cookie,
            new TransferServingUserRequestV1(waiterId, Guid.NewGuid(), new string('a', 201))));

        Assert.Equal(HttpStatusCode.BadRequest, transfer.StatusCode);
    }

    /// <summary>
    /// Found in an independent review (2026-09-11): TransferServingUserAsync
    /// used to run BEFORE the note's length was validated, so a rejected
    /// note (400) still left every one of the departing waiter's tables
    /// reassigned. The note is now validated and stored first, so a 400 here
    /// must mean nothing moved at all.
    /// </summary>
    [Fact]
    public async Task ATooLongHandoffNoteLeavesTheTablesUntransferred()
    {
        var terminalId = Guid.NewGuid();
        var (fromWaiterId, fromCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.create", "orders.send", "orders.transfer-server");
        var toWaiterId = Guid.NewGuid();
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), fromCookie,
            new CreateTableDraftRequest(tableId, "M-99",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var transfer = await client.SendAsync(JsonRequest(TransferPath(terminalId), fromCookie,
            new TransferServingUserRequestV1(fromWaiterId, toWaiterId, new string('a', 201))));

        Assert.Equal(HttpStatusCode.BadRequest, transfer.StatusCode);
        Assert.Equal(fromWaiterId, await _database.GetServingUserIdAsync(draft!.OrderId));
    }

    /// <summary>
    /// V1-RMD-181: found by the 2026-09-12 five-agent independent Garson
    /// audit — the bulk reassignment left row_version untouched, so a
    /// stale reader (a waiter mid-edit on the table, still holding the OLD
    /// row_version) would have passed UpdateOrderAsync's own optimistic
    /// concurrency check afterwards even though serving_user_id had, in
    /// fact, just changed under them. A real shift transfer must bump it
    /// like every other mutation does.
    /// </summary>
    [Fact]
    public async Task TransferringServingUserBumpsTheOrdersRowVersion()
    {
        var terminalId = Guid.NewGuid();
        var toTerminalId = Guid.NewGuid();
        var (fromWaiterId, fromCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.create", "orders.send", "orders.transfer-server");
        var (toWaiterId, _) = await _database.SeedCashierSessionWithPermissionsAsync(
            toTerminalId, "waiter", "orders.create");
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), fromCookie,
            new CreateTableDraftRequest(tableId, "M-181",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        var rowVersionBefore = await _database.GetRowVersionAsync(draft!.OrderId);

        using var transfer = await client.SendAsync(JsonRequest(TransferPath(terminalId), fromCookie,
            new TransferServingUserRequestV1(fromWaiterId, toWaiterId, "")));
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        var rowVersionAfter = await _database.GetRowVersionAsync(draft.OrderId);
        Assert.True(rowVersionAfter > rowVersionBefore,
            $"Expected row_version to advance past {rowVersionBefore} after a serving-user transfer, stayed at {rowVersionAfter}.");
        Assert.Equal(toWaiterId, await _database.GetServingUserIdAsync(draft.OrderId));
    }

    [Fact]
    public async Task ASecondHandoffNoteSupersedesTheFirstUnpoppedOne()
    {
        var terminalId = Guid.NewGuid();
        var toTerminalId = Guid.NewGuid();
        var (waiterId, cookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "waiter", "orders.transfer-server");
        var (toWaiterId, toCookie) = await _database.SeedCashierSessionWithPermissionsAsync(
            toTerminalId, "waiter", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var firstTransfer = await client.SendAsync(JsonRequest(TransferPath(terminalId), cookie,
            new TransferServingUserRequestV1(waiterId, toWaiterId, "İlk not")));
        Assert.Equal(HttpStatusCode.OK, firstTransfer.StatusCode);
        using var secondTransfer = await client.SendAsync(JsonRequest(TransferPath(terminalId), cookie,
            new TransferServingUserRequestV1(waiterId, toWaiterId, "İkinci not")));
        Assert.Equal(HttpStatusCode.OK, secondTransfer.StatusCode);

        using var pop = await client.SendAsync(PostRequest(HandoffNotePopPath(toTerminalId), toCookie));
        Assert.Equal(HttpStatusCode.OK, pop.StatusCode);
        var note = await pop.Content.ReadFromJsonAsync<ServingHandoffNoteV1>();
        Assert.Equal("İkinci not", note!.Note);

        // The superseded first note must not surface later either.
        using var secondPop = await client.SendAsync(PostRequest(HandoffNotePopPath(toTerminalId), toCookie));
        Assert.Equal(HttpStatusCode.NoContent, secondPop.StatusCode);
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
    public async Task ARequiredModifierGroupWithNoSelectionIsRejected()
    {
        // V1-RMD-161: found by the 2026-09-10 Garson audit — a modifier
        // group's min_selections was never enforced. A required group
        // (min 1) picked from not at all used to go through silently.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Pizza", 400m, 10m);
        await _database.SeedModifierGroupWithTwoOptionsAsync(product, minSelections: 1, maxSelections: 1);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-54",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Pizza", 1, 400m)])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SelectingMoreThanAModifierGroupsMaxIsRejected()
    {
        // V1-RMD-161: a group capped at one selection (e.g. "pick one size")
        // used to accept both options with no complaint.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Pizza", 400m, 10m);
        var (_, first, second) = await _database.SeedModifierGroupWithTwoOptionsAsync(
            product, minSelections: 0, maxSelections: 1);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-55",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Pizza", 1, 400m,
                    [new OrderItemModifierSelectionDto(first), new OrderItemModifierSelectionDto(second)])])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ASelectionWithinTheGroupsRuleIsAccepted()
    {
        // V1-RMD-161: the positive case — exactly one selection from a
        // required, max-one group must still work.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Pizza", 400m, 10m);
        var (_, first, _) = await _database.SeedModifierGroupWithTwoOptionsAsync(
            product, minSelections: 1, maxSelections: 1);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-56",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Pizza", 1, 400m,
                    [new OrderItemModifierSelectionDto(first)])])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AQuantityAboveTheMaximumIsRejected()
    {
        // V1-RMD-162: found by the 2026-09-10 Garson audit — quantity had
        // no upper bound server-side.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Çorba", 90m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-57",
                [new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1_000_000m, 90m)])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ANoteLongerThanTheMaximumIsRejected()
    {
        // V1-RMD-162: found by the 2026-09-10 Garson audit — note length
        // had no upper bound server-side.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Çorba", 90m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-58",
                [new OrderItemDraftDto(
                    Guid.NewGuid(), product, "Çorba", 1, 90m,
                    SpecialInstructions: new string('a', 5000))])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TooManyItemsInOneDraftIsRejected()
    {
        // V1-RMD-162: found by the 2026-09-10 Garson audit — item count had
        // no upper bound server-side.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedStockedProductAsync("Çorba", 90m, 10m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var items = Enumerable.Range(0, 250)
            .Select(_ => new OrderItemDraftDto(Guid.NewGuid(), product, "Çorba", 1, 90m))
            .ToArray();

        using var response = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-59", items)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static string FireCoursePath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/fire-course";

    private static string TransferPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/orders/transfer-server";

    private static string StaffPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/orders/staff";

    private static string HandoffNotePopPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/orders/handoff-note/pop";

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage PostRequest(string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
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

[CollectionDefinition("Order table-draft PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OrderTableDraftPostgresqlDefinition;
