using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>A line the guest already had is added to an open check by the cashier: no kitchen ticket, stock consumed, who and why kept.</summary>
public sealed class KitchenlessAddHttpTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();
    private NpgsqlDataSource _source = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _source = NpgsqlDataSource.Create(_database.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task TheLineIsStoredActiveWithItsUserAndReasonConsumesStockOpensNoTicketAndIsAudited()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "waiter-forgot", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var itemId = body.RootElement.GetProperty("itemId").GetGuid();
        Assert.Equal(
            1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_item_id = @id AND status = 'Active' AND kitchen_state = 'Served' AND without_kitchen_reason_code = 'waiter-forgot' AND added_without_kitchen_by = '" + userId + "';", itemId));
        Assert.Equal(4m, await OnHandAsync(seeded.StockItemId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM kitchen.kitchen_ticket_items WHERE order_item_id = @id;", itemId));
        Assert.Equal(
            1L, await Count($"SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @id AND event_name = 'order.item-added-without-kitchen' AND actor_id = '{userId}' AND reason = 'waiter-forgot';", seeded.OrderId));
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task WithoutThePermissionTheCallIsForbiddenAndNothingIsAdded()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.send");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "waiter-forgot", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(5m, await OnHandAsync(seeded.StockItemId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("because")]
    public async Task AnUnknownOrMissingReasonIsRefusedAndNothingIsAdded(string? reason)
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, reason, seeded.RowVersion));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(5m, await OnHandAsync(seeded.StockItemId));
    }

    [Fact]
    public async Task ACheckThatAlreadyHasABillRefusesTheLine()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "other", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(5m, await OnHandAsync(seeded.StockItemId));
    }

    [Fact]
    public async Task ReplayingTheSameKeyAddsTheLineOnce()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var key = Guid.NewGuid().ToString("N");

        using var first = await client.SendAsync(Add(terminalId, seeded, cookie, "customer-asked", seeded.RowVersion, key));
        using var second = await client.SendAsync(Add(terminalId, seeded, cookie, "customer-asked", seeded.RowVersion, key));

        Assert.True(first.StatusCode == HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(4m, await OnHandAsync(seeded.StockItemId));
    }

    [Fact]
    public async Task AnUnknownReasonAnswersWithTheReasonRequiredCode()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "because", seeded.RowVersion));

        Assert.Equal("REASON_REQUIRED", await ErrorCode(response));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    [InlineData(0.5)]
    public async Task AQuantityOutsideTheRuleIsRefusedWithAClientErrorAndNothingIsAdded(double quantity)
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "other", seeded.RowVersion, quantity: quantity));

        Assert.InRange((int)response.StatusCode, 400, 499);
        Assert.Equal(1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(5m, await OnHandAsync(seeded.StockItemId));
    }

    [Fact]
    public async Task AProductWithoutAPriceIsNotFoundAndNothingIsAdded()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await Exec("UPDATE catalog.products SET current_price = NULL WHERE product_id = @id;", ("id", seeded.ProductId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "other", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task ADraftOrderBecomesSubmittedWhenItsFirstLineIsAddedWithoutAKitchen()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false, OrderState.Draft);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "other", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task ACancelledOrderRefusesTheLine()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false, OrderState.Cancelled);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "other", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task AMissingIdempotencyKeyIsRefusedAndNothingIsAdded()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "other", seeded.RowVersion, key: ""));

        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", await ErrorCode(response));
        Assert.Equal(1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task TheSameKeyOnAnotherOrderIsNotAnsweredWithTheFirstOrdersResult()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var first = await SeedAsync(withBill: false);
        var second = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var key = Guid.NewGuid().ToString("N");

        using var one = await client.SendAsync(Add(terminalId, first, cookie, "other", first.RowVersion, key));
        using var two = await client.SendAsync(Add(terminalId, second, cookie, "other", second.RowVersion, key));

        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(HttpStatusCode.OK, two.StatusCode);
        Assert.Equal(2L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", second.OrderId));
    }

    [Fact]
    public async Task AStaleRowVersionIsAConflictAndNothingIsAdded()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Add(terminalId, seeded, cookie, "other", seeded.RowVersion + 5));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(5m, await OnHandAsync(seeded.StockItemId));
    }

    [Fact]
    public async Task TheStoredLineCannotBeVoidedAsUnsentBecauseItIsAlreadyServed()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "orders.add-without-kitchen");
        var seeded = await SeedAsync(withBill: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var added = await client.SendAsync(Add(terminalId, seeded, cookie, "other", seeded.RowVersion));
        using var body = JsonDocument.Parse(await added.Content.ReadAsStringAsync());
        var itemId = body.RootElement.GetProperty("itemId").GetGuid();

        var order = await new PostgresOrderRepository(_source).GetByIdAsync(seeded.OrderId);
        var dto = OrderDtoAssembler.MapToDto(order!, "T1").Items.Single(i => i.ItemId == itemId);

        Assert.False(dto.CanVoid);
        Assert.False(dto.CanVoidSent);
    }

    [Fact]
    public void TheOrderViewShowsTheReasonOfAKitchenLessLineAndNothingForOrdinaryLines()
    {
        var orderId = Guid.NewGuid();
        var ordinary = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Dish", 1m, 100m, 10m);
        var added = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Dish", 1m, 100m, 10m).WithoutKitchen(Guid.NewGuid(), "customer-asked");

        var dto = OrderDtoAssembler.MapToDto(new Order(orderId, OrderSource.Cashier, "ORD-1", [ordinary, added]), "T1");

        Assert.Null(dto.Items.Single(i => i.ItemId == ordinary.Id).AddedWithoutKitchenReason);
        Assert.Equal("customer-asked", dto.Items.Single(i => i.ItemId == added.Id).AddedWithoutKitchenReason);
    }

    private sealed record Seeded(Guid OrderId, long RowVersion, Guid ProductId, Guid StockItemId);

    /// <summary>A submitted table order with one sent line and a stocked product (five on hand) the cashier can add.</summary>
    private async Task<Seeded> SeedAsync(bool withBill, OrderState status = OrderState.Submitted)
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await Exec(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table, @zone, @number, 2, 'Occupied');
            """,
            ("zone", zoneId), ("zone_code", "Z-" + zoneId.ToString("N")), ("table", tableId), ("number", "T-" + tableId.ToString("N")[..6]));

        var productId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var suffix = productId.ToString("N")[..8];
        await Exec(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product, @sku, 'Dish', 1, 1, 100);
            INSERT INTO inventory.stock_locations (id, code, name, location_type) VALUES (@location, @code, 'Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@item, @code, 'Stock item', 'Portion', 'adet', @location);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier) VALUES (@product, @item, 1);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance, @item, @location, 5, 0, 5);
            """,
            ("product", productId), ("sku", "SKU-" + suffix), ("location", locationId), ("item", stockItemId), ("code", "KA-" + suffix),
            ("balance", Guid.NewGuid()));

        var orderId = Guid.NewGuid();
        var sent = new OrderItem(
            Guid.NewGuid(), orderId, productId, "Dish", 1m, 100m, 10m, status: OrderItemState.Active, kitchenState: KitchenState.Sent);
        var order = new Order(orderId, OrderSource.Cashier, "ORD-" + orderId.ToString("N"), [sent], tableId, status: status,
            servingUserId: Guid.NewGuid());
        await new PostgresOrderRepository(_source).AddAsync(order);

        var billId = Guid.Empty;
        if (withBill)
        {
            var bill = Bill.FromOrder(Guid.NewGuid(), "BIL-" + orderId.ToString("N"), order);
            await new PostgresBillRepository(_source).AddAsync(bill);
            billId = bill.Id;
        }

        await Exec(
            "UPDATE table_mgmt.tables SET current_order_id = @order, current_bill_id = @bill WHERE table_id = @table;",
            ("order", orderId), ("bill", withBill ? billId : DBNull.Value), ("table", tableId));
        var stored = await new PostgresOrderRepository(_source).GetByIdAsync(orderId);
        return new Seeded(orderId, stored!.RowVersion, productId, stockItemId);
    }

    private async Task<decimal> OnHandAsync(Guid stockItemId)
        => (decimal)(await Scalar("SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @id;", stockItemId))!;

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private async Task<object?> Scalar(string sql, Guid id)
    {
        await using var command = _source.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync();
    }

    private async Task<string> Text(string sql, Guid id) => (string)(await Scalar(sql, id))!;

    private async Task<long> Count(string sql, Guid id) => (long)(await Scalar(sql, id))!;

    private async Task Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static HttpRequestMessage Add(Guid terminalId, Seeded seeded, string cookie, string? reason, long rowVersion, string? key = null, double quantity = 1)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{seeded.OrderId:D}/items/without-kitchen");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = JsonContent.Create(new
        {
            idempotencyKey = key ?? Guid.NewGuid().ToString("N"),
            expectedRowVersion = rowVersion,
            productId = seeded.ProductId,
            quantity,
            reasonCode = reason,
        });
        return request;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        app.MapOrderManagementApi();
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
