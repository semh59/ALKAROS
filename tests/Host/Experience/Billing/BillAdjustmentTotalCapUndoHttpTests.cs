using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>
/// What the per-bill comp and discount cap counts, over the real endpoints and a real PostgreSQL: an undone discount
/// drops out of the total, and an item a waiter comped on their own is part of it.
/// </summary>
[Collection("Billing split PostgreSQL HTTP")]
public sealed class BillAdjustmentTotalCapUndoHttpTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AnUndoneDiscountNoLongerCountsTowardTheCapAndTheCashierCanCompAgain()
    {
        var terminalId = Guid.NewGuid();
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.discount", "bills.split");
        var (userId, cashier) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 50m, 5);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var discount = await SendDiscountAsync(client, terminalId, seeded.BillId, supervisor, 50m);

        using var blocked = await SendCompAsync(client, terminalId, seeded.BillId, cashier, 50m);
        using var undo = await SendUndoAsync(client, terminalId, seeded.BillId, await FirstAdjustmentIdAsync(seeded.BillId), supervisor);
        using var again = await SendCompAsync(client, terminalId, seeded.BillId, cashier, 50m);

        Assert.Equal(HttpStatusCode.OK, discount.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, undo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task AnItemTheWaiterCompedCountsTowardTheCapOfItsBill()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cashier) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 120m, 5);
        var comped = await _database.SeedBillAsync();
        var clean = await _database.SeedBillAsync();
        await CompItemAsync(comped.SecondItemId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var onCompedBill = await SendCompAsync(client, terminalId, comped.BillId, cashier, 110m);
        using var onCleanBill = await SendCompAsync(client, terminalId, clean.BillId, cashier, 110m);

        Assert.Equal(HttpStatusCode.Accepted, onCompedBill.StatusCode);
        Assert.Equal(HttpStatusCode.OK, onCleanBill.StatusCode);
    }

    [Fact]
    public async Task AnItemOtherThanACompIsNotCounted()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cashier) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 120m, 5);
        var seeded = await _database.SeedBillAsync();
        await using (var source = NpgsqlDataSource.Create(_database.ConnectionString))
        await using (var command = source.CreateCommand(
            "UPDATE billing.bill_items SET discount_amount = 60, gross_amount = gross_amount - 60, net_amount = net_amount - 60 WHERE bill_item_id = @id;"))
        {
            command.Parameters.AddWithValue("id", seeded.FirstItemId);
            await command.ExecuteNonQueryAsync();
        }

        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var comp = await SendCompAsync(client, terminalId, seeded.BillId, cashier, 110m);

        Assert.Equal(HttpStatusCode.OK, comp.StatusCode);
    }

    [Fact]
    public async Task ACompedItemCountsAtItsFullValueSoTheCapIsReachedExactlyAndNotPassed()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cashier) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 120m, 5);
        var reaching = await _database.SeedBillAsync();
        var passing = await _database.SeedBillAsync();
        var itemValue = await ItemValueAsync(reaching.SecondItemId);
        await CompItemAsync(reaching.SecondItemId);
        await CompItemAsync(passing.SecondItemId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var exactly = await SendCompAsync(client, terminalId, reaching.BillId, cashier, 150m - itemValue);
        using var pastByAKurus = await SendCompAsync(client, terminalId, passing.BillId, cashier, 150m - itemValue + 0.01m);

        Assert.Equal(HttpStatusCode.OK, exactly.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, pastByAKurus.StatusCode);
    }

    private async Task<decimal> ItemValueAsync(Guid billItemId)
    {
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var command = source.CreateCommand("SELECT gross_amount FROM billing.bill_items WHERE bill_item_id = @id;");
        command.Parameters.AddWithValue("id", billItemId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private async Task CompItemAsync(Guid billItemId)
    {
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var command = source.CreateCommand(
            """
            UPDATE billing.bill_items
            SET line_type = 'Complimentary', discount_amount = gross_amount, gross_amount = 0, net_amount = 0, tax_amount = 0
            WHERE bill_item_id = @id;
            """);
        command.Parameters.AddWithValue("id", billItemId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> FirstAdjustmentIdAsync(Guid billId)
    {
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var command = source.CreateCommand(
            "SELECT bill_adjustment_id FROM billing.bill_adjustments WHERE bill_id = @id ORDER BY created_at LIMIT 1;");
        command.Parameters.AddWithValue("id", billId);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<HttpResponseMessage> SendCompAsync(
        HttpClient client, Guid terminalId, Guid billId, string cookie, decimal amount)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/comp")
        {
            Content = JsonContent.Create(new ApplyBillCompRequestV1(Guid.NewGuid().ToString(), amount, "CustomerSatisfaction")),
        };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendDiscountAsync(
        HttpClient client, Guid terminalId, Guid billId, string cookie, decimal percent)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/discount")
        {
            Content = JsonContent.Create(new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", percent, "PromotionalOffer")),
        };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendUndoAsync(
        HttpClient client, Guid terminalId, Guid billId, Guid adjustmentId, string cookie)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/adjustments/{adjustmentId:D}/undo");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        app.MapBillingSplitApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        return client;
    }
}
