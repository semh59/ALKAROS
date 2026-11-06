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
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>Several open orders of one table are billed together on one bill (real host, real Postgres).</summary>
public sealed class MergeOrdersHttpTests : IAsyncLifetime
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
    public async Task TwoOrdersOfOneTableBecomeOneBillOfBothTheirItemsOwnedByTheCallingTerminal()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var merged = await MergeAsync(client, terminalId, cookie, "key-1", first, second);

        Assert.Equal(HttpStatusCode.OK, merged.Status);
        var billId = merged.Json.GetProperty("billId").GetGuid();
        Assert.Equal(2, merged.Json.GetProperty("items").GetArrayLength());
        Assert.Equal(1L, await Count("SELECT count(*) FROM billing.bills WHERE bill_id = @id AND order_id IS NULL AND terminal_id = @terminal;", ("id", billId), ("terminal", terminalId)));
        Assert.Equal(2L, await Count("SELECT count(DISTINCT oi.order_id) FROM billing.bill_items bi JOIN orders.order_items oi ON oi.order_item_id = bi.order_item_id WHERE bi.bill_id = @id;", ("id", billId)));
        Assert.Equal(1L, await Count("SELECT count(*) FROM table_mgmt.tables WHERE table_id = @id AND current_bill_id = @bill;", ("id", table), ("bill", billId)));
        var bills = new PostgresBillRepository(_source);
        Assert.Equal(billId, Assert.Single(await bills.GetByOrderIdAsync(first.Id)).Id);
        Assert.Equal(billId, Assert.Single(await bills.GetByOrderIdAsync(second.Id)).Id);
    }

    [Fact]
    public async Task ResendingTheSameKeyAndOrdersReturnsTheSameBillWhileAnotherSetOrACancelledBillRefusesTheKey()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        var third = await SeedOrderAsync(table, 20m);
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var original = await MergeAsync(client, terminalId, cookie, "key-replay", first, second);
        var replay = await MergeAsync(client, terminalId, cookie, "key-replay", second, first);
        var otherSet = await MergeAsync(client, terminalId, cookie, "key-replay", first, third);
        await Exec("UPDATE billing.bills SET status = 'Cancelled' WHERE bill_id = @id;", ("id", original.Json.GetProperty("billId").GetGuid()));
        var afterCancel = await MergeAsync(client, terminalId, cookie, "key-replay", first, second);

        Assert.Equal(original.Json.GetProperty("billId").GetGuid(), replay.Json.GetProperty("billId").GetGuid());
        Assert.Equal(1L, await Count("SELECT count(*) FROM billing.bills WHERE order_id IS NULL;"));
        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED"), (otherSet.Status, otherSet.Code));
        Assert.Equal((HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED"), (afterCancel.Status, afterCancel.Code));
    }

    [Fact]
    public async Task ARoleWithoutBillsSplitIsRefusedAndNothingIsCreated()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        var (terminalId, cookie) = await CashierAsync("payments.take");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var refused = await MergeAsync(client, terminalId, cookie, "key-denied", first, second);

        Assert.Equal((HttpStatusCode.Forbidden, "FORBIDDEN"), (refused.Status, refused.Code));
        Assert.Equal(0L, await Count("SELECT count(*) FROM billing.bills WHERE order_id IS NULL;"));
    }

    [Fact]
    public async Task EveryRefusedMergeNamesItsCauseInTurkishAndCreatesNothing()
    {
        var table = await SeedTableAsync();
        var otherTable = await SeedTableAsync();
        var a = await SeedOrderAsync(table, 10m);
        var b = await SeedOrderAsync(table, 10m);
        var elsewhere = await SeedOrderAsync(otherTable, 10m);
        var qr = await SeedOrderAsync(table, 10m, OrderSource.Qr);
        var draft = await SeedOrderAsync(table, 10m, status: OrderState.Draft);
        var cancelled = await SeedOrderAsync(table, 10m, status: OrderState.Cancelled);
        var billed = await SeedOrderAsync(table, 10m);
        await Exec(
            "INSERT INTO billing.bills (bill_id, bill_number, table_id, order_id, status, currency_code, opened_at, created_at, updated_at) VALUES (@bill, @number, @table, @order, 'Open', 'TRY', now(), now(), now());",
            ("bill", Guid.NewGuid()), ("number", "BIL-B-" + Guid.NewGuid().ToString("N")[..10]), ("table", table), ("order", billed.Id));
        var sentOne = await SeedOrderAsync(table, 10m);
        await Exec("INSERT INTO table_mgmt.table_sent_checks (order_id, table_id) VALUES (@order, @table);", ("order", sentOne.Id), ("table", table));
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var single = await MergeAsync(client, terminalId, cookie, "k-single", a);
        var twice = await MergeAsync(client, terminalId, cookie, "k-twice", a, a);
        var tables = await MergeAsync(client, terminalId, cookie, "k-tables", a, elsewhere);
        var source = await MergeAsync(client, terminalId, cookie, "k-source", a, qr);
        var missing = await MergeAsync(client, terminalId, cookie, "k-missing", a.Id, Guid.NewGuid());
        var draftState = await MergeAsync(client, terminalId, cookie, "k-draft", a, draft);
        var cancelledState = await MergeAsync(client, terminalId, cookie, "k-cancelled", a, cancelled);
        var alreadyBilled = await MergeAsync(client, terminalId, cookie, "k-billed", a, billed);
        var mixed = await MergeAsync(client, terminalId, cookie, "k-mixed", a, sentOne);
        var blankKey = await MergeAsync(client, terminalId, cookie, " ", a, b);

        Assert.Equal((HttpStatusCode.BadRequest, "MERGE_NEEDS_TWO_ORDERS"), (single.Status, single.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "MERGE_NEEDS_TWO_ORDERS"), (twice.Status, twice.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "MERGE_DIFFERENT_TABLES"), (tables.Status, tables.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "MERGE_ORDER_SOURCE"), (source.Status, source.Code));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), (missing.Status, missing.Code));
        Assert.Equal((HttpStatusCode.Conflict, "UNSUPPORTED_ORDER_STATE"), (draftState.Status, draftState.Code));
        Assert.Equal((HttpStatusCode.Conflict, "UNSUPPORTED_ORDER_STATE"), (cancelledState.Status, cancelledState.Code));
        Assert.Equal((HttpStatusCode.Conflict, "MERGE_ORDER_ALREADY_BILLED"), (alreadyBilled.Status, alreadyBilled.Code));
        Assert.Equal((HttpStatusCode.Conflict, "MERGE_MIXED_SENT"), (mixed.Status, mixed.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_REQUIRED"), (blankKey.Status, blankKey.Code));
        Assert.Equal("Yalnız aynı masadaki siparişler birleştirilebilir.", tables.Message);
        Assert.Equal(0L, await Count("SELECT count(*) FROM billing.bills WHERE order_id IS NULL;"));
    }

    [Fact]
    public async Task OrdersTheTillAlreadyHoldAreMergedAndAWaiterOrderIsAccepted()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m, OrderSource.Waiter);
        await Exec("INSERT INTO table_mgmt.table_sent_checks (order_id, table_id) VALUES (@a, @table), (@b, @table);", ("a", first.Id), ("b", second.Id), ("table", table));
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var merged = await MergeAsync(client, terminalId, cookie, "key-sent", first, second);

        Assert.Equal(HttpStatusCode.OK, merged.Status);
    }

    [Fact]
    public async Task AnotherTerminalWithTheSameKeyGetsItsOwnAnswerAndNeverTheFirstTerminalsBill()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        var (terminalA, cookieA) = await CashierAsync("bills.split");
        var (terminalB, cookieB) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var original = await MergeAsync(client, terminalA, cookieA, "shared-key", first, second);
        var other = await MergeAsync(client, terminalB, cookieB, "shared-key", first, second);

        Assert.Equal(HttpStatusCode.OK, original.Status);
        Assert.Equal((HttpStatusCode.Conflict, "MERGE_ORDER_ALREADY_BILLED"), (other.Status, other.Code));
        Assert.Equal(1L, await Count("SELECT count(*) FROM billing.bills WHERE order_id IS NULL;"));
    }

    [Fact]
    public async Task AnOrderWithoutAnyBillableItemAndAListOfTooManyOrdersAreRefused()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var empty = await SeedOrderAsync(table, 50m);
        await Exec("UPDATE orders.order_items SET status = 'Cancelled' WHERE order_id = @id;", ("id", empty.Id));
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var emptyOrder = await MergeAsync(client, terminalId, cookie, "key-empty", first, empty);
        var tooMany = await MergeAsync(client, terminalId, cookie, "key-many", Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToArray());

        Assert.Equal((HttpStatusCode.Conflict, "MERGE_ORDER_EMPTY"), (emptyOrder.Status, emptyOrder.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "MERGE_TOO_MANY_ORDERS"), (tooMany.Status, tooMany.Code));
        Assert.Equal(0L, await Count("SELECT count(*) FROM billing.bills WHERE order_id IS NULL;"));
    }

    [Fact]
    public async Task ARoundNotSentYetStaysOffTheMergedBillAndAnOrderOfOnlySuchARoundIsRefused()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        var unsent = await SeedOrderAsync(table, 30m);
        await Exec("UPDATE orders.order_items SET status = 'Draft' WHERE order_id = @id;", ("id", unsent.Id));
        await Exec(
            "INSERT INTO orders.order_items (order_item_id, order_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount, status, kitchen_state, portion_reservation_status, created_at, updated_at) " +
            "SELECT gen_random_uuid(), order_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount, 'Draft', kitchen_state, portion_reservation_status, now(), now() FROM orders.order_items WHERE order_id = @id LIMIT 1;",
            ("id", first.Id));
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var onlyUnsent = await MergeAsync(client, terminalId, cookie, "key-unsent", first, unsent);
        var merged = await MergeAsync(client, terminalId, cookie, "key-round", first, second);

        Assert.Equal((HttpStatusCode.Conflict, "MERGE_ORDER_EMPTY"), (onlyUnsent.Status, onlyUnsent.Code));
        Assert.Equal(HttpStatusCode.OK, merged.Status);
        Assert.Equal(2, merged.Json.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task TwoTerminalsMergingTheSameOrdersTogetherLeaveExactlyOneBill()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        var (terminalA, cookieA) = await CashierAsync("bills.split");
        var (terminalB, cookieB) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var results = await Task.WhenAll(
            MergeAsync(client, terminalA, cookieA, "key-race-a", first, second),
            MergeAsync(client, terminalB, cookieB, "key-race-b", first, second));

        Assert.Single(results, r => r.Status == HttpStatusCode.OK);
        Assert.Single(results, r => r.Status == HttpStatusCode.Conflict && r.Code == "MERGE_ORDER_ALREADY_BILLED");
        Assert.Equal(1L, await Count("SELECT count(*) FROM billing.bills WHERE order_id IS NULL;"));
    }

    [Theory]
    [InlineData("status = 'Cancelled'", "UNSUPPORTED_ORDER_STATE")]
    [InlineData("table_id = @other", "MERGE_DIFFERENT_TABLES")]
    public async Task AnOrderCancelledOrMovedWhileTheMergeWaitsForItsLockIsNotBilled(string change, string code)
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        Assert.Equal(HttpStatusCode.OK, (await MergeAsync(client, terminalId, cookie, "key-warm-up", await SeedOrderAsync(table, 10m), await SeedOrderAsync(table, 20m))).Status);

        await using var connection = await _source.OpenConnectionAsync();
        await using var cancelling = await connection.BeginTransactionAsync();
        await using (var cancel = new NpgsqlCommand($"UPDATE orders.orders SET {change} WHERE order_id = @id;", connection, cancelling))
        {
            cancel.Parameters.AddWithValue("id", first.Id);
            cancel.Parameters.AddWithValue("other", await SeedTableAsync());
            await cancel.ExecuteNonQueryAsync();
        }
        var merge = MergeAsync(client, terminalId, cookie, "key-cancelled", first, second);
        await WaitUntilARequestWaitsForAsync(connection);

        await cancelling.CommitAsync();
        var result = await merge;
        Assert.Equal(code, result.Code);
        Assert.True(result.Status is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        Assert.Equal(1L, await Count("SELECT count(*) FROM billing.bills WHERE order_id IS NULL;"));
        Assert.Equal(0L, await Count("SELECT count(*) FROM billing.bill_items WHERE order_item_id IN (SELECT order_item_id FROM orders.order_items WHERE order_id IN (@a, @b));", ("a", first.Id), ("b", second.Id)));
    }

    [Fact]
    public async Task AnItemCancelledWhileTheMergeWaitsForTheLockIsLeftOffTheBill()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        Assert.Equal(HttpStatusCode.OK, (await MergeAsync(client, terminalId, cookie, "key-warm-up", await SeedOrderAsync(table, 10m), await SeedOrderAsync(table, 20m))).Status);

        await using var connection = await _source.OpenConnectionAsync();
        await using var cancelling = await connection.BeginTransactionAsync();
        await using (var cancel = new NpgsqlCommand(
            "UPDATE orders.orders SET updated_at = now() WHERE order_id = @id; UPDATE orders.order_items SET status = 'Cancelled' WHERE order_id = @id;",
            connection, cancelling))
        {
            cancel.Parameters.AddWithValue("id", first.Id);
            await cancel.ExecuteNonQueryAsync();
        }
        var merge = MergeAsync(client, terminalId, cookie, "key-item-cancelled", first, second);
        await WaitUntilARequestWaitsForAsync(connection);

        await cancelling.CommitAsync();
        var result = await merge;
        Assert.Equal((HttpStatusCode.Conflict, "MERGE_ORDER_EMPTY"), (result.Status, result.Code));
        Assert.Equal(1L, await Count("SELECT count(*) FROM billing.bills WHERE order_id IS NULL;"));
    }

    private async Task WaitUntilARequestWaitsForAsync(NpgsqlConnection holder)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await Count("SELECT count(*) FROM pg_stat_activity WHERE @holder = ANY(pg_blocking_pids(pid));", ("holder", holder.ProcessID)) > 0)
                return;
            await Task.Delay(100);
        }

        Assert.Fail("The merge never waited for the order lock.");
    }

    [Fact]
    public async Task APaidMergedBillCompletesBothOrdersAndFreesTheTable()
    {
        var table = await SeedTableAsync();
        var first = await SeedOrderAsync(table, 100m);
        var second = await SeedOrderAsync(table, 50m);
        await Exec("UPDATE table_mgmt.tables SET current_order_id = @order WHERE table_id = @table;", ("order", first.Id), ("table", table));
        var (terminalId, cookie) = await CashierAsync("bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var merged = await MergeAsync(client, terminalId, cookie, "key-paid", first, second);
        await Exec(
            "UPDATE billing.bills SET status = 'Paid', closed_at = now() - interval '30 minutes' WHERE bill_id = @id;",
            ("id", merged.Json.GetProperty("billId").GetGuid()));

        var repaired = await app.Services.GetServices<IHostedService>().OfType<BillClosureSweeper>().Single()
            .RepairPaidBillOrdersAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Equal(2, repaired);
        Assert.Equal(2L, await Count("SELECT count(*) FROM orders.orders WHERE order_id IN (@a, @b) AND status = 'Completed';", ("a", first.Id), ("b", second.Id)));
        Assert.Equal("Available", await Text("SELECT current_status FROM table_mgmt.tables WHERE table_id = @id;", ("id", table)));
    }

    private sealed record MergeResult(HttpStatusCode Status, JsonElement Json, string Code, string Message);

    private static async Task<MergeResult> MergeAsync(HttpClient client, Guid terminalId, string cookie, string key, params Order[] orders)
        => await MergeAsync(client, terminalId, cookie, key, orders.Select(o => o.Id).ToArray());

    private static async Task<MergeResult> MergeAsync(HttpClient client, Guid terminalId, string cookie, string key, params Guid[] orderIds)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/merge");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = JsonContent.Create(new MergeOrdersRequestV1(orderIds, key));
        using var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var json = document.RootElement.Clone();
        var error = json.TryGetProperty("error", out var e) ? e : default;
        return new MergeResult(
            response.StatusCode,
            json,
            error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var code) ? code.GetString()! : "",
            error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message) ? message.GetString()! : "");
    }

    private async Task<(Guid TerminalId, string Cookie)> CashierAsync(params string[] permissions)
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", permissions);
        return (terminalId, cookie);
    }

    private async Task<Guid> SeedTableAsync()
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await Exec(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table, @zone, @number, 4, 'Occupied');
            """,
            ("zone", zoneId), ("zone_code", "Z-" + zoneId.ToString("N")), ("table", tableId), ("number", "T-" + tableId.ToString("N")[..6]));
        return tableId;
    }

    private async Task<Order> SeedOrderAsync(Guid tableId, decimal price, OrderSource source = OrderSource.Cashier, OrderState status = OrderState.Submitted)
    {
        var productId = Guid.NewGuid();
        await Exec(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price) VALUES (@id, @sku, 'Dish', 1, 1, @price);",
            ("id", productId), ("sku", "SKU-" + productId.ToString("N")[..8]), ("price", price));
        var orderId = Guid.NewGuid();
        var item = new OrderItem(Guid.NewGuid(), orderId, productId, "Dish", 1m, price, 10m, status: OrderItemState.Active);
        var order = new Order(orderId, source, "ORD-" + orderId.ToString("N"), [item], tableId, status: status);
        await new PostgresOrderRepository(_source).AddAsync(order);
        return order;
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
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private async Task<long> Count(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> Text(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
