namespace ALKAROS.Orders.OrderAggregate.Tests;

using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.OrderAggregate.Tests.Fixtures;
using Npgsql;
using Xunit;

/// <summary>
/// Integration tests that exercise the order repository and the orders
/// migration constraints (canonical status/kitchen/source checks, FKs,
/// snapshot persistence) against a real database created from 006-catalog,
/// 010-tables and 011-orders.
/// </summary>
public sealed class PostgresOrderTests : IClassFixture<OrdersTestDatabase>
{
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private readonly PostgresOrderRepository _orders;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresOrderTests(OrdersTestDatabase database)
    {
        _dataSource = database.DataSource;
        _orders = new PostgresOrderRepository(database.DataSource);
    }

    [Fact]
    public async Task OrderRoundTripPersistsAllPdfFields()
    {
        var product = await SeedProduct();
        var order = NewOrder(product);

        await _orders.AddAsync(order);

        var loaded = await _orders.GetByIdAsync(order.Id);
        Assert.NotNull(loaded);
        Assert.Equal(order.Id, loaded.Id);
        Assert.Equal(OrderSource.Waiter, loaded.Source);
        Assert.Equal(order.OrderNumber, loaded.OrderNumber);
        Assert.Equal(OrderState.Draft, loaded.Status);
        Assert.Equal(ConfirmationStatus.NotRequired, loaded.ConfirmationStatus);
        Assert.Null(loaded.TableId);
        Assert.Null(loaded.CustomerId);
        Assert.Equal(1, loaded.RowVersion);

        var item = Assert.Single(loaded.Items);
        Assert.Equal(product, item.ProductId);
        Assert.Equal("Lahmacun", item.ProductNameSnapshot);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(120m, item.UnitPrice);
        Assert.Equal(OrderItemState.Draft, item.Status);
        Assert.Equal(KitchenState.NotSent, item.KitchenState);
    }

