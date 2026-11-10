using System.Net;
using System.Net.Http.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.Comp.Tests;

/// <summary>
/// An item comped while its bill is already open: the bill line has to follow, otherwise the guest still pays for it and
/// the comp is invisible to everything that reads the bill.
/// </summary>
[Collection("Order comp PostgreSQL HTTP")]
public sealed class OrderCompOnOpenBillHttpTests : IAsyncLifetime
{
    private readonly OrderManagementCompTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Theory]
    [InlineData(BillState.Open)]
    [InlineData(BillState.Reopened)]
    public async Task AnItemCompedWhileItsBillIsOpenIsCompedOnTheBillLineAndTheBillPaysLess(BillState billState)
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await _database.SeedBillForItemAsync(orderId, itemId, billState);
        var before = await BillLineAsync(itemId);
        var payableBefore = await PayableAsync(itemId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await CompAsync(client, terminalId, orderId, itemId, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await BillLineAsync(itemId);
        Assert.Equal("Sale", before.LineType);
        Assert.Equal("Complimentary", after.LineType);
        Assert.Equal(before.Gross, after.Discount);
        Assert.Equal(0m, after.Gross);
        Assert.True(payableBefore > 0m);
        Assert.Equal(0m, await PayableAsync(itemId));
    }

    [Fact]
    public async Task ACompedItemKeepsTheIdOfItsBillLine()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await _database.SeedBillForItemAsync(orderId, itemId, BillState.Open);
        var lineBefore = await BillLineIdAsync(itemId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await CompAsync(client, terminalId, orderId, itemId, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(lineBefore, await BillLineIdAsync(itemId));
    }

    [Fact]
    public async Task AnItemCompIsRefusedWhenTheDiscountOnTheBillWouldNoLongerFit()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await _database.SeedBillForItemAsync(orderId, itemId, BillState.Open);
        await using (var insert = _database.DataSource.CreateCommand(
            """
            INSERT INTO billing.bill_adjustments (bill_adjustment_id, bill_id, adjustment_type, calculation_type, amount, net_amount, gross_amount, is_deduction, reason, authorized_by, created_at)
            SELECT gen_random_uuid(), bill_id, 'DiscountAmount', 'FixedAmount', 1, 1, 1, true, 'PromotionalOffer', @user, now()
            FROM billing.bill_items WHERE order_item_id = @id;
            """))
        {
            insert.Parameters.AddWithValue("id", itemId);
            insert.Parameters.AddWithValue("user", userId);
            await insert.ExecuteNonQueryAsync();
        }

        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await CompAsync(client, terminalId, orderId, itemId, cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("DISCOUNT_EXCEEDS_PAYABLE", await response.Content.ReadAsStringAsync());
        Assert.Equal("Sale", (await BillLineAsync(itemId)).LineType);
        Assert.Equal(0m, await _database.DiscountOfOrderItemAsync(itemId));
    }

    private async Task<Guid> BillLineIdAsync(Guid orderItemId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT bill_item_id FROM billing.bill_items WHERE order_item_id = @id;");
        command.Parameters.AddWithValue("id", orderItemId);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<HttpResponseMessage> CompAsync(
        HttpClient client, Guid terminalId, Guid orderId, Guid itemId, string cookie)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items/{itemId:D}/comp")
        {
            Content = JsonContent.Create(new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "VIPGuest")),
        };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private async Task<decimal> PayableAsync(Guid orderItemId)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            SELECT b.payable_amount FROM billing.bills b
            JOIN billing.bill_items i ON i.bill_id = b.bill_id WHERE i.order_item_id = @id;
            """);
        command.Parameters.AddWithValue("id", orderItemId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private async Task<(string LineType, decimal Discount, decimal Gross)> BillLineAsync(Guid orderItemId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT line_type, discount_amount, gross_amount FROM billing.bill_items WHERE order_item_id = @id;");
        command.Parameters.AddWithValue("id", orderItemId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2));
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
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
