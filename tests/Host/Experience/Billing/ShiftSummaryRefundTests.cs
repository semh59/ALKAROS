using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

[Collection("Billing split PostgreSQL HTTP")]
public sealed class ShiftSummaryRefundTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ARefundedOrderIsOutOfTheWaitersSalesTheTipPoolAndTheWaiterCount()
    {
        var terminalId = Guid.NewGuid();
        var (keptWaiterId, keptCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var keptBill = await _database.SeedBillAsync();
        await _database.SetOrderServingUserAsync(keptBill.BillId, keptWaiterId);

        var (refundedWaiterId, refundedCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var refundedBill = await _database.SeedBillAsync();
        await _database.SetOrderServingUserAsync(refundedBill.BillId, refundedWaiterId);
        var (_, cashierCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await Tip(client, terminalId, keptBill.BillId, cashierCookie, 40m);
        await Tip(client, terminalId, refundedBill.BillId, cashierCookie, 60m);
        await RefundAwayAsync(refundedBill.BillId, refundedWaiterId);

        var kept = await SummaryAsync(client, terminalId, keptCookie);
        var refunded = await SummaryAsync(client, terminalId, refundedCookie);

        Assert.True(kept.SalesTotal > 0m);
        Assert.Equal(0m, refunded.SalesTotal);
        Assert.Equal(40m, kept.TipPoolTotal);
        Assert.Equal(1, kept.WaitersWorkedToday);
        Assert.Equal(40m, kept.TipPoolShare);
    }

    [Fact]
    public async Task AnOrderWhoseBillIsStillOpenAfterOnePaymentWasRefundedStillCounts()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, waiterCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var bill = await _database.SeedBillAsync();
        await _database.SetOrderServingUserAsync(bill.BillId, waiterId);
        await _database.SeedCollectedAsync(bill.BillId, 100m, waiterId);
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using (var refund = source.CreateCommand(
            "UPDATE payments.payments SET status = 'Refunded' WHERE bill_id = @b;"))
        {
            refund.Parameters.AddWithValue("b", bill.BillId);
            await refund.ExecuteNonQueryAsync();
        }
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var summary = await SummaryAsync(client, terminalId, waiterCookie);

        Assert.True(summary.SalesTotal > 0m);
        Assert.Equal(1, summary.WaitersWorkedToday);
    }

    [Fact]
    public async Task AnOrderBilledAgainAndPaidAfterARefundStillCounts()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, waiterCookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter");
        var bill = await _database.SeedBillAsync();
        await _database.SetOrderServingUserAsync(bill.BillId, waiterId);
        await RefundAwayAsync(bill.BillId, waiterId);
        await using (var source = NpgsqlDataSource.Create(_database.ConnectionString))
        await using (var again = source.CreateCommand(
            """
            INSERT INTO billing.bills (bill_id, bill_number, order_id, status, payable_amount, opened_at, created_at, updated_at)
            SELECT gen_random_uuid(), 'AGAIN-' || substr(bill_id::text, 1, 8), order_id, 'Paid', 100, now(), now(), now()
            FROM billing.bills WHERE bill_id = @b;
            """))
        {
            again.Parameters.AddWithValue("b", bill.BillId);
            await again.ExecuteNonQueryAsync();
        }
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var summary = await SummaryAsync(client, terminalId, waiterCookie);

        Assert.True(summary.SalesTotal > 0m);
    }

    private async Task RefundAwayAsync(Guid billId, Guid userId)
    {
        await _database.SeedCollectedAsync(billId, 100m, userId);
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var command = source.CreateCommand(
            """
            UPDATE payments.payments SET status = 'Refunded' WHERE bill_id = @b;
            UPDATE billing.bills SET status = 'Cancelled', cancelled_at = now() WHERE bill_id = @b;
            """);
        command.Parameters.AddWithValue("b", billId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task Tip(HttpClient client, Guid terminalId, Guid billId, string cookie, decimal amount)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/tip");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = JsonContent.Create(new ApplyBillTipRequestV1(Guid.NewGuid().ToString(), amount));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<MyShiftSummaryV1> SummaryAsync(HttpClient client, Guid terminalId, string cookie)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/terminals/{terminalId:D}/orders/my-shift-summary");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MyShiftSummaryV1>())!;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddBillingSplitExperience();
        builder.Services.AddOrderManagementExperience();
        var app = builder.Build();
        app.MapBillingSplitApi();
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
