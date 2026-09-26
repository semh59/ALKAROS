using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.Confirmation.Tests;

/// <summary>
/// V12-QRO-003: confirming a pending QR order claims its portions through the cross-channel
/// arbiter only on a successful accept, atomically with the Accepted write and the table move;
/// a refusal, a stock loss or a lost race leaves no hold and no half-moved table.
/// </summary>
[Collection("Order confirmation PostgreSQL HTTP")]
public sealed class QrConfirmationReservationHttpTests : IAsyncLifetime
{
    private readonly OrderManagementConfirmationTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AcceptingAQrOrderClaimsItsPortionsAndOccupiesTheTableTogether()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, tableId, productId, submissionId) = await _database.SeedQrPendingOrderAsync(stockOnHandQuantity: 2m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderState.Accepted, (await _database.ReloadOrderAsync(orderId)).Status);
        Assert.Equal(("Occupied", (Guid?)orderId), await _database.GetTableStateAsync(tableId));
        // The QR hold was taken and, in the same transaction, became the consumption.
        var hold = Assert.Single(await _database.GetHoldsForOrderAsync(orderId));
        Assert.Equal(("Consumed", "Qr", submissionId.ToString("D")), hold);
        Assert.Equal(1m, await _database.GetOnHandQuantityForProductAsync(productId));
        Assert.Equal(0m, (await _database.GetHoldStateForProductAsync(productId)).Reserved);
    }

    [Fact]
    public async Task AQrOrderLosingTheLastPortionToAnotherChannelLeavesNothingBehind()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, tableId, productId, _) = await _database.SeedQrPendingOrderAsync(stockOnHandQuantity: 1m);
        Assert.Equal(
            CrossChannelReservationOutcome.Reserved,
            await _database.HoldAsync(Guid.NewGuid(), Guid.NewGuid(), productId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INSUFFICIENT_STOCK", body);
        Assert.Contains("Son Porsiyon Mantı", body);
        Assert.Equal(OrderState.PendingConfirmation, (await _database.ReloadOrderAsync(orderId)).Status);
        Assert.Equal(("Reserved", (Guid?)orderId), await _database.GetTableStateAsync(tableId));
        Assert.Empty(await _database.GetHoldsForOrderAsync(orderId));
        Assert.Equal(1m, await _database.GetOnHandQuantityForProductAsync(productId));
    }

    [Fact]
    public async Task AQrOrderForAnUnmappedProductIsRefusedWithoutAnyHold()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, tableId, _, _) = await _database.SeedQrPendingOrderAsync(stockOnHandQuantity: 0m, seedStockMapping: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("PRODUCT_STOCK_NOT_CONFIGURED", await response.Content.ReadAsStringAsync());
        Assert.Equal(("Reserved", (Guid?)orderId), await _database.GetTableStateAsync(tableId));
        Assert.Empty(await _database.GetHoldsForOrderAsync(orderId));
    }

    [Fact]
    public async Task TwoStaffAcceptingTheSameQrOrderAtOnceHaveOneWinnerAndOneStockEffect()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, tableId, productId, _) = await _database.SeedQrPendingOrderAsync(stockOnHandQuantity: 5m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(0, 2).Select(async _ =>
        {
            await start.Task;
            using var response = await client.SendAsync(JsonRequest(
                AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));
            return response.StatusCode;
        }).ToList();
        start.SetResult();
        var statuses = await Task.WhenAll(attempts);

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Conflict));
        Assert.Equal(4m, await _database.GetOnHandQuantityForProductAsync(productId));
        Assert.Single(await _database.GetHoldsForOrderAsync(orderId));
        Assert.Equal(("Occupied", (Guid?)orderId), await _database.GetTableStateAsync(tableId));
    }

    [Fact]
    public async Task AcceptingAQrOrderLocksEveryStockRowInTheGlobalOrderBeforeTheHoldTakesAny()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, productStock, modifierStock) = await _database.SeedQrPendingOrderWithModifierStockAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        // Another transaction holds the modifier's row, which sorts first.
        await using var blocker = await _database.DataSource.OpenConnectionAsync();
        await using var blockerTransaction = await blocker.BeginTransactionAsync();
        await LockRowAsync(blocker, blockerTransaction, modifierStock);

        var accept = client.SendAsync(JsonRequest(AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));
        await Task.Delay(700);

        // The acceptance waits on the first row of the order and has not taken the product's row out of turn.
        bool productRowFree;
        await using (var probe = await _database.DataSource.OpenConnectionAsync())
        await using (var probeTransaction = await probe.BeginTransactionAsync())
        {
            await using var tryLock = new Npgsql.NpgsqlCommand("SELECT pg_try_advisory_xact_lock(hashtext($1)::bigint);", probe, probeTransaction);
            tryLock.Parameters.AddWithValue($"{productStock.Item:N}:{productStock.Location:N}");
            productRowFree = (bool)(await tryLock.ExecuteScalarAsync())!;
            await probeTransaction.RollbackAsync();
        }

        await blockerTransaction.RollbackAsync();
        using var response = await accept;

        Assert.True(productRowFree, "the product row must not be locked before the modifier row");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task LockRowAsync(Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction, (Guid Item, Guid Location) row)
    {
        await using var command = new Npgsql.NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue($"{row.Item:N}:{row.Location:N}");
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task RejectingAQrOrderFreesTheTableAndNeverClaimsStock()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, tableId, productId, _) = await _database.SeedQrPendingOrderAsync(stockOnHandQuantity: 1m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            RejectPath(terminalId, orderId), cookie, new RejectPendingOrderRequestV1(1, "Masada kimse yok")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderState.Rejected, (await _database.ReloadOrderAsync(orderId)).Status);
        Assert.Equal(("Available", (Guid?)null), await _database.GetTableStateAsync(tableId));
        Assert.Empty(await _database.GetHoldsForOrderAsync(orderId));
        Assert.Equal(1m, await _database.GetOnHandQuantityForProductAsync(productId));
    }

    [Fact]
    public async Task ARejectionThatLosesToAConcurrentAcceptLeavesTheKitchenItemsUntouched()
    {
        // V1-RMD-313: the rejection passes its version check, then waits on the kitchen ticket while an accept of
        // the same order commits; the rejection's save then fails and must take its kitchen cancellation with it.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, _, productId, _) = await _database.SeedQrPendingOrderAsync(stockOnHandQuantity: 2m);
        var orderItemId = (await _database.ReloadOrderAsync(orderId)).Items[0].Id;
        await _database.SeedKitchenTicketAsync(orderId, orderItemId, productId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        await using var blocker = await _database.DataSource.OpenConnectionAsync();
        await using var blockerTransaction = await blocker.BeginTransactionAsync();
        await using (var lockTickets = new Npgsql.NpgsqlCommand(
            "SELECT id FROM kitchen.kitchen_tickets WHERE order_id = $1 FOR UPDATE;", blocker, blockerTransaction))
        {
            lockTickets.Parameters.AddWithValue(orderId);
            await lockTickets.ExecuteNonQueryAsync();
        }

        var reject = client.SendAsync(JsonRequest(RejectPath(terminalId, orderId), cookie, new RejectPendingOrderRequestV1(1, "Masada kimse yok")));
        await Task.Delay(500);
        using var accept = await client.SendAsync(JsonRequest(AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));
        await blockerTransaction.RollbackAsync();
        using var rejectResponse = await reject;

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, rejectResponse.StatusCode);
        Assert.Equal(OrderState.Accepted, (await _database.ReloadOrderAsync(orderId)).Status);
        Assert.DoesNotContain("Cancelled", await _database.GetKitchenTicketItemStatusesAsync(orderId));
    }

    [Fact]
    public async Task AStaleRowVersionRefusesTheQrAcceptWithoutAnyHold()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter", "orders.create");
        var (orderId, tableId, _, _) = await _database.SeedQrPendingOrderAsync(stockOnHandQuantity: 1m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(7, null)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("CONCURRENCY_CONFLICT", await response.Content.ReadAsStringAsync());
        Assert.Equal(("Reserved", (Guid?)orderId), await _database.GetTableStateAsync(tableId));
        Assert.Empty(await _database.GetHoldsForOrderAsync(orderId));
    }

    [Fact]
    public async Task AStaffSessionWithoutOrderPermissionCannotAcceptAQrOrder()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter");
        var (orderId, tableId, _, _) = await _database.SeedQrPendingOrderAsync(stockOnHandQuantity: 1m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AcceptPath(terminalId, orderId), cookie, new AcceptPendingOrderRequestV1(1, null)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(OrderState.PendingConfirmation, (await _database.ReloadOrderAsync(orderId)).Status);
        Assert.Equal(("Reserved", (Guid?)orderId), await _database.GetTableStateAsync(tableId));
        Assert.Empty(await _database.GetHoldsForOrderAsync(orderId));
    }

    private static string AcceptPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/accept";

    private static string RejectPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/reject";

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
