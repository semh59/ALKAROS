using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.PaymentAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.SecurityAdministration.Tests;

/// <summary>The manager's tool for dine-in orders that stayed live without a bill.</summary>
[Collection("Security administration PostgreSQL HTTP")]
public sealed class HangingOrdersHttpTests : IAsyncLifetime
{
    private const string Path = "/api/v1/management/security/orders";

    private readonly SecurityAdministrationTestDatabase _database = new();
    private readonly RecordingAnnouncer _announcer = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton(new HostDatabaseConnection(new NpgsqlConnectionStringBuilder(_database.DataSource.ConnectionString) { Password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD") }.ConnectionString));
        var composition = ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog);
        HostComposition.ApplyComposedModuleServices(builder.Services, composition.Services);
        builder.Services.AddSingleton<ICashierQueueAnnouncer>(_announcer);
        builder.Services.AddSecurityAdministrationExperience();
        _application = builder.Build();
        _application.MapSecurityAdministrationApi();
        await _application.StartAsync();
        _baseAddress = new Uri(Assert.Single(_application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses));
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task BothToolsAreManagerOnly()
    {
        var order = await SeedAsync(OrderState.Submitted, ageHours: 20, KitchenState.Sent);
        using var anonymous = Client(null);
        using var viewOnly = Client(SecurityAdministrationTestDatabase.ViewOnlyManagerToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Path}/hanging")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsJsonAsync($"{Path}/clear-hanging?dryRun=false", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"{Path}/clear-hanging?dryRun=false", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.GetAsync($"{Path}/hanging")).StatusCode);
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(order));
        Assert.Empty(_announcer.Changes);
    }

    [Fact]
    public async Task OnlyOldLiveDineInOrdersWithoutAnActiveBillAreListedWithTheirStockAndWasteLines()
    {
        var hanging = await SeedAsync(OrderState.Submitted, ageHours: 13, KitchenState.Sent, KitchenState.Preparing, KitchenState.Held);
        var cancelledBillOnly = await SeedAsync(OrderState.Accepted, ageHours: 30, billStatus: BillState.Cancelled);
        await SeedAsync(OrderState.Submitted, ageHours: 1);
        await SeedAsync(OrderState.Submitted, ageHours: 40, billStatus: BillState.Open);
        await SeedAsync(OrderState.Submitted, ageHours: 40, billStatus: BillState.Paid);
        await SeedAsync(OrderState.Submitted, ageHours: 40, source: OrderSource.Online);
        await SeedAsync(OrderState.Completed, ageHours: 40);
        await SeedAsync(OrderState.Draft, ageHours: 40);
        using var manager = Client(SecurityAdministrationTestDatabase.ManagerToken);

        var listed = await manager.GetFromJsonAsync<JsonElement>($"{Path}/hanging");

        Assert.Equal(2, listed.GetArrayLength());
        var first = listed.EnumerateArray().Single(o => o.GetProperty("orderId").GetGuid() == hanging);
        Assert.Equal(2, first.GetProperty("stockLines").GetInt32());
        Assert.Equal(1, first.GetProperty("wasteLines").GetInt32());
        Assert.Contains(listed.EnumerateArray(), o => o.GetProperty("orderId").GetGuid() == cancelledBillOnly);
        var shorter = await manager.GetFromJsonAsync<JsonElement>($"{Path}/hanging?olderThanHours=24");
        Assert.Equal(1, shorter.GetArrayLength());
    }

    [Fact]
    public async Task ClearingIsADryRunByDefaultAndTheRealRunNeedsAReason()
    {
        var order = await SeedAsync(OrderState.Submitted, ageHours: 20, KitchenState.Sent);
        using var manager = Client(SecurityAdministrationTestDatabase.ManagerToken);

        using var dry = await manager.PostAsJsonAsync($"{Path}/clear-hanging", new { reason = "Eski" });
        var dryBody = await dry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(dryBody.GetProperty("dryRun").GetBoolean());
        Assert.Equal(1, dryBody.GetProperty("orders").GetArrayLength());
        Assert.Equal(0, dryBody.GetProperty("results").GetArrayLength());

        foreach (var reason in new[] { "", "   ", new string('x', 501) })
        {
            using var refused = await manager.PostAsJsonAsync($"{Path}/clear-hanging?dryRun=false", new { reason, orderIds = new[] { order } });
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("REASON_REQUIRED", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString());
        }

        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(order));
        Assert.Equal(0, await _database.SystemAuditCountAsync("orders.hanging.clear-started", OrderBacklogAdministration.AuditAggregateId));
    }