    [Fact]
    public async Task OrderWithModifiersAndHistoryRoundTrips()
    {
        var product = await SeedProduct();
        var modifierGroupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        await SeedModifier(modifierGroupId, modifierId, "Cheese Group", "Extra Cheese");
        var modifier = new OrderItemModifier(Guid.NewGuid(), Guid.NewGuid(), modifierId, "Extra Cheese", 15m);
        var item = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), product, "Pizza", 1, 200m, 10m, modifiers: [modifier]);
        var order = new Order(Guid.NewGuid(), OrderSource.Qr, UniqueNumber(), [item]);
        var changed = order.TransitionTo(OrderState.Submitted, changedBy: Guid.NewGuid());

        await _orders.AddAsync(changed);

        var loaded = await _orders.GetByIdAsync(order.Id);
        Assert.NotNull(loaded);
        Assert.Equal(OrderState.Submitted, loaded.Status);
        Assert.Equal(ConfirmationStatus.NotRequired, loaded.ConfirmationStatus);
        var loadedItem = Assert.Single(loaded.Items);
        var loadedModifier = Assert.Single(loadedItem.Modifiers);
        Assert.Equal("Extra Cheese", loadedModifier.ModifierNameSnapshot);
        Assert.Equal(15m, loadedModifier.PriceDelta);
        Assert.Single(loaded.History);
        Assert.Equal(OrderState.Draft, loaded.History[0].OldStatus);
        Assert.Equal(OrderState.Submitted, loaded.History[0].NewStatus);
    }

    [Fact]
    public async Task SaveUpdatesOrderAndAppendsHistoryGuardedByRowVersion()
    {
        var product = await SeedProduct();
        var order = NewOrder(product);
        await _orders.AddAsync(order);

        var loaded = await _orders.GetByIdAsync(order.Id);
        Assert.NotNull(loaded);
        var submitted = loaded.TransitionTo(OrderState.Submitted);
        var accepted = submitted.TransitionTo(OrderState.PendingConfirmation).TransitionTo(OrderState.Accepted);

        var newVersion = await _orders.SaveAsync(accepted, loaded.RowVersion);

        Assert.Equal(loaded.RowVersion + 1, newVersion);
        var reloaded = await _orders.GetByIdAsync(order.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(OrderState.Accepted, reloaded.Status);
        Assert.Equal(ConfirmationStatus.Accepted, reloaded.ConfirmationStatus);
        Assert.Equal(newVersion, reloaded.RowVersion);
        Assert.Equal(3, reloaded.History.Count);
    }

    [Fact]
    public async Task StaleRowVersionSaveRejected()
    {
        var product = await SeedProduct();
        var order = NewOrder(product);
        await _orders.AddAsync(order);

        var act = () => _orders.SaveAsync(order.TransitionTo(OrderState.Submitted), expectedRowVersion: 5);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("concurrent modification", exception.Message);
    }

    [Fact]
    public async Task SnapshotPersistedAndUnaffectedByCatalogChange()
    {
        var product = await SeedProduct();
        var order = NewOrder(product);
        await _orders.AddAsync(order);

        await using (var command = _dataSource.CreateCommand(
            "UPDATE catalog.products SET name = @name, sku = @sku WHERE product_id = @product_id;"))
        {
            command.Parameters.AddWithValue("name", "Yeniden Adlandırıldı");
            command.Parameters.AddWithValue("sku", "NEW-SKU");
            command.Parameters.AddWithValue("product_id", product);
            await command.ExecuteNonQueryAsync();
        }

        var loaded = await _orders.GetByIdAsync(order.Id);
        Assert.NotNull(loaded);
        var item = Assert.Single(loaded.Items);
        Assert.Equal("Lahmacun", item.ProductNameSnapshot);
        Assert.Equal("LAH-001", item.SkuSnapshot);
        Assert.Equal(120m, item.UnitPrice);
    }

    [Fact]
    public async Task InvalidOrderStatusCheckRejected()
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO orders.orders (order_id, source, status, confirmation_status, order_number, created_at, updated_at)
            VALUES (@order_id, @source, @status, @confirmation_status, @order_number, now(), now());
            """);
        command.Parameters.AddWithValue("order_id", Guid.NewGuid());
        command.Parameters.AddWithValue("source", OrderSource.Waiter.ToString());
        command.Parameters.AddWithValue("status", "Bogus");
        command.Parameters.AddWithValue("confirmation_status", ConfirmationStatus.NotRequired.ToString());
        command.Parameters.AddWithValue("order_number", "ORD-BOGUS");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task InvalidConfirmationStatusCheckRejected()
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO orders.orders (order_id, source, status, confirmation_status, order_number, created_at, updated_at)
            VALUES (@order_id, @source, @status, @confirmation_status, @order_number, now(), now());
            """);
        command.Parameters.AddWithValue("order_id", Guid.NewGuid());
        command.Parameters.AddWithValue("source", OrderSource.Waiter.ToString());
        command.Parameters.AddWithValue("status", OrderState.Draft.ToString());
        command.Parameters.AddWithValue("confirmation_status", "Bogus");
        command.Parameters.AddWithValue("order_number", "ORD-CONF");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task InvalidItemStatusCheckRejected()
    {
        var product = await SeedProduct();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO orders.order_items (
                order_item_id, order_id, product_id, product_name_snapshot, quantity, unit_price,
                tax_rate, net_amount, gross_amount, status, kitchen_state, portion_reservation_status,
                created_at, updated_at)
            VALUES (@order_item_id, @order_id, @product_id, @product_name_snapshot, @quantity, @unit_price,
                    @tax_rate, @net_amount, @gross_amount, @status, @kitchen_state, @portion_reservation_status,
                    now(), now());
            """);
        var orderId = Guid.NewGuid();
        command.Parameters.AddWithValue("order_item_id", Guid.NewGuid());
        command.Parameters.AddWithValue("order_id", orderId);
        command.Parameters.AddWithValue("product_id", product);
        command.Parameters.AddWithValue("product_name_snapshot", "X");
        command.Parameters.AddWithValue("quantity", 1m);
        command.Parameters.AddWithValue("unit_price", 10m);
        command.Parameters.AddWithValue("tax_rate", 10m);
        command.Parameters.AddWithValue("net_amount", 10m);
        command.Parameters.AddWithValue("gross_amount", 11m);
        command.Parameters.AddWithValue("status", "Bogus");
        command.Parameters.AddWithValue("kitchen_state", KitchenState.NotSent.ToString());
        command.Parameters.AddWithValue("portion_reservation_status", PortionReservationStatus.NotApplicable.ToString());

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task UnknownProductForeignKeyRejected()
    {
        var item = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hayalet", 1, 10m, 10m);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, UniqueNumber(), [item]);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => _orders.AddAsync(order));
        Assert.Equal(ForeignKeyViolation, exception.SqlState);
    }

    [Fact]
    public async Task SaveActivatesVoidedItemAndKeepsCancellation()
    {
        var product = await SeedProduct();
        var item = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), product, "Kazandibi", 1, 40m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.NotSent);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, UniqueNumber(), [item]);
        await _orders.AddAsync(order);

        var loaded = await _orders.GetByIdAsync(order.Id);
        Assert.NotNull(loaded);
        var cancelled = loaded.CancelItem(loaded.Items.Single().Id, reason: "customer changed mind");
        var newVersion = await _orders.SaveAsync(cancelled, loaded.RowVersion);

        var reloaded = await _orders.GetByIdAsync(order.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(OrderItemState.Cancelled, Assert.Single(reloaded.Items).Status);
        Assert.Equal(KitchenState.Cancelled, Assert.Single(reloaded.Items).KitchenState);
        Assert.Equal(newVersion, reloaded.RowVersion);
    }

    private async Task<Guid> SeedProduct()
    {
        var productId = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, @name, @product_type, @stock_mode, @current_price);
            """);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("sku", "LAH-" + Guid.NewGuid().ToString("N")[..8]);
        command.Parameters.AddWithValue("name", "Lahmacun");
        command.Parameters.AddWithValue("product_type", 1);
        command.Parameters.AddWithValue("stock_mode", 1);
        command.Parameters.AddWithValue("current_price", 120m);
        await command.ExecuteNonQueryAsync();
        return productId;
    }

    private async Task SeedModifier(Guid groupId, Guid modifierId, string groupName, string modifierName)
    {
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type)
            VALUES (@modifier_group_id, @code, @name, @selection_type);
            """))
        {
            command.Parameters.AddWithValue("modifier_group_id", groupId);
            command.Parameters.AddWithValue("code", "GRP-" + Guid.NewGuid().ToString("N")[..8]);
            command.Parameters.AddWithValue("name", groupName);
            command.Parameters.AddWithValue("selection_type", 1);
            await command.ExecuteNonQueryAsync();
        }

        await using var command2 = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta)
            VALUES (@modifier_id, @modifier_group_id, @code, @name, @price_delta);
            """);
        command2.Parameters.AddWithValue("modifier_id", modifierId);
        command2.Parameters.AddWithValue("modifier_group_id", groupId);
        command2.Parameters.AddWithValue("code", "MOD-" + modifierId.ToString("N")[..8]);
        command2.Parameters.AddWithValue("name", modifierName);
        command2.Parameters.AddWithValue("price_delta", 15m);
        await command2.ExecuteNonQueryAsync();
    }

    private static Order NewOrder(Guid productId)
    {
        var item = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), productId, "Lahmacun", 2, 120m, 10m, skuSnapshot: "LAH-001");
        return new Order(Guid.NewGuid(), OrderSource.Waiter, UniqueNumber(), [item]);
    }

    private static string UniqueNumber()
        => "ORD-" + Guid.NewGuid().ToString("N")[..8];
}

