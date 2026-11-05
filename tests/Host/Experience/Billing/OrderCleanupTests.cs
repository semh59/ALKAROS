using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Orders.OrderCleanup;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>The empty-order sweeper, and the screens told when a bill or an order is cancelled.</summary>
public sealed class OrderCleanupTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromHours(2);

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
    public async Task AnEmptyOrderLeftForLongerThanTheTimeoutIsCancelledByTheSystemAndItsTableIsFree()
    {
        var empty = await SeedOrderAsync(OrderState.Draft, ageHours: 3);
        var sweeper = Sweeper();

        var first = await sweeper.SweepOnceAsync(Timeout, CancellationToken.None);
        var second = await sweeper.SweepOnceAsync(Timeout, CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", empty.OrderId));
        Assert.Equal("Available", await Text("SELECT current_status FROM table_mgmt.tables WHERE table_id = @id;", empty.TableId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM table_mgmt.tables WHERE table_id = @id AND current_order_id IS NOT NULL;", empty.TableId));
        Assert.Equal(1L, await Count(
            $"SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @id AND event_name = 'order.cancelled' AND actor_type = 'System' " +
            $"AND actor_id = '{OrderCleanupStore.SystemActorId}' AND reason = '{OrderCleanupStore.EmptyOrderReason}';",
            empty.OrderId));
    }

    [Fact]
    public void TheAgreedTimeoutAndSweepIntervalAreUsed()
    {
        Assert.Equal(Timeout, EmptyOrderSweeper.IdleTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), EmptyOrderSweeper.Interval);
    }

    [Theory]
    [InlineData(119, "Draft")]
    [InlineData(121, "Cancelled")]
    public async Task OnlyAnOrderUntouchedForMoreThanTheTimeoutIsCancelled(int minutesUntouched, string expectedStatus)
    {
        var seeded = await SeedOrderAsync(OrderState.Draft, ageHours: 0);
        await Exec("UPDATE orders.orders SET updated_at = now() - make_interval(mins => @m) WHERE order_id = @id;", ("m", minutesUntouched), ("id", seeded.OrderId));

        await Sweeper().SweepOnceAsync(Timeout, CancellationToken.None);

        Assert.Equal(expectedStatus, await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task AWaiterOpenedEmptyOrderIsCancelledToo()
    {
        var seeded = await SeedOrderAsync(OrderState.Draft, ageHours: 3, source: OrderSource.Waiter);

        Assert.Equal(1, await Sweeper().SweepOnceAsync(Timeout, CancellationToken.None));
        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task AnOrderThatCannotBeCancelledDoesNotStopTheSweepOfTheOthers()
    {
        var poison = await SeedOrderAsync(OrderState.Draft, ageHours: 3);
        var fine = await SeedOrderAsync(OrderState.Draft, ageHours: 3);
        await Exec(
            """
            CREATE FUNCTION audit.test_refuse_order() RETURNS trigger AS $fn$
            BEGIN
                IF NEW.event_name = 'order.cancelled' AND NEW.aggregate_id = 'POISON'::uuid THEN RAISE EXCEPTION 'refused by the test'; END IF;
                RETURN NEW;
            END;
            $fn$ LANGUAGE plpgsql;
            CREATE TRIGGER test_refuse_order BEFORE INSERT ON audit.audit_events FOR EACH ROW EXECUTE FUNCTION audit.test_refuse_order();
            """.Replace("POISON", poison.OrderId.ToString("D")));

        var cancelled = await Sweeper().SweepOnceAsync(Timeout, CancellationToken.None);

        Assert.Equal(1, cancelled);
        Assert.Equal("Draft", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", poison.OrderId));
        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", fine.OrderId));
    }

    [Theory]
    [InlineData("recent")]
    [InlineData("with-draft-item")]
    [InlineData("with-active-item")]
    [InlineData("with-bill")]
    [InlineData("submitted")]
    [InlineData("online")]
    public async Task AnOrderThatIsNotAnOldEmptyDraftIsLeftAlone(string scenario)
    {
        var seeded = scenario switch
        {
            "recent" => await SeedOrderAsync(OrderState.Draft, ageHours: 0),
            "with-draft-item" => await SeedOrderAsync(OrderState.Draft, ageHours: 3, item: OrderItemState.Draft),
            "with-active-item" => await SeedOrderAsync(OrderState.Draft, ageHours: 3, item: OrderItemState.Active),
            "with-bill" => await SeedOrderAsync(OrderState.Draft, ageHours: 3, withBill: true),
            "submitted" => await SeedOrderAsync(OrderState.Submitted, ageHours: 3),
            _ => await SeedOrderAsync(OrderState.Draft, ageHours: 3, source: OrderSource.Online),
        };

        var swept = await Sweeper().SweepOnceAsync(Timeout, CancellationToken.None);

        Assert.Equal(0, swept);
        Assert.NotEqual("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(seeded.OrderId, await Guid_("SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @id;", seeded.TableId));
    }

    [Fact]
    public async Task ADraftWhoseOnlyLineWasCancelledCountsAsEmpty()
    {
        var seeded = await SeedOrderAsync(OrderState.Draft, ageHours: 3, item: OrderItemState.Cancelled);

        Assert.Equal(1, await Sweeper().SweepOnceAsync(Timeout, CancellationToken.None));
        Assert.Equal("Cancelled", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
    }

    [Fact]
    public async Task AnOrderThatChangesAfterItWasFoundAsEmptyIsNotCancelled()
    {
        var store = Store();
        var touched = await SeedOrderAsync(OrderState.Draft, ageHours: 3);
        var gotItem = await SeedOrderAsync(OrderState.Draft, ageHours: 3);
        var billed = await SeedOrderAsync(OrderState.Draft, ageHours: 3);
        var found = await store.FindEmptyDraftsAsync(Timeout, 10, CancellationToken.None);
        Assert.Contains(touched.OrderId, found);
        Assert.Contains(gotItem.OrderId, found);
        Assert.Contains(billed.OrderId, found);

        await Exec("UPDATE orders.orders SET updated_at = now() WHERE order_id = @id;", ("id", touched.OrderId));
        await new PostgresOrderRepository(_source).SaveAsync(
            (await new PostgresOrderRepository(_source).GetByIdAsync(gotItem.OrderId))!.AddItem(
                new OrderItem(Guid.NewGuid(), gotItem.OrderId, await SeedProductAsync(), "Dish", 1m, 100m, 10m, status: OrderItemState.Draft)),
            1);
        await Exec(
            "INSERT INTO billing.bills (bill_id, bill_number, order_id, status, currency_code, opened_at, created_at, updated_at) VALUES (@bill, @number, @id, 'Open', 'TRY', now(), now(), now());",
            ("bill", Guid.NewGuid()), ("number", "BIL-" + billed.OrderId.ToString("N")), ("id", billed.OrderId));

        foreach (var orderId in new[] { touched.OrderId, gotItem.OrderId, billed.OrderId })
        {
            Assert.False(await store.CancelEmptyDraftAsync(orderId, Timeout, CancellationToken.None));
            Assert.Equal("Draft", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", orderId));
        }
    }

    [Fact]
    public async Task TheMenuTellsWhichProductsAllowHalfPortions()
    {
        var halfId = await SeedProductAsync();
        var wholeId = await SeedProductAsync();
        await Exec("UPDATE catalog.products SET allows_half_portion = true WHERE product_id = @id;", ("id", halfId));

        var page = await new DualScreenStore(_source).GetCatalogAsync(null, 100, null, CancellationToken.None);

        Assert.True(page.Items.Single(p => p.ProductId == halfId).AllowsHalfPortion);
        Assert.False(page.Items.Single(p => p.ProductId == wholeId).AllowsHalfPortion);
    }

    [Fact]
    public async Task TheSweeperIsRegisteredInTheHost()
    {
        await using var app = await StartAsync();

        Assert.Single(app.Services.GetServices<IHostedService>().OfType<EmptyOrderSweeper>());
    }

    [Fact]
    public async Task CancellingABillTellsTheCustomerDisplayAndWithTheOrderFlagTheTillQueueToo()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.split", "bills.cancel");
        var plain = await _database.SeedBillAsync();
        var withOrder = await _database.SeedBillAsync();
        var hub = new RecordingHub();
        var announcer = new RecordingAnnouncer();
        await using var app = await StartAsync(hub, announcer);
        using var client = new HttpClient { BaseAddress = AddressOf(app) };

        using var first = await client.SendAsync(CancelBill(terminalId, plain.BillId, cookie, cancelOrder: false));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Single(hub.Pushes, push => push.Group == DualScreenApplication.TerminalGroup(terminalId) && push.Argument.Contains("BillCancelled"));
        Assert.Empty(announcer.Changes);

        using var second = await client.SendAsync(CancelBill(terminalId, withOrder.BillId, cookie, cancelOrder: true));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, hub.Pushes.Count);
        var orderId = await _database.OrderIdOfBillAsync(withOrder.BillId);
        Assert.Equal(("Cancelled", orderId), Assert.Single(announcer.Changes));
    }

    [Fact]
    public async Task CancellingAnOrderTellsTheCustomerDisplayAndTheTillQueue()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier", "bills.cancel");
        var orderId = await _database.SeedOrderWithoutBillAsync();
        var version = (await new PostgresOrderRepository(_source).GetByIdAsync(orderId))!.RowVersion;
        var hub = new RecordingHub();
        var announcer = new RecordingAnnouncer();
        await using var app = await StartAsync(hub, announcer);
        using var client = new HttpClient { BaseAddress = AddressOf(app) };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/cancel");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = JsonContent.Create(new { idempotencyKey = Guid.NewGuid().ToString("N"), expectedRowVersion = version, reason = "Vazgeçti" });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(hub.Pushes, push => push.Group == DualScreenApplication.TerminalGroup(terminalId) && push.Argument.Contains("OrderCancelled"));
        Assert.Equal(("Cancelled", orderId), Assert.Single(announcer.Changes));
    }

    private static HttpRequestMessage CancelBill(Guid terminalId, Guid billId, string cookie, bool cancelOrder)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/cancel");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Content = JsonContent.Create(new { reason = "Müşteri gitti", cancelOrder });
        return request;
    }

    private OrderCleanupStore Store() => new(_source, OrderCancellationStores.Create(_source, new PostgresAuditEventStore(_source)));

    private EmptyOrderSweeper Sweeper() => new(new ServiceCollection().AddSingleton(Store()).BuildServiceProvider(), NullLogger<EmptyOrderSweeper>.Instance);

    private sealed record SeededOrder(Guid OrderId, Guid TableId);

    private async Task<Guid> SeedProductAsync()
    {
        var productId = Guid.NewGuid();
        await Exec(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price) VALUES (@id, @sku, 'Dish', 1, 1, 100);",
            ("id", productId), ("sku", "SKU-" + productId.ToString("N")[..8]));
        return productId;
    }

    /// <summary>An order on its own occupied table; <paramref name="ageHours"/> sets how long ago it was last touched.</summary>
    private async Task<SeededOrder> SeedOrderAsync(
        OrderState status, int ageHours, OrderItemState? item = null, bool withBill = false, OrderSource source = OrderSource.Cashier)
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
        if (item is { } itemState)
            items.Add(new OrderItem(Guid.NewGuid(), orderId, await SeedProductAsync(), "Dish", 1m, 100m, 10m, status: itemState));
        var order = new Order(orderId, source, "ORD-" + orderId.ToString("N"), items, tableId, status: status);
        await new PostgresOrderRepository(_source).AddAsync(order);
        if (withBill)
            await Exec(
                "INSERT INTO billing.bills (bill_id, bill_number, order_id, status, currency_code, opened_at, created_at, updated_at) VALUES (@bill, @number, @id, 'Open', 'TRY', now(), now(), now());",
                ("bill", Guid.NewGuid()), ("number", "BIL-" + orderId.ToString("N")), ("id", orderId));
        await Exec(
            "UPDATE table_mgmt.tables SET current_order_id = @order WHERE table_id = @table; " +
            "UPDATE orders.orders SET created_at = now() - make_interval(hours => @age), updated_at = now() - make_interval(hours => @age) WHERE order_id = @order;",
            ("order", orderId), ("table", tableId), ("age", ageHours));
        return new SeededOrder(orderId, tableId);
    }

    private async Task<WebApplication> StartAsync(RecordingHub? hub = null, RecordingAnnouncer? announcer = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddBillingSplitExperience();
        if (hub is not null)
            builder.Services.AddSingleton<IHubContext<CustomerDisplayHub>>(hub);
        if (announcer is not null)
            builder.Services.AddSingleton<ICashierQueueAnnouncer>(announcer);
        var app = builder.Build();
        app.MapOrderManagementApi();
        app.MapBillingSplitApi();
        await app.StartAsync();
        return app;
    }

    private static Uri AddressOf(WebApplication app)
        => new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());

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

    private sealed class RecordingAnnouncer : ICashierQueueAnnouncer
    {
        public List<(string Change, Guid OrderId)> Changes { get; } = [];

        public Task AnnouncePendingChecksChangedAsync(string change, Guid orderId, Guid? tableId, CancellationToken cancellationToken = default)
        {
            Changes.Add((change, orderId));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHub : IHubContext<CustomerDisplayHub>
    {
        public List<(string Group, string Method, string Argument)> Pushes { get; } = [];

        public IHubClients Clients => new RecordingClients(Pushes);

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class RecordingClients(List<(string Group, string Method, string Argument)> pushes) : IHubClients
    {
        public IClientProxy All => throw new NotSupportedException();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Client(string connectionId) => throw new NotSupportedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IClientProxy Group(string groupName) => new RecordingProxy(groupName, pushes);

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IClientProxy User(string userId) => throw new NotSupportedException();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingProxy(string group, List<(string Group, string Method, string Argument)> pushes) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            pushes.Add((group, method, JsonSerializer.Serialize(args[0])));
            return Task.CompletedTask;
        }
    }
}