    [Fact]
    public async Task TheRealRunCancelsEveryHangingOrderAuditsItAndASecondRunFindsNothing()
    {
        var first = await SeedAsync(OrderState.Submitted, ageHours: 20, KitchenState.Sent, KitchenState.Preparing);
        var second = await SeedAsync(OrderState.Accepted, ageHours: 50, billStatus: BillState.Cancelled);
        var recent = await SeedAsync(OrderState.Submitted, ageHours: 2);
        using var manager = Client(SecurityAdministrationTestDatabase.ManagerToken);

        using var real = await manager.PostAsJsonAsync(
            $"{Path}/clear-hanging?dryRun=false", new { reason = "Eski hayalet sipariş", orderIds = new[] { first, second } });
        var body = await real.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, real.StatusCode);
        Assert.All(body.GetProperty("results").EnumerateArray(), r => Assert.Equal("Cleared", r.GetProperty("outcome").GetString()));
        Assert.Equal(2, body.GetProperty("results").GetArrayLength());
        Assert.Equal("Cancelled", await _database.OrderStatusOfAsync(first));
        Assert.Equal("Cancelled", await _database.OrderStatusOfAsync(second));
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(recent));
        Assert.Equal(2, await _database.SystemAuditCountAsync("order.cancelled", first) + await _database.SystemAuditCountAsync("order.cancelled", second));
        Assert.Equal(1, await _database.SystemAuditCountAsync("orders.hanging.clear-started", OrderBacklogAdministration.AuditAggregateId));
        Assert.Equal(1, await _database.SystemAuditCountAsync("orders.hanging.cleared", OrderBacklogAdministration.AuditAggregateId));
        Assert.Equal(new[] { first, second }.Order(), _announcer.Changes.Where(c => c.Change == "Cancelled").Select(c => c.OrderId).Order());
        Assert.Equal(("User", "Eski hayalet sipariş"), await AuditActorAndReasonAsync("order.cancelled", first));
        Assert.Equal(("User", "Eski hayalet sipariş"), await AuditActorAndReasonAsync("orders.hanging.cleared", OrderBacklogAdministration.AuditAggregateId));

        var again = await manager.GetFromJsonAsync<JsonElement>($"{Path}/hanging");
        Assert.Equal(0, again.GetArrayLength());
    }

    [Fact]
    public async Task OnlyTheConfirmedOrdersAreCancelledNotOnesThatAgedInSinceTheDryRun()
    {
        var confirmed = await SeedAsync(OrderState.Submitted, ageHours: 20, KitchenState.Sent);
        var agedIn = await SeedAsync(OrderState.Submitted, ageHours: 21, KitchenState.Sent);
        var gone = Guid.NewGuid();
        using var manager = Client(SecurityAdministrationTestDatabase.ManagerToken);

        using var real = await manager.PostAsJsonAsync(
            $"{Path}/clear-hanging?dryRun=false", new { reason = "Eski hayalet sipariş", orderIds = new[] { confirmed, gone } });
        var body = await real.Content.ReadFromJsonAsync<JsonElement>();

        var outcomes = body.GetProperty("results").EnumerateArray()
            .ToDictionary(r => r.GetProperty("orderId").GetGuid(), r => r.GetProperty("outcome").GetString());
        Assert.Equal(2, outcomes.Count);
        Assert.Equal("Cleared", outcomes[confirmed]);
        Assert.Equal("Changed", outcomes[gone]);
        Assert.Equal("Cancelled", await _database.OrderStatusOfAsync(confirmed));
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(agedIn));

        foreach (var unconfirmedBody in new object[] { new { reason = "Eski hayalet sipariş" }, new { reason = "Eski hayalet sipariş", orderIds = Array.Empty<Guid>() } })
        {
            using var unconfirmed = await manager.PostAsJsonAsync($"{Path}/clear-hanging?dryRun=false", unconfirmedBody);
            Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
            Assert.Equal("ORDERS_NOT_CONFIRMED", (await unconfirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString());
        }

        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(agedIn));
    }

    [Fact]
    public async Task TheAgeFloorKeepsRecentKitchenOrdersOutEvenWhenAShorterAgeIsAsked()
    {
        var recent = await SeedAsync(OrderState.Submitted, ageHours: 3, KitchenState.Preparing);
        var old = await SeedAsync(OrderState.Submitted, ageHours: 7, KitchenState.Sent);
        using var manager = Client(SecurityAdministrationTestDatabase.ManagerToken);

        var listed = await manager.GetFromJsonAsync<JsonElement>($"{Path}/hanging?olderThanHours=1");

        Assert.Equal(old, Assert.Single(listed.EnumerateArray()).GetProperty("orderId").GetGuid());
        Assert.DoesNotContain(listed.EnumerateArray(), o => o.GetProperty("orderId").GetGuid() == recent);
    }

    [Fact]
    public async Task EachOrderIsAnsweredOnItsOwnWhenMoneyOrAServedLineStopsIt()
    {
        var withPayment = await SeedAsync(OrderState.Submitted, ageHours: 20, billStatus: BillState.Cancelled, pendingPayment: true);
        var served = await SeedAsync(OrderState.Submitted, ageHours: 21, KitchenState.Served);
        var clean = await SeedAsync(OrderState.Submitted, ageHours: 22, KitchenState.Sent);
        using var manager = Client(SecurityAdministrationTestDatabase.ManagerToken);

        using var real = await manager.PostAsJsonAsync(
            $"{Path}/clear-hanging?dryRun=false", new { reason = "Temizlik", orderIds = new[] { withPayment, served, clean } });
        var results = (await real.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results").EnumerateArray()
            .ToDictionary(r => r.GetProperty("orderId").GetGuid(), r => r.GetProperty("outcome").GetString());

        Assert.Equal("HasPayment", results[withPayment]);
        Assert.Equal("HasServedItems", results[served]);
        Assert.Equal("Cleared", results[clean]);
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(withPayment));
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(served));
        Assert.Equal("Cancelled", await _database.OrderStatusOfAsync(clean));
        Assert.Equal(clean, Assert.Single(_announcer.Changes).OrderId);
    }

    [Fact]
    public async Task AnOrderTouchedAfterTheIntentWasRecordedIsReportedAsChangedAndLeftAlone()
    {
        var order = await SeedAsync(OrderState.Submitted, ageHours: 20, KitchenState.Sent);
        await using (var install = _database.DataSource.CreateCommand(
            """
            CREATE OR REPLACE FUNCTION audit.test_touch_order() RETURNS trigger AS $fn$
            BEGIN
                IF NEW.event_name = 'orders.hanging.clear-started' THEN
                    UPDATE orders.orders SET created_at = now(), updated_at = now() WHERE order_id = 'TOUCHED'::uuid;
                END IF;
                RETURN NEW;
            END;
            $fn$ LANGUAGE plpgsql;
            CREATE TRIGGER test_touch_order BEFORE INSERT ON audit.audit_events FOR EACH ROW EXECUTE FUNCTION audit.test_touch_order();
            """.Replace("TOUCHED", order.ToString("D"))))
            await install.ExecuteNonQueryAsync();
        using var manager = Client(SecurityAdministrationTestDatabase.ManagerToken);

        using var real = await manager.PostAsJsonAsync($"{Path}/clear-hanging?dryRun=false", new { reason = "Temizlik", orderIds = new[] { order } });
        var body = await real.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Changed", Assert.Single(body.GetProperty("results").EnumerateArray()).GetProperty("outcome").GetString());
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(order));
        Assert.Empty(_announcer.Changes);
    }

    [Fact]
    public async Task WhenTheIntentCannotBeRecordedNoOrderIsTouched()
    {
        var order = await SeedAsync(OrderState.Submitted, ageHours: 20, KitchenState.Sent);
        await using (var install = _database.DataSource.CreateCommand(
            """
            CREATE OR REPLACE FUNCTION audit.test_refuse_hanging() RETURNS trigger AS $fn$
            BEGIN
                IF NEW.event_name = 'orders.hanging.clear-started' THEN RAISE EXCEPTION 'refused by the test'; END IF;
                RETURN NEW;
            END;
            $fn$ LANGUAGE plpgsql;
            CREATE TRIGGER test_refuse_hanging BEFORE INSERT ON audit.audit_events FOR EACH ROW EXECUTE FUNCTION audit.test_refuse_hanging();
            """))
            await install.ExecuteNonQueryAsync();
        using var manager = Client(SecurityAdministrationTestDatabase.ManagerToken);

        using var real = await manager.PostAsJsonAsync($"{Path}/clear-hanging?dryRun=false", new { reason = "Temizlik", orderIds = new[] { order } });

        Assert.Equal(HttpStatusCode.InternalServerError, real.StatusCode);
        Assert.Equal("Submitted", await _database.OrderStatusOfAsync(order));
        Assert.Empty(_announcer.Changes);
        Assert.Equal(0, await _database.SystemAuditCountAsync("orders.hanging.cleared", OrderBacklogAdministration.AuditAggregateId));
    }

    private async Task<(string? ActorType, string? Reason)> AuditActorAndReasonAsync(string eventName, Guid aggregateId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT actor_type, reason FROM audit.audit_events WHERE event_name = @e AND aggregate_id = @a;");
        command.Parameters.AddWithValue("e", eventName);
        command.Parameters.AddWithValue("a", aggregateId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetString(1));
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

    private HttpClient Client(string? managerToken)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (managerToken is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{SecurityAdministrationEndpoints.ManagerCookieName}={managerToken}");
        return client;
    }

    /// <summary>An order on its own table, one line per given kitchen state, last touched <paramref name="ageHours"/> ago.</summary>
    private async Task<Guid> SeedAsync(
        OrderState status, int ageHours, params KitchenState[] lines)
        => await SeedAsync(status, ageHours, null, OrderSource.Waiter, false, lines);

    private Task<Guid> SeedAsync(OrderState status, int ageHours, BillState billStatus)
        => SeedAsync(status, ageHours, billStatus, OrderSource.Waiter, false);

    private Task<Guid> SeedAsync(OrderState status, int ageHours, BillState billStatus, bool pendingPayment)
        => SeedAsync(status, ageHours, billStatus, OrderSource.Waiter, pendingPayment);

    private Task<Guid> SeedAsync(OrderState status, int ageHours, OrderSource source)
        => SeedAsync(status, ageHours, null, source, false);

    private async Task<Guid> SeedAsync(
        OrderState status, int ageHours, BillState? billStatus, OrderSource source, bool pendingPayment, params KitchenState[] lines)
    {
        var orderId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await using (var table = _database.DataSource.CreateCommand(
            "INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status) VALUES (@id, @number, 4, true, 'Occupied');"))
        {
            table.Parameters.AddWithValue("id", tableId);
            table.Parameters.AddWithValue("number", "HG-" + tableId.ToString("N")[..6]);
            await table.ExecuteNonQueryAsync();
        }

        var items = new List<OrderItem>();
        foreach (var state in lines.DefaultIfEmpty(KitchenState.Sent))
            items.Add(new OrderItem(
                Guid.NewGuid(), orderId, await ProductAsync(), "Dish", 1m, 50m, 0m, status: OrderItemState.Active, kitchenState: state));
        var order = new Order(orderId, source, "ORD-" + orderId.ToString("N")[..12], items, tableId: tableId, status: status);
        await new PostgresOrderRepository(_database.DataSource).AddAsync(order);
        await using (var age = _database.DataSource.CreateCommand(
            "UPDATE orders.orders SET created_at = now() - make_interval(hours => @age), updated_at = now() - make_interval(hours => @age) WHERE order_id = @id;"))
        {
            age.Parameters.AddWithValue("age", ageHours);
            age.Parameters.AddWithValue("id", orderId);
            await age.ExecuteNonQueryAsync();
        }

        if (billStatus is { } billState)
        {
            var billId = Guid.NewGuid();
            await new PostgresBillRepository(_database.DataSource).AddAsync(new Bill(
                billId, "BILL-" + billId.ToString("N")[..8], [BillItem.FromOrderItem(billId, order.Items[0])],
                tableId: tableId, orderId: orderId, status: billState, currencyCode: "TRY"));
            if (pendingPayment)
                await new PostgresPaymentRepository(_database.DataSource).AddAsync(new Payment(Guid.NewGuid(), billId, 50m).Tender(50m));
        }

        return orderId;
    }

    private async Task<Guid> ProductAsync()
    {
        var productId = Guid.NewGuid();
        await using var command = _database.DataSource.CreateCommand(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price) VALUES (@id, @sku, 'Dish', 1, 1, 50);");
        command.Parameters.AddWithValue("id", productId);
        command.Parameters.AddWithValue("sku", "SKU-" + productId.ToString("N")[..8]);
        await command.ExecuteNonQueryAsync();
        return productId;
    }
}