/// <summary>
/// Validates the rollback direction of the 011 migration against its own
/// freshly created database; the FK added on table_mgmt.tables must be
/// dropped first so the schema drop succeeds.
/// </summary>
public sealed class PostgresOrderDownSqlTests : IClassFixture<OrdersTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresOrderDownSqlTests(OrdersTestDatabase database)
    {
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task DownSqlDropsTablesConstraintAndSchema()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "011-orders.down.sql"));

        await using var command = _dataSource.CreateCommand(downSql);
        await command.ExecuteNonQueryAsync();

        await using var after = _dataSource.CreateCommand(
            """
            SELECT to_regclass('orders.orders'),
                   to_regclass('orders.order_items'),
                   to_regclass('orders.order_item_modifiers'),
                   to_regclass('orders.order_status_history');
            """);
        await using var reader = await after.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.IsDBNull(0));
        Assert.True(reader.IsDBNull(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));

        await using var constraint = _dataSource.CreateCommand(
            """
            SELECT conname FROM pg_constraint
            JOIN pg_class ON conrelid = pg_class.oid
            JOIN pg_namespace ON pg_class.relnamespace = pg_namespace.oid
            WHERE conname = 'fk_tables_current_order' AND pg_namespace.nspname = 'table_mgmt';
            """);
        var rows = await constraint.ExecuteScalarAsync();
        Assert.Null(rows);
    }
}