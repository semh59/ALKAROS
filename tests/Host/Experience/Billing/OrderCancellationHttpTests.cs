using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Orders.OrderCancellation;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.MovementReversal;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Tables.TableRelease;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>Cancelling a whole order, alone and together with its bill.</summary>
public sealed class OrderCancellationHttpTests : IAsyncLifetime
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
    public async Task CancellingGivesBackTheStockOfUnstartedLinesWastesStartedOnesAndFreesTheBillAndTheTable()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent, KitchenState.Preparing, KitchenState.Held);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Müşteri gitti", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(3, body.RootElement.GetProperty("itemsCancelled").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("stockRestoredItems").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("wasteItems").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("billsCancelled").GetInt32());

        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(3L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id AND status = 'Cancelled' AND kitchen_state = 'Cancelled';", seeded.OrderId));
        Assert.Equal(3L, await Count(CancelledTicketLines, seeded.OrderId));
        Assert.Equal(1m, await OnHandAsync(seeded.Lines[0].StockItemId));
        Assert.Equal(0m, await OnHandAsync(seeded.Lines[1].StockItemId));
        Assert.Equal(1m, await OnHandAsync(seeded.Lines[2].StockItemId));
        Assert.Equal((1m, 0m), await PortionsAsync(seeded.Lines[0].ReservationId));
        Assert.Equal((0m, 1m), await PortionsAsync(seeded.Lines[1].ReservationId));
        Assert.Equal((1m, 0m), await PortionsAsync(seeded.Lines[2].ReservationId));

        Assert.Equal("Cancelled", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", seeded.BillId));
        Assert.Equal("Müşteri gitti", await Text("SELECT cancellation_reason FROM billing.bills WHERE bill_id = @id;", seeded.BillId));
        Assert.Equal(userId, await Guid_("SELECT cancelled_by FROM billing.bills WHERE bill_id = @id;", seeded.BillId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM billing.bill_items WHERE bill_id = @id AND superseded_at IS NULL;", seeded.BillId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM table_mgmt.tables WHERE table_id = @id AND (current_order_id IS NOT NULL OR current_bill_id IS NOT NULL);", seeded.TableId));
        Assert.Equal("Available", await Text("SELECT current_status FROM table_mgmt.tables WHERE table_id = @id;", seeded.TableId));
        Assert.Equal(1L, await Count(
            $"SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @id AND event_name = 'order.cancelled' AND actor_id = '{userId}' AND reason = 'Müşteri gitti';",
            seeded.OrderId));
    }

    [Fact]
    public async Task AnOrderSentToTheTillIsCancelledAndForgottenThere()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await Exec(
            """
            UPDATE table_mgmt.tables SET current_order_id = NULL, current_bill_id = NULL WHERE table_id = @id;
            INSERT INTO table_mgmt.table_sent_checks (order_id, table_id) SELECT order_id, table_id FROM orders.orders WHERE order_id = @order;
            """,
            ("id", seeded.TableId), ("order", seeded.OrderId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Vazgeçti", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0L, await Count("SELECT count(*) FROM table_mgmt.table_sent_checks WHERE order_id = @id;", seeded.OrderId));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Partial")]
    public async Task MoneyOnTheBillRefusesTheCancellationAndNothingChanges(string paymentState)
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await _database.SeedPaymentAsync(seeded.BillId, paymentState, 50m, userId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Deneme", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ORDER_HAS_PAYMENT", await ErrorCode(response));
        await AssertUntouchedAsync(seeded);
    }

    [Fact]
    public async Task APaidBillRefusesTheCancellationEvenWithoutAnAllocationRow()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await Exec("UPDATE billing.bills SET status = 'Paid' WHERE bill_id = @id;", ("id", seeded.BillId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Deneme", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ORDER_HAS_PAYMENT", await ErrorCode(response));
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task APaymentRefundedInFullDoesNotBlockTheCancellation()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await _database.SeedPaymentAsync(seeded.BillId, "Refunded", 50m, userId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Iade edilmisti", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnOrderWithoutABillAndADraftLineIsCancelledWithoutTouchingStock()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(false, KitchenState.Sent, KitchenState.NotSent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Vazgeçti", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("itemsCancelled").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("stockRestoredItems").GetInt32());
        Assert.Equal(0, body.RootElement.GetProperty("billsCancelled").GetInt32());
        Assert.Equal(1m, await OnHandAsync(seeded.Lines[0].StockItemId));
        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal("Available", await Text("SELECT current_status FROM table_mgmt.tables WHERE table_id = @id;", seeded.TableId));
    }

    [Fact]
    public async Task ACancellationWaitsForAPaymentThatHoldsTheBillSettlementLock()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await using var holder = await _source.OpenConnectionAsync();
        await using var holderTransaction = await holder.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", holder, holderTransaction))
        {
            hold.Parameters.AddWithValue($"bill-settlement:{seeded.BillId:N}");
            await hold.ExecuteNonQueryAsync();
        }

        var cancelling = client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Deneme", seeded.RowVersion));
        await Task.Delay(1500);
        Assert.False(cancelling.IsCompleted);

        await holderTransaction.CommitAsync();
        using var response = await cancelling;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AServedLineRefusesTheCancellationAndNothingChanges()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent, KitchenState.Served);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Deneme", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ORDER_HAS_SERVED_ITEMS", await ErrorCode(response));
        await AssertUntouchedAsync(seeded);
    }

    [Fact]
    public async Task ACancelledOrderCannotBeCancelledAgainAndAReplayWithTheSameKeyAnswersTheSame()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var key = Guid.NewGuid().ToString("N");

        using var first = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Birinci", seeded.RowVersion, key));
        using var replay = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Birinci", seeded.RowVersion, key));
        using var again = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Ikinci", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("ORDER_CANCEL_NOT_ALLOWED", await ErrorCode(again));
        Assert.Equal(1L, await Count("SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @id AND event_name = 'order.cancelled';", seeded.OrderId));
        Assert.Equal(1m, await OnHandAsync(seeded.Lines[0].StockItemId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ACancellationWithoutAReasonIsRefusedBeforeAnythingChanges(string? reason)
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, reason, seeded.RowVersion));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("REASON_REQUIRED", await ErrorCode(response));
        await AssertUntouchedAsync(seeded);
    }

    [Fact]
    public async Task ARoleWithoutTheCancelPermissionIsRefused()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "waiter", "bills.split");
        var seeded = await SeedAsync(KitchenState.Sent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Deneme", seeded.RowVersion));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertUntouchedAsync(seeded);
    }

    [Fact]
    public async Task AStaleOrderVersionIsRefusedAndNothingChanges()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelOrder(terminalId, seeded.OrderId, cookie, "Deneme", seeded.RowVersion + 7));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", await ErrorCode(response));
        await AssertUntouchedAsync(seeded);
    }

    [Fact]
    public async Task ACancellationWhoseAuditRecordCannotBeWrittenIsNotKept()
    {
        var seeded = await SeedAsync(KitchenState.Sent);
        var store = OrderCancellationStores.Create(_source, new FailingAuditEventStore());
        var (userId, _) = await _database.SeedSessionWithPermissionsAsync(Guid.NewGuid(), "cashier", "bills.cancel");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CancelAsync(
            seeded.OrderId, "Deneme", seeded.RowVersion, userId, "test", CancellationToken.None));

        await AssertUntouchedAsync(seeded);
        Assert.Equal(0m, await OnHandAsync(seeded.Lines[0].StockItemId));
    }

    [Fact]
    public async Task CancellingABillWithTheOrderFlagCancelsTheOrderToo()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent, KitchenState.Preparing);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelBill(terminalId, seeded.BillId, cookie, "Müşteri gitti", cancelOrder: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("orderCancelled").GetBoolean());
        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal("Cancelled", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", seeded.BillId));
        Assert.Equal(1m, await OnHandAsync(seeded.Lines[0].StockItemId));
        Assert.Equal("Available", await Text("SELECT current_status FROM table_mgmt.tables WHERE table_id = @id;", seeded.TableId));
        Assert.Equal(1L, await Count("SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @id AND event_name = 'bill.cancelled';", seeded.BillId));
        Assert.Equal(1L, await Count("SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @id AND event_name = 'order.cancelled';", seeded.OrderId));
    }

    [Fact]
    public async Task CancellingABillWithoutTheOrderFlagLeavesTheOrderOnItsTable()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelBill(terminalId, seeded.BillId, cookie, "Yeniden açılacak", cancelOrder: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.GetProperty("orderCancelled").GetBoolean());
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(0m, await OnHandAsync(seeded.Lines[0].StockItemId));
        Assert.Equal(seeded.OrderId, await Guid_("SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @id;", seeded.TableId));
    }

    [Fact]
    public async Task CancellingABillWithTheOrderFlagStillRefusesWhenMoneyMovedOnTheBill()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split", "bills.cancel");
        var seeded = await SeedAsync(KitchenState.Sent);
        await _database.SeedPaymentAsync(seeded.BillId, "Pending", 50m, userId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(CancelBill(terminalId, seeded.BillId, cookie, "Deneme", cancelOrder: true));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("BILL_HAS_PAYMENT", await ErrorCode(response));
        await AssertUntouchedAsync(seeded);
    }

    private const string CancelledTicketLines =
        "SELECT count(*) FROM kitchen.kitchen_ticket_items i JOIN kitchen.kitchen_tickets t ON t.id = i.ticket_id " +
        "WHERE t.order_id = @id AND i.status = 'Cancelled';";

    private async Task AssertUntouchedAsync(SeededOrder seeded)
    {
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM orders.order_items WHERE order_id = @id AND status = 'Cancelled';", seeded.OrderId));
        Assert.NotEqual("Cancelled", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", seeded.BillId));
        Assert.Equal(seeded.OrderId, await Guid_("SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @id;", seeded.TableId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @id AND event_name = 'order.cancelled';", seeded.OrderId));
        Assert.Equal(0L, await Count(CancelledTicketLines, seeded.OrderId));
    }

    private sealed record SeededLine(Guid StockItemId, Guid ReservationId);

    private sealed record SeededOrder(
        Guid OrderId, long RowVersion, Guid BillId, Guid TableId, IReadOnlyList<SeededLine> Lines);

    /// <summary>
    /// A submitted order on an occupied table, one line per kitchen state: each line consumed one unit of its own stock
    /// item and counted one daily menu portion, sits on a kitchen ticket, and is billed on an open bill.
    /// </summary>
    private Task<SeededOrder> SeedAsync(params KitchenState[] states) => SeedAsync(true, states);

    /// <summary>A <see cref="KitchenState.NotSent"/> state seeds a Draft line (nothing consumed, nothing on the ticket).</summary>
    private async Task<SeededOrder> SeedAsync(bool withBill, params KitchenState[] states)
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

        var orderId = Guid.NewGuid();
        var items = new List<OrderItem>();
        var lines = new List<SeededLine>();
        foreach (var state in states)
        {
            var productId = Guid.NewGuid();
            await Exec(
                "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price) VALUES (@id, @sku, 'Dish', 1, 1, 100);",
                ("id", productId), ("sku", "SKU-" + productId.ToString("N")[..8]));
            if (state == KitchenState.NotSent)
            {
                items.Add(new OrderItem(Guid.NewGuid(), orderId, productId, "Dish", 1m, 100m, 10m, status: OrderItemState.Draft));
                lines.Add(new SeededLine(Guid.Empty, Guid.Empty));
                continue;
            }

            var item = new OrderItem(
                Guid.NewGuid(), orderId, productId, "Dish", 1m, 100m, 10m, status: OrderItemState.Active, kitchenState: state);
            items.Add(item);
            var (stockItemId, locationId) = await SeedConsumedStockAsync(item.Id);
            lines.Add(new SeededLine(stockItemId, await SeedPortionAsync(item.Id, stockItemId, locationId)));
        }

        var order = new Order(orderId, OrderSource.Cashier, "ORD-" + orderId.ToString("N"), items, tableId, status: OrderState.Submitted);
        await new PostgresOrderRepository(_source).AddAsync(order);

        var ticketId = Guid.NewGuid();
        await new PostgresKitchenTicketRepository(_source).AddAsync(new KitchenTicket(
            ticketId, orderId, "KT-" + ticketId.ToString("N")[..8], "hot-line",
            items.Where(i => i.Status == OrderItemState.Active)
                .Select(i => new KitchenTicketItem(Guid.NewGuid(), ticketId, i.Id, i.ProductId, "Dish", quantity: 1)).ToArray()));

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
        return new SeededOrder(orderId, stored!.RowVersion, billId, tableId, lines);
    }

    private async Task<(Guid StockItemId, Guid LocationId)> SeedConsumedStockAsync(Guid orderItemId)
    {
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var suffix = stockItemId.ToString("N")[..8];
        await Exec(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type) VALUES (@location, @code, 'Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@item, @code, 'Stock item', 'Portion', 'adet', @location);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance, @item, @location, 0, 0, 0);
            """,
            ("location", locationId), ("item", stockItemId), ("code", "OC-" + suffix), ("balance", Guid.NewGuid()));
        await new PostgresStockMovementRepository(_source).AppendAsync(new StockMovement(
            id: Guid.NewGuid(), stockItemId: stockItemId, stockLocationId: locationId, movementType: StockMovementType.Consumption,
            direction: MovementDirection.Out, quantity: 1m, unitCode: "adet", sourceType: StockMovementSourceType.Order,
            sourceReferenceId: orderItemId, reason: "Test seed: order item sent"));
        return (stockItemId, locationId);
    }

    private async Task<Guid> SeedPortionAsync(Guid orderItemId, Guid stockItemId, Guid locationId)
    {
        var reservationId = Guid.NewGuid();
        await Exec(
            """
            INSERT INTO inventory.portion_reservations (
                id, order_id, order_item_id, stock_item_id, stock_location_id, quantity, unit_code, status, version,
                idempotency_key, reserved_at, transitioned_at, created_by, transitioned_by, metadata)
            VALUES (@id, @order, @item, @stock, @location, 1, 'adet', 'Consumed', 1,
                @key, NOW(), NOW(), @actor, @actor,
                jsonb_build_object('daily_menu_item_id', @menu::text, 'portion_count', 1));
            """,
            ("id", reservationId), ("order", Guid.NewGuid()), ("item", orderItemId), ("stock", stockItemId), ("location", locationId),
            ("key", "dsale:" + orderItemId.ToString("N")), ("actor", Guid.NewGuid()), ("menu", Guid.NewGuid()));
        return reservationId;
    }

    private async Task<decimal> OnHandAsync(Guid stockItemId)
        => (decimal)(await Scalar("SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @id;", stockItemId))!;

    private async Task<(decimal Returned, decimal Wasted)> PortionsAsync(Guid reservationId)
    {
        await using var command = _source.CreateCommand(
            """
            SELECT COALESCE((metadata->>'portions_returned')::numeric, 0), COALESCE((metadata->>'portions_wasted')::numeric, 0)
            FROM inventory.portion_reservations WHERE id = @id;
            """);
        command.Parameters.AddWithValue("id", reservationId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    private async Task<object?> Scalar(string sql, Guid id)
    {
        await using var command = _source.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync();
    }

    private async Task<string> Text(string sql, Guid id) => (string)(await Scalar(sql, id))!;

    private async Task<long> Count(string sql, Guid id) => (long)(await Scalar(sql, id))!;

    private async Task<Guid?> Guid_(string sql, Guid id) => await Scalar(sql, id) as Guid?;

    private async Task Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private static HttpRequestMessage CancelOrder(
        Guid terminalId, Guid orderId, string cookie, string? reason, long rowVersion, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/cancel");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = JsonContent.Create(new { idempotencyKey = key ?? Guid.NewGuid().ToString("N"), expectedRowVersion = rowVersion, reason });
        return request;
    }

    private static HttpRequestMessage CancelBill(Guid terminalId, Guid billId, string cookie, string reason, bool cancelOrder)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/cancel");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = JsonContent.Create(new { reason, cancelOrder });
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

internal static class OrderCancellationStores
{
    public static OrderCancellationStore Create(NpgsqlDataSource source, ALKAROS.Audit.EventStore.IAuditEventStore audit)
    {
        var movements = new PostgresStockMovementRepository(source);
        var balances = new PostgresStockBalanceRepository(source);
        var locations = new PostgresStockLocationRepository(source);
        return new OrderCancellationStore(
            new PostgresOrderRepository(source),
            new PostgresBillRepository(source),
            new OrderItemVoidEffects(
                movements,
                new StockMovementReversalService(
                    movements, new PostgresStockItemRepository(source), locations, new StockBalanceProjector(balances, movements, locations), balances),
                new PostgresReservationAwareConsumptionGuard(),
                new PostgresKitchenTicketRepository(source)),
            new PostgresTableReleasePort(),
            source,
            audit);
    }

    public static BillCancellationStore CreateBill(NpgsqlDataSource source, ALKAROS.Audit.EventStore.IAuditEventStore audit)
        => new(source, audit, new PostgresBillRepository(source), new PostgresTableReleasePort(), Create(source, audit));
}
