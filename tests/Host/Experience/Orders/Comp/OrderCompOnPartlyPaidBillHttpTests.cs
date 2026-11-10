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
/// An item comp lowers what a bill is worth, so it must not take the payable below what was already collected: the
/// cashier refunds the payment first.
/// </summary>
[Collection("Order comp PostgreSQL HTTP")]
public sealed class OrderCompOnPartlyPaidBillHttpTests : IAsyncLifetime
{
    private readonly OrderManagementCompTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public async Task ACompThatWouldTakeThePayableBelowWhatWasCollectedIsRefusedAndNothingChanges(int collected, int refunded)
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await _database.SeedBillForItemAsync(orderId, itemId, BillState.Open);
        await CollectAsync(itemId, userId, collected, refunded);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await CompAsync(client, terminalId, orderId, itemId, cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("COMP_BELOW_COLLECTED", body);
        Assert.Contains("iade", body);
        Assert.Equal("Sale", await LineTypeAsync(itemId));
        Assert.Equal(0m, await _database.DiscountOfOrderItemAsync(itemId));
    }

    [Fact]
    public async Task ACompIsAllowedOnceTheCollectedMoneyHasBeenRefunded()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await _database.SeedBillForItemAsync(orderId, itemId, BillState.Open);
        await CollectAsync(itemId, userId, 1, 1);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await CompAsync(client, terminalId, orderId, itemId, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Complimentary", await LineTypeAsync(itemId));
    }

    [Fact]
    public async Task ACompIsRefusedWhileAPaymentOfTheBillIsStillPending()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await _database.SeedBillForItemAsync(orderId, itemId, BillState.Open);
        await using (var insert = _database.DataSource.CreateCommand(
            """
            INSERT INTO payments.payments (payment_id, bill_id, status, requested_amount, initiated_at, created_at, updated_at)
            SELECT gen_random_uuid(), bill_id, 'Pending', 1, now(), now(), now() FROM billing.bill_items WHERE order_item_id = @item;
            """))
        {
            insert.Parameters.AddWithValue("item", itemId);
            await insert.ExecuteNonQueryAsync();
        }

        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await CompAsync(client, terminalId, orderId, itemId, cookie);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("COMP_PAYMENT_IN_FLIGHT", await response.Content.ReadAsStringAsync());
        Assert.Equal("Sale", await LineTypeAsync(itemId));
    }

    /// <summary>Collects <paramref name="collected"/> lira on the bill of the item, of which <paramref name="refunded"/> is paid back.</summary>
    private async Task CollectAsync(Guid orderItemId, Guid userId, int collected, int refunded)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            WITH bill AS (SELECT bill_id FROM billing.bill_items WHERE order_item_id = @item),
            payment AS (
                INSERT INTO payments.payments (payment_id, bill_id, status, requested_amount, approved_amount, initiated_at, approved_at, created_at, updated_at)
                SELECT @payment, bill_id, 'Approved', @collected, @collected, now(), now(), now(), now() FROM bill RETURNING payment_id, bill_id),
            allocation AS (
                INSERT INTO payments.payment_allocations (payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at)
                SELECT gen_random_uuid(), payment_id, bill_id, @collected, 'TRY', @key, now() FROM payment RETURNING 1)
            INSERT INTO payments.payment_reversals (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            SELECT gen_random_uuid(), payment_id, 'FullRefund', @refunded, 'Refund', @user, @key || 'r', now() FROM payment WHERE @refunded > 0;
            """);
        command.Parameters.AddWithValue("item", orderItemId);
        command.Parameters.AddWithValue("payment", Guid.NewGuid());
        command.Parameters.AddWithValue("key", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("collected", (decimal)collected);
        command.Parameters.AddWithValue("refunded", (decimal)refunded);
        command.Parameters.AddWithValue("user", userId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> LineTypeAsync(Guid orderItemId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT line_type FROM billing.bill_items WHERE order_item_id = @id;");
        command.Parameters.AddWithValue("id", orderItemId);
        return (string)(await command.ExecuteScalarAsync())!;
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
