using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task RecentSalesListsAPaidBillOfTheOwnSessionWithItsTableAmountAndEveryMethod()
    {
        var seeded = await _database.SeedBillAsync();
        await CashPaymentAsync(seeded.BillId, 100m);
        await CardPaymentAsync(seeded.BillId, 50m);
        await OpenSessionAsync(_cashierId);
        await CloseBillAsync(seeded.BillId, DateTimeOffset.UtcNow.AddSeconds(5), _terminalId);
        var tableNumber = await Scalar<string>(
            "SELECT t.table_number FROM billing.bills b JOIN table_mgmt.tables t ON t.table_id = b.table_id WHERE b.bill_id = @p", seeded.BillId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var recent = await RecentSalesAsync(client);

        Assert.True(recent.HasOpenSession);
        var sale = Assert.Single(recent.Sales);
        Assert.Equal(seeded.BillId, sale.BillId);
        Assert.Equal(tableNumber, sale.TableNumber);
        Assert.Equal(150m, sale.Amount);
        Assert.Equal(["Card", "Cash"], sale.Methods);
    }

    [Fact]
    public async Task RecentSalesShowsWhatIsLeftAfterAPartialRefund()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await CloseBillAsync(seeded.BillId, DateTimeOffset.UtcNow.AddSeconds(5), _terminalId);
        await Execute(
            """
            INSERT INTO payments.payment_reversals (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            VALUES (gen_random_uuid(), @p, 'PartialRefund', 30, 'Eksik ürün', @u, 'recent-' || @p::text, now());
            """, paymentId, ("u", _cashierId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var recent = await RecentSalesAsync(client);

        Assert.Equal(70m, Assert.Single(recent.Sales).Amount);
    }

    [Fact]
    public async Task RecentSalesTakesTheTableFromTheOrderWhenTheCheckLeftItsTableAndHasNoneForAQuickSale()
    {
        var sent = await _database.SeedBillAsync();
        await CashPaymentAsync(sent.BillId, 100m);
        var quick = await _database.SeedBillAsync();
        await CashPaymentAsync(quick.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await CloseBillAsync(sent.BillId, DateTimeOffset.UtcNow.AddSeconds(5), _terminalId);
        await CloseBillAsync(quick.BillId, DateTimeOffset.UtcNow.AddSeconds(6), _terminalId);
        var tableNumber = await Scalar<string>(
            "SELECT t.table_number FROM billing.bills b JOIN table_mgmt.tables t ON t.table_id = b.table_id WHERE b.bill_id = @p", sent.BillId);
        await Execute("UPDATE billing.bills SET table_id = NULL WHERE bill_id = @p", sent.BillId);
        await Execute(
            "UPDATE orders.orders SET table_id = NULL WHERE order_id = (SELECT order_id FROM billing.bills WHERE bill_id = @p)", quick.BillId);
        await Execute("UPDATE billing.bills SET table_id = NULL WHERE bill_id = @p", quick.BillId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var recent = await RecentSalesAsync(client);

        Assert.Equal(tableNumber, recent.Sales.Single(sale => sale.BillId == sent.BillId).TableNumber);
        Assert.Null(recent.Sales.Single(sale => sale.BillId == quick.BillId).TableNumber);
    }

    [Fact]
    public async Task RecentSalesLeavesOutBillsThatAreNotPaidClosedBeforeTheSessionOrOwnedByAnotherTill()
    {
        var open = await _database.SeedBillAsync();
        var before = await _database.SeedBillAsync();
        var elsewhere = await _database.SeedBillAsync();
        var cancelled = await _database.SeedBillAsync();
        var unowned = await _database.SeedBillAsync();
        var mine = await _database.SeedBillAsync();
        foreach (var bill in new[] { open, before, elsewhere, cancelled, unowned, mine })
            await CashPaymentAsync(bill.BillId, 10m);
        await OpenSessionAsync(_cashierId);
        var later = DateTimeOffset.UtcNow.AddSeconds(5);
        await CloseBillAsync(before.BillId, DateTimeOffset.UtcNow.AddHours(-2), _terminalId);
        await CloseBillAsync(elsewhere.BillId, later, Guid.NewGuid());
        await CloseBillAsync(cancelled.BillId, later, _terminalId);
        await Execute("UPDATE billing.bills SET status = 'Cancelled', cancelled_at = now() WHERE bill_id = @p", cancelled.BillId);
        await CloseBillAsync(unowned.BillId, later, _terminalId);
        await Execute("UPDATE billing.bills SET terminal_id = NULL WHERE bill_id = @p", unowned.BillId);
        await CloseBillAsync(mine.BillId, later, _terminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var recent = await RecentSalesAsync(client);

        Assert.Equal(mine.BillId, Assert.Single(recent.Sales).BillId);
    }

    [Fact]
    public async Task RecentSalesListsTheNewestTwentyFirst()
    {
        await OpenSessionAsync(_cashierId);
        var first = DateTimeOffset.UtcNow.AddSeconds(5);
        var ids = new List<Guid>();
        for (var index = 0; index < 22; index++)
        {
            var bill = await _database.SeedBillAsync();
            await CashPaymentAsync(bill.BillId, 10m);
            await CloseBillAsync(bill.BillId, first.AddSeconds(index), _terminalId);
            ids.Add(bill.BillId);
        }

        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var recent = await RecentSalesAsync(client);

        Assert.Equal(Enumerable.Reverse(ids).Take(20), recent.Sales.Select(sale => sale.BillId));
    }

    [Fact]
    public async Task RecentSalesIsEmptyAndSaysSoWhenTheCallerHasNoOpenSession()
    {
        var seeded = await _database.SeedBillAsync();
        await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_managerId);
        await CloseBillAsync(seeded.BillId, DateTimeOffset.UtcNow.AddSeconds(5), _terminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var recent = await RecentSalesAsync(client);

        Assert.False(recent.HasOpenSession);
        Assert.Empty(recent.Sales);
    }

    [Fact]
    public async Task RecentSalesStillListsWhileTheDrawerIsBeingCounted()
    {
        var seeded = await _database.SeedBillAsync();
        await CashPaymentAsync(seeded.BillId, 100m);
        var sessionId = await OpenSessionAsync(_cashierId);
        await CloseBillAsync(seeded.BillId, DateTimeOffset.UtcNow.AddSeconds(5), _terminalId);
        await Execute("UPDATE cash.cash_sessions SET status = 'Counting' WHERE cash_session_id = @p", sessionId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var recent = await RecentSalesAsync(client);

        Assert.True(recent.HasOpenSession);
        Assert.Single(recent.Sales);
    }

    [Fact]
    public async Task RecentSalesNeedsThePermissionToRequestARefund()
    {
        var (_, noPermissionCookie) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "waiter");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(RecentSalesRequest(noPermissionCookie));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task CloseBillAsync(Guid billId, DateTimeOffset closedAt, Guid terminalId) => Execute(
        "UPDATE billing.bills SET status = 'Paid', closed_at = @at, terminal_id = @t WHERE bill_id = @p",
        billId, ("at", closedAt), ("t", terminalId));

    private HttpRequestMessage RecentSalesRequest(string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{_terminalId:D}/billing/recent-sales");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<RecentSalesV1> RecentSalesAsync(HttpClient client)
    {
        using var response = await client.SendAsync(RecentSalesRequest(_cashierCookie));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RecentSalesV1>())!;
    }
}
