using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Billing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

[Collection("Billing split PostgreSQL HTTP")]
public sealed class BillCancellationHttpTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task CancellingARecreatedBillRecordsTheReasonFreesTheOrderAndAuditsIt()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(
            terminalId, "cashier", "bills.split", "bills.cancel");
        var seeded = await _database.SeedBillAsync();
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await Scalar(source,
            "UPDATE table_mgmt.tables SET current_bill_id = @id WHERE table_id = (SELECT table_id FROM billing.bills WHERE bill_id = @id);",
            ("id", seeded.BillId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var cancelled = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, "Müşteri vazgeçti"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        await using (var read = source.CreateCommand(
            """
            SELECT b.status, b.cancellation_reason, b.cancelled_by,
                   (SELECT count(*) FROM billing.bill_items i WHERE i.bill_id = b.bill_id AND i.superseded_at IS NULL),
                   (SELECT t.current_bill_id FROM table_mgmt.tables t WHERE t.table_id = b.table_id),
                   (SELECT count(*) FROM audit.audit_events a WHERE a.aggregate_id = b.bill_id
                       AND a.event_name = 'bill.cancelled' AND a.actor_id = @user AND a.reason = 'Müşteri vazgeçti')
            FROM billing.bills b WHERE b.bill_id = @id;
            """))
        {
            read.Parameters.AddWithValue("id", seeded.BillId);
            read.Parameters.AddWithValue("user", userId);
            await using var reader = await read.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Cancelled", reader.GetString(0));
            Assert.Equal("Müşteri vazgeçti", reader.GetString(1));
            Assert.Equal(userId, reader.GetGuid(2));
            Assert.Equal(0L, reader.GetInt64(3));
            Assert.True(reader.IsDBNull(4));
            Assert.Equal(1L, reader.GetInt64(5));
        }

        using var again = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, "Tekrar"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("UNSUPPORTED_BILL_STATE", await ErrorCode(again));
    }

    [Fact]
    public async Task ABillCanBeOpenedAgainForTheSameOrderAfterItIsCancelled()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(
            terminalId, "cashier", "bills.split", "bills.cancel");
        var orderId = await _database.SeedOrderWithoutBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var fromOrder = $"/api/v1/terminals/{terminalId:D}/billing/bills/from-order/{orderId:D}";

        var first = await PostAsync<BillSplitDesignDto>(client, fromOrder, cookie);
        using (var cancelled = await client.SendAsync(Cancel(terminalId, first.BillId, cookie, "Yanlış hesap")))
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var second = await PostAsync<BillSplitDesignDto>(client, fromOrder, cookie);

        Assert.NotEqual(first.BillId, second.BillId);
        Assert.Equal(2L, await _database.BillCountForOrderAsync(orderId));
    }

    [Fact]
    public async Task RecallCancellationAlsoLetsTheOrderBeBilledAgain()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split");
        var orderId = await _database.SeedOrderWithoutBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var fromOrder = $"/api/v1/terminals/{terminalId:D}/billing/bills/from-order/{orderId:D}";
        await PostAsync<BillSplitDesignDto>(client, fromOrder, cookie);

        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using (var connection = await source.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var cancelled = await new PostgresBillRepository(source)
                .CancelActiveBillsForOrderAsync(orderId, DateTimeOffset.UtcNow, connection, transaction);
            Assert.Equal(1, cancelled);
            await transaction.CommitAsync();
        }

        var recreated = await PostAsync<BillSplitDesignDto>(client, fromOrder, cookie);
        Assert.Equal(2L, await _database.BillCountForOrderAsync(orderId));
        Assert.NotEqual(Guid.Empty, recreated.BillId);
    }

    [Fact]
    public async Task ABillWithMoneyOnItIsRefusedAndStaysOpen()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(
            terminalId, "cashier", "bills.split", "bills.cancel");
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, userId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var refused = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, "Deneme"));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("BILL_HAS_PAYMENT", await ErrorCode(refused));
        Assert.NotEqual("Cancelled", await StatusAsync(seeded.BillId));
    }

    [Fact]
    public async Task APaidBillIsRefusedWithThePaymentMessage()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await _database.SeedBillAsync();
        await _database.SetBillStatusAsync(seeded.BillId, "Paid");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var refused = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, "Deneme"));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("BILL_HAS_PAYMENT", await ErrorCode(refused));
        Assert.Equal("Paid", await StatusAsync(seeded.BillId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMissingReasonIsRefusedBeforeAnythingChanges(string? reason)
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var refused = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, reason));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("REASON_REQUIRED", await ErrorCode(refused));
        Assert.NotEqual("Cancelled", await StatusAsync(seeded.BillId));
    }

    [Fact]
    public async Task AReasonLongerThanFiveHundredCharactersIsRefused()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var tooLong = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, new string('x', 501)));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("REASON_REQUIRED", await ErrorCode(tooLong));

        using var limit = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, new string('x', 500)));
        Assert.Equal(HttpStatusCode.OK, limit.StatusCode);
    }

    [Fact]
    public async Task ACancelledBillNoLongerCountsItsOrderItemsAsBilled()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await _database.SeedBillAsync();
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        var repository = new PostgresBillRepository(source);
        var bill = (await repository.GetByIdAsync(seeded.BillId))!;
        var orderItemIds = bill.Items.Select(item => item.OrderItemId).ToArray();
        Assert.Equal(orderItemIds.Length, (await repository.GetBilledOrderItemIdsAsync(orderItemIds)).Count);
        Assert.True(await repository.IsOrderItemBilledAsync(orderItemIds[0]));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var cancelled = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, "Yanlış masa"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        Assert.Empty(await repository.GetBilledOrderItemIdsAsync(orderItemIds));
        Assert.False(await repository.IsOrderItemBilledAsync(orderItemIds[0]));
    }

    [Fact]
    public async Task ARoleWithoutTheCancelPermissionIsRefused()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter", "bills.split");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var denied = await client.SendAsync(Cancel(terminalId, seeded.BillId, cookie, "Deneme"));

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.NotEqual("Cancelled", await StatusAsync(seeded.BillId));
    }

    [Fact]
    public async Task TheShippedCashierRoleHoldsTheCancelPermission()
    {
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var read = source.CreateCommand(
            """
            SELECT r.code FROM identity.role_permissions rp
            JOIN identity.roles r ON r.role_id = rp.role_id
            JOIN identity.permissions p ON p.permission_id = rp.permission_id
            WHERE p.code = 'bills.cancel' AND r.code IN ('cashier', 'supervisor', 'manager', 'waiter');
            """);
        var roles = new List<string>();
        await using var reader = await read.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            roles.Add(reader.GetString(0));
        Assert.Equal(["cashier", "manager", "supervisor"], roles.Order().ToArray());
    }

    private async Task<string> StatusAsync(Guid billId)
    {
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var read = source.CreateCommand("SELECT status FROM billing.bills WHERE bill_id = @id;");
        read.Parameters.AddWithValue("id", billId);
        return (string)(await read.ExecuteScalarAsync())!;
    }

    private static async Task Scalar(NpgsqlDataSource source, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private static HttpRequestMessage Cancel(Guid terminalId, Guid billId, string cookie, string? reason)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/cancel");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = JsonContent.Create(new CancelBillRequestV1(reason));
        return request;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
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
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
