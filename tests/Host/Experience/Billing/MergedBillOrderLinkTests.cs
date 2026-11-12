using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Orders.OrderCancellation;
using ALKAROS.Host.Experience.Orders.OrderCleanup;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>
/// A bill merged from several orders carries no order id; its orders are the ones its items come from. Every consumer that
/// asks "which bill does this order have" must still find it (real host services, real Postgres).
/// </summary>
public sealed class MergedBillOrderLinkTests : IAsyncLifetime
{
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    private readonly BillingSplitTestDatabase _database = new();
    private NpgsqlDataSource _source = null!;
    private Guid _actor;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _source = NpgsqlDataSource.Create(_database.ConnectionString);
        (_actor, _) = await _database.SeedSessionWithPermissionsAsync(Guid.NewGuid(), "cashier", "bills.cancel");
    }

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task APaidMergedBillCompletesEveryOrderItIsMadeOfAndFreesTheTable()
    {
        var merged = await SeedMergedAsync(attachToTable: true);
        await Exec("UPDATE billing.bills SET status = 'Paid', closed_at = now() - interval '30 minutes' WHERE bill_id = @id;", ("id", merged.BillId));
        await using var app = await StartAsync();

        var repaired = await app.Services.GetServices<IHostedService>().OfType<BillClosureSweeper>().Single()
            .RepairPaidBillOrdersAsync(Grace, CancellationToken.None);

        Assert.Equal(2, repaired);
        Assert.Equal("Completed", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.FirstOrderId));
        Assert.Equal("Completed", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.SecondOrderId));
        Assert.Equal("Available", await Text("SELECT current_status FROM table_mgmt.tables WHERE table_id = @id;", merged.TableId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM table_mgmt.tables WHERE table_id = @id AND current_order_id IS NOT NULL;", merged.TableId));
    }

    [Fact]
    public async Task AnOpenMergedBillKeepsItsOrdersOutOfTheHangingListAndACancelledOneReleasesThem()
    {
        var merged = await SeedMergedAsync();
        await Exec("UPDATE orders.orders SET created_at = now() - interval '3 hours' WHERE order_id IN (@a, @b);", ("a", merged.FirstOrderId), ("b", merged.SecondOrderId));
        var store = CleanupStore();

        var whileOpen = await store.ListHangingAsync(TimeSpan.FromHours(2), 50, CancellationToken.None);
        await Exec("UPDATE billing.bills SET status = 'Cancelled' WHERE bill_id = @id;", ("id", merged.BillId));
        var afterCancel = await store.ListHangingAsync(TimeSpan.FromHours(2), 50, CancellationToken.None);

        Assert.DoesNotContain(whileOpen, o => o.OrderId == merged.FirstOrderId || o.OrderId == merged.SecondOrderId);
        Assert.Contains(afterCancel, o => o.OrderId == merged.FirstOrderId);
        Assert.Contains(afterCancel, o => o.OrderId == merged.SecondOrderId);
    }

    [Fact]
    public async Task CancellingOneOrderOfAMergedBillCancelsTheBillAndFreesItsItemsButLeavesTheOtherOrderOpen()
    {
        var merged = await SeedMergedAsync(attachToTable: true);

        await CancellationStore().CancelAsync(merged.FirstOrderId, "Müşteri vazgeçti", null, _actor, "corr-merged-1", CancellationToken.None);

        Assert.Equal("Cancelled", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", merged.BillId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM billing.bill_items WHERE bill_id = @id AND superseded_at IS NULL;", merged.BillId));
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.SecondOrderId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM table_mgmt.tables WHERE table_id = @id AND current_bill_id IS NOT NULL;", merged.TableId));
    }

    [Fact]
    public async Task CancellingAMergedBillWithTheOrderFlagCancelsEveryOrderOfTheBill()
    {
        var merged = await SeedMergedAsync(attachToTable: true);
        await using var app = await StartAsync();

        var result = await app.Services.GetRequiredService<BillCancellationStore>().CancelAsync(
            merged.BillId, "Hesap iptal", cancelOrder: true, _actor, "corr-merged-2", CancellationToken.None);

        Assert.True(result.OrderCancelled);
        Assert.Equal(new[] { merged.FirstOrderId, merged.SecondOrderId }.Order(), result.CancelledOrderIds!.Order());
        Assert.Equal("Cancelled", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", merged.BillId));
        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.FirstOrderId));
        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.SecondOrderId));
        Assert.Equal("Available", await Text("SELECT current_status FROM table_mgmt.tables WHERE table_id = @id;", merged.TableId));
    }

    [Fact]
    public async Task CancellingAMergedBillWithoutTheOrderFlagLeavesBothOrdersOpen()
    {
        var merged = await SeedMergedAsync();
        await using var app = await StartAsync();

        var result = await app.Services.GetRequiredService<BillCancellationStore>().CancelAsync(
            merged.BillId, "Hesap iptal", cancelOrder: false, _actor, "corr-merged-3", CancellationToken.None);

        Assert.False(result.OrderCancelled);
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.FirstOrderId));
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.SecondOrderId));
    }

    [Fact]
    public async Task APaidMergedBillCannotBeCancelledWithTheOrderFlagEither()
    {
        var merged = await SeedMergedAsync();
        await Exec("UPDATE billing.bills SET status = 'Paid' WHERE bill_id = @id;", ("id", merged.BillId));
        await using var app = await StartAsync();

        await Assert.ThrowsAsync<BillCancelHasPaymentException>(() => app.Services.GetRequiredService<BillCancellationStore>().CancelAsync(
            merged.BillId, "Hesap iptal", cancelOrder: true, _actor, "corr-merged-5", CancellationToken.None));
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.FirstOrderId));
    }

    [Fact]
    public async Task IfOneOrderOfAMergedBillCannotBeCancelledNoneOfThemIsAndTheBillStaysOpen()
    {
        var merged = await SeedMergedAsync(attachToTable: true);
        await Exec(
            "UPDATE orders.order_items SET kitchen_state = 'Served' WHERE order_id = @id;", ("id", merged.SecondOrderId));
        await using var app = await StartAsync();

        await Assert.ThrowsAsync<OrderCancelHasServedItemsException>(() => app.Services.GetRequiredService<BillCancellationStore>().CancelAsync(
            merged.BillId, "Hesap iptal", cancelOrder: true, _actor, "corr-merged-6", CancellationToken.None));

        Assert.Equal("Open", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", merged.BillId));
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.FirstOrderId));
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.SecondOrderId));
    }

    [Fact]
    public async Task OnlyTheOrdersWhoseEveryBillIsSettledAreCompletedWhenAMergedBillIsPaid()
    {
        var merged = await SeedMergedAsync(attachToTable: true);
        await Exec("UPDATE billing.bills SET status = 'Paid', closed_at = now() - interval '30 minutes' WHERE bill_id = @id;", ("id", merged.BillId));
        await Exec(
            "INSERT INTO billing.bills (bill_id, bill_number, table_id, order_id, status, currency_code, opened_at, created_at, updated_at) " +
            "VALUES (@bill, @number, @table, @order, 'Open', 'TRY', now(), now(), now());",
            ("bill", Guid.NewGuid()), ("number", "BIL-X-" + Guid.NewGuid().ToString("N")[..10]), ("table", merged.TableId), ("order", merged.SecondOrderId));
        await using var app = await StartAsync();

        var repaired = await app.Services.GetServices<IHostedService>().OfType<BillClosureSweeper>().Single()
            .RepairPaidBillOrdersAsync(Grace, CancellationToken.None);

        Assert.Equal(1, repaired);
        Assert.Equal("Completed", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.FirstOrderId));
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", merged.SecondOrderId));
    }

    [Fact]
    public async Task APaidMergedBillBlocksCancellingEitherOfItsOrders()
    {
        var merged = await SeedMergedAsync();
        await Exec("UPDATE billing.bills SET status = 'Paid' WHERE bill_id = @id;", ("id", merged.BillId));

        await Assert.ThrowsAsync<OrderCancelHasPaymentException>(() =>
            CancellationStore().CancelAsync(merged.SecondOrderId, "Müşteri vazgeçti", null, _actor, "corr-merged-4", CancellationToken.None));
        Assert.Equal("Paid", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", merged.BillId));
    }

    [Fact]
    public async Task FromOrderOnAnOrderOfAMergedBillReturnsTheMergedBillAndOpensNoSecondOne()
    {
        var merged = await SeedMergedAsync();
        await using var app = await StartAsync();

        var design = await app.Services.GetRequiredService<BillingSplitStore>()
            .CreateBillFromOrderAsync(merged.SecondOrderId, canMutate: true, CancellationToken.None);

        Assert.Equal(merged.BillId, design.BillId);
        Assert.Equal(1L, await Count("SELECT count(*) FROM billing.bills WHERE table_id = @id;", merged.TableId));
    }

    [Fact]
    public async Task TheTillQueueShowsTheMergedBillOnItsOrdersAndDropsThemOncePaid()
    {
        var merged = await SeedMergedAsync();
        await Exec("UPDATE table_mgmt.tables SET current_order_id = NULL WHERE table_id = @id;", ("id", merged.TableId));
        await Exec(
            "UPDATE orders.order_items SET without_kitchen_reason_code = 'other', added_without_kitchen_by = gen_random_uuid() WHERE order_id = @id;",
            ("id", merged.SecondOrderId));
        var store = new CashierHandoffStore(_source);

        var whileOpen = await store.GetChecksAwaitingPaymentAsync();
        await Exec("UPDATE billing.bills SET status = 'Paid' WHERE bill_id = @id;", ("id", merged.BillId));
        var afterPaid = await store.GetChecksAwaitingPaymentAsync();

        var check = Assert.Single(whileOpen, c => c.BillId == merged.BillId);
        Assert.Equal(merged.FirstOrderId, check.OrderId);
        Assert.Equal(2, check.ItemCount);
        Assert.Equal(200m, check.Total);
        Assert.Equal("Dish, Dish", check.ItemPreview);
        Assert.Equal(1, check.AddedWithoutKitchenCount);
        Assert.DoesNotContain(afterPaid, c => c.OrderId == merged.FirstOrderId || c.OrderId == merged.SecondOrderId);
    }

    [Fact]
    public async Task ATableOrderEntersTheTillQueueOnlyOnceItsBillIsOpened()
    {
        var (tableId, order) = await SeedTableBoundOrderAsync();
        var store = new CashierHandoffStore(_source);

        var beforeBill = await store.GetChecksAwaitingPaymentAsync();
        var bill = BillSourceOperations.CreateMergedBill(Guid.NewGuid(), "BIL-T-" + Guid.NewGuid().ToString("N")[..10], [order.Items[0]], tableId);
        await new PostgresBillRepository(_source).AddAsync(bill);
        var afterBill = await store.GetChecksAwaitingPaymentAsync();

        Assert.DoesNotContain(beforeBill, c => c.OrderId == order.Id);
        Assert.Contains(afterBill, c => c.OrderId == order.Id && c.BillId == bill.Id);
    }

    [Fact]
    public async Task ACheckWhoseMergedBillHoldsMoneyCannotBeRecalledToItsTableAndOneWithoutMoneyCancelsTheBill()
    {
        var withMoney = await SeedMergedAsync();
        await Exec("UPDATE table_mgmt.tables SET current_order_id = NULL WHERE table_id = @id;", ("id", withMoney.TableId));
        await _database.SeedCollectedAsync(withMoney.BillId, 50m, _actor);
        var free = await SeedMergedAsync();
        await Exec("UPDATE table_mgmt.tables SET current_order_id = NULL WHERE table_id = @id;", ("id", free.TableId));
        var store = new CashierHandoffStore(_source);
        var audit = new ALKAROS.Audit.EventStore.PostgresAuditEventStore(_source);

        await Assert.ThrowsAsync<CheckHasPaymentException>(() =>
            store.RecallCheckAsync(withMoney.TableId, withMoney.FirstOrderId, new PostgresBillRepository(_source), audit, _actor, "corr-recall-1"));
        await store.RecallCheckAsync(free.TableId, free.FirstOrderId, new PostgresBillRepository(_source), audit, _actor, "corr-recall-2");

        Assert.Equal("Open", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", withMoney.BillId));
        Assert.Equal("Cancelled", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", free.BillId));
    }

    [Fact]
    public async Task ReleasingAnOrderPointsTheTableAtTheNextOrdersOwnBillNotAtAnUnrelatedMergedOne()
    {
        var unrelated = await SeedMergedAsync();
        var released = await SeedOrderAsync(unrelated.TableId);
        var next = await SeedOrderAsync(unrelated.TableId);
        var nextBillId = Guid.NewGuid();
        await Exec("UPDATE orders.orders SET created_at = now() + interval '1 hour' WHERE order_id IN (@a, @b);", ("a", unrelated.FirstOrderId), ("b", unrelated.SecondOrderId));
        await Exec(
            "INSERT INTO billing.bills (bill_id, bill_number, table_id, order_id, status, currency_code, opened_at, created_at, updated_at) " +
            "VALUES (@bill, @number, @table, @order, 'Open', 'TRY', now(), now(), now());",
            ("bill", nextBillId), ("number", "BIL-N-" + nextBillId.ToString("N")[..10]), ("table", unrelated.TableId), ("order", next.Id));
        await Exec("UPDATE table_mgmt.tables SET current_order_id = @order WHERE table_id = @table;", ("order", released.Id), ("table", unrelated.TableId));

        await using (var connection = await _source.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await new ALKAROS.Tables.TableRelease.PostgresTableReleasePort().ReleaseOrderAsync(released.Id, "Hesap ödendi", connection, transaction);
            await transaction.CommitAsync();
        }

        Assert.Equal(next.Id, await Guid_("SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @id;", unrelated.TableId));
        Assert.Equal(nextBillId, await Guid_("SELECT current_bill_id FROM table_mgmt.tables WHERE table_id = @id;", unrelated.TableId));
    }

    [Fact]
    public async Task ClearingACancelledBillsPointerLeavesTheTablePointingAtItsOtherOpenBill()
    {
        var (tableId, first) = await SeedTableBoundOrderAsync();
        var second = await SeedOrderAsync(tableId);
        var firstBillId = Guid.NewGuid();
        var secondBillId = Guid.NewGuid();
        foreach (var (bill, order) in new[] { (firstBillId, first.Id), (secondBillId, second.Id) })
            await Exec(
                "INSERT INTO billing.bills (bill_id, bill_number, table_id, order_id, status, currency_code, opened_at, created_at, updated_at) " +
                "VALUES (@bill, @number, @table, @order, 'Open', 'TRY', now(), now(), now());",
                ("bill", bill), ("number", "BIL-P-" + bill.ToString("N")[..10]), ("table", tableId), ("order", order));
        await Exec("UPDATE table_mgmt.tables SET current_bill_id = @bill WHERE table_id = @table;", ("bill", firstBillId), ("table", tableId));
        await Exec("UPDATE billing.bills SET status = 'Cancelled' WHERE bill_id = @id;", ("id", firstBillId));

        await using (var connection = await _source.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await new ALKAROS.Tables.TableRelease.PostgresTableReleasePort().ClearBillPointerAsync(firstBillId, connection, transaction);
            await transaction.CommitAsync();
        }

        Assert.Equal(secondBillId, await Guid_("SELECT current_bill_id FROM table_mgmt.tables WHERE table_id = @id;", tableId));
    }

    [Fact]
    public async Task TheMergedBillIsFoundOnEveryOrderItIsMadeOf()
    {
        var merged = await SeedMergedAsync();
        var bills = new PostgresBillRepository(_source);

        Assert.Equal(merged.BillId, Assert.Single(await bills.GetByOrderIdAsync(merged.FirstOrderId)).Id);
        Assert.Equal(merged.BillId, Assert.Single(await bills.GetByOrderIdAsync(merged.SecondOrderId)).Id);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Paid")]
    public async Task ATableOrderWhoseBillIsCancelledOrPaidIsNotInTheTillQueue(string billStatus)
    {
        var (tableId, order) = await SeedTableBoundOrderAsync();
        var bill = BillSourceOperations.CreateMergedBill(Guid.NewGuid(), "BIL-S-" + Guid.NewGuid().ToString("N")[..10], [order.Items[0]], tableId);
        await new PostgresBillRepository(_source).AddAsync(bill);
        await Exec("UPDATE billing.bills SET status = @status WHERE bill_id = @id;", ("status", billStatus), ("id", bill.Id));

        Assert.DoesNotContain(await new CashierHandoffStore(_source).GetChecksAwaitingPaymentAsync(), c => c.OrderId == order.Id);
    }

    [Fact]
    public async Task RecallingAnOrderStillAtItsTableCancelsTheBillAndClearsTheTablePointer()
    {
        var (tableId, order) = await SeedTableBoundOrderAsync();
        var bill = BillSourceOperations.CreateMergedBill(Guid.NewGuid(), "BIL-R-" + Guid.NewGuid().ToString("N")[..10], [order.Items[0]], tableId);
        await new PostgresBillRepository(_source).AddAsync(bill);
        await Exec("UPDATE table_mgmt.tables SET current_bill_id = @bill WHERE table_id = @table;", ("bill", bill.Id), ("table", tableId));
        var store = new CashierHandoffStore(_source);

        var result = await store.RecallCheckAsync(
            tableId, order.Id, new PostgresBillRepository(_source), new ALKAROS.Audit.EventStore.PostgresAuditEventStore(_source), _actor, "corr-recall-3");

        Assert.Equal("AlreadyAttached", result.Outcome);
        Assert.Equal("Cancelled", await Text("SELECT status FROM billing.bills WHERE bill_id = @id;", bill.Id));
        Assert.Equal(0L, await Count("SELECT count(*) FROM table_mgmt.tables WHERE table_id = @id AND current_bill_id IS NOT NULL;", tableId));
    }

    private async Task<(Guid TableId, Order Order)> SeedTableBoundOrderAsync()
    {
        var tableId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        await Exec(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table, @zone, @number, 4, 'Occupied');
            """,
            ("zone", zoneId), ("zone_code", "Z-" + zoneId.ToString("N")), ("table", tableId), ("number", "T-" + tableId.ToString("N")[..6]));
        var order = await SeedOrderAsync(tableId);
        await Exec("UPDATE table_mgmt.tables SET current_order_id = @order WHERE table_id = @table;", ("order", order.Id), ("table", tableId));
        return (tableId, order);
    }

    private sealed record Merged(Guid BillId, Guid FirstOrderId, Guid SecondOrderId, Guid TableId);

    /// <summary>Two submitted orders of one table, one item each, billed together on one bill that has no order id.</summary>
    private async Task<Merged> SeedMergedAsync(bool attachToTable = false)
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

        var first = await SeedOrderAsync(tableId);
        var second = await SeedOrderAsync(tableId);
        var bill = BillSourceOperations.CreateMergedBill(
            Guid.NewGuid(), "BIL-M-" + Guid.NewGuid().ToString("N")[..10], [first.Items[0], second.Items[0]], tableId);
        await new PostgresBillRepository(_source).AddAsync(bill);
        if (attachToTable)
            await Exec(
                "UPDATE table_mgmt.tables SET current_order_id = @order, current_bill_id = @bill WHERE table_id = @table;",
                ("order", first.Id), ("bill", bill.Id), ("table", tableId));
        return new Merged(bill.Id, first.Id, second.Id, tableId);
    }

    private async Task<Order> SeedOrderAsync(Guid tableId)
    {
        var productId = Guid.NewGuid();
        await Exec(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price) VALUES (@id, @sku, 'Dish', 1, 1, 100);",
            ("id", productId), ("sku", "SKU-" + productId.ToString("N")[..8]));
        var orderId = Guid.NewGuid();
        var item = new OrderItem(Guid.NewGuid(), orderId, productId, "Dish", 1m, 100m, 10m, status: OrderItemState.Active);
        var order = new Order(orderId, OrderSource.Cashier, "ORD-" + orderId.ToString("N"), [item], tableId, status: OrderState.Submitted);
        await new PostgresOrderRepository(_source).AddAsync(order);
        return order;
    }

    private OrderCancellationStore CancellationStore() => OrderCancellationStores.Create(_source, new ALKAROS.Audit.EventStore.PostgresAuditEventStore(_source));

    private OrderCleanupStore CleanupStore() => new(_source, CancellationStore());

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        await app.StartAsync();
        return app;
    }

    private async Task<object?> Scalar(string sql, Guid id)
    {
        await using var command = _source.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync();
    }

    private async Task<Guid?> Guid_(string sql, Guid id) => await Scalar(sql, id) as Guid?;

    private async Task<string> Text(string sql, Guid id) => (string)(await Scalar(sql, id))!;

    private async Task<long> Count(string sql, Guid id) => (long)(await Scalar(sql, id))!;

    private async Task Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
