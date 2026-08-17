using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.BillFoundation.Tests.Fixtures;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using Xunit;

namespace ALKAROS.Billing.BillFoundation.Tests;

/// <summary>
/// Integration tests for PostgresBillRepository and 019-billing schema.
/// Exercises CRUD, multi-order sourcing, order splitting, and double-billing unique constraints.
/// </summary>
public sealed class PostgresBillTests : IClassFixture<BillingTestDatabase>
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";

    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBillTests(BillingTestDatabase database)
    {
        _dataSource = database.DataSource;
        _bills = new PostgresBillRepository(database.DataSource);
        _orders = new PostgresOrderRepository(database.DataSource);
    }

    [Fact]
    public async Task BillRoundTripPersistsAndLoadsAllFields()
    {
        var product = await SeedProduct("Doner", 180m);
        var table = await SeedTable("Masa 1");
        var order = await CreateAndSaveOrder(product, "Doner", 180m, table);

        var billId = Guid.NewGuid();
        var billNumber = UniqueBillNumber();
        var orderItem = order.Items[0];

        var billItem = BillItem.FromOrderItem(billId, orderItem, notes: "Az acili");
        var bill = new Bill(
            id: billId,
            billNumber: billNumber,
            items: new[] { billItem },
            tableId: table,
            orderId: order.Id,
            status: BillState.Open,
            currencyCode: "TRY");

        await _bills.AddAsync(bill);

        var loaded = await _bills.GetByIdAsync(billId);
        Assert.NotNull(loaded);
        Assert.Equal(billId, loaded.Id);
        Assert.Equal(billNumber, loaded.BillNumber);
        Assert.Equal(table, loaded.TableId);
        Assert.Equal(order.Id, loaded.OrderId);
        Assert.Equal(BillState.Open, loaded.Status);
        Assert.Equal("TRY", loaded.CurrencyCode);
        Assert.Equal(1, loaded.RowVersion);

        var loadedItem = Assert.Single(loaded.Items);
        Assert.Equal(billItem.Id, loadedItem.Id);
        Assert.Equal(billId, loadedItem.BillId);
        Assert.Equal(orderItem.Id, loadedItem.OrderItemId);
        Assert.Equal(product, loadedItem.ProductId);
        Assert.Equal("Doner", loadedItem.ProductNameSnapshot);
        Assert.Equal(orderItem.Quantity, loadedItem.Quantity);
        Assert.Equal(180m, loadedItem.UnitPrice);
        Assert.Equal(BillLineType.Sale, loadedItem.LineType);
        Assert.Equal("Az acili", loadedItem.Notes);
        Assert.Equal(loadedItem.GrossAmount, loaded.PayableAmount);

        // Also test GetByBillNumberAsync
        var byNumber = await _bills.GetByBillNumberAsync(billNumber);
        Assert.NotNull(byNumber);
        Assert.Equal(billId, byNumber.Id);
    }

    [Fact]
    public async Task MultiOrderSourcingPersistsAndAggregatesCorrectly()
    {
        // Positive 1 (V0-DOM-002): Merge items from Order A and Order B onto one Bill
        var product1 = await SeedProduct("Pide", 150m);
        var product2 = await SeedProduct("Kola", 40m);
        var table = await SeedTable("Masa 2");

        var order1 = await CreateAndSaveOrder(product1, "Pide", 150m, table);
        var order2 = await CreateAndSaveOrder(product2, "Kola", 40m, table);

        var mergedBillId = Guid.NewGuid();
        var mergedBill = BillSourceOperations.CreateMergedBill(
            billId: mergedBillId,
            billNumber: UniqueBillNumber(),
            orderItems: new[] { order1.Items[0], order2.Items[0] },
            tableId: table);

        await _bills.AddAsync(mergedBill);

        var loaded = await _bills.GetByIdAsync(mergedBillId);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Items.Count);
        Assert.Null(loaded.OrderId); // Merged bill has no single origin order dominance

        // Verify total payable equals sum of items from both orders:
        // Order1 item (150 net * 1.10 = 165) + Order2 item (40 net * 1.10 = 44) = 209
        var expectedTotal = order1.Items[0].GrossAmount + order2.Items[0].GrossAmount;
        Assert.Equal(expectedTotal, loaded.PayableAmount);
    }

    [Fact]
    public async Task SplitOrderPartitionAcrossBillsPersistsLosslessly()
    {
        // Positive 2 (V0-DOM-002): Split one Order across 2 Bills
        var product1 = await SeedProduct("Corba", 70m);
        var product2 = await SeedProduct("Kofte", 220m);
        var product3 = await SeedProduct("Tatli", 90m);
        var table = await SeedTable("Masa 3");

        var item1 = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), product1, "Corba", 1, 70m, 10m);
        var item2 = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), product2, "Kofte", 1, 220m, 10m);
        var item3 = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), product3, "Tatli", 1, 90m, 10m);

        var order = new Order(
            id: Guid.NewGuid(),
            source: OrderSource.Waiter,
            orderNumber: "ORD-" + Guid.NewGuid().ToString("N")[..8],
            items: new[] { item1, item2, item3 },
            tableId: table);
        await _orders.AddAsync(order);

        var billIdA = Guid.NewGuid();
        var billIdB = Guid.NewGuid();
        var partitions = new[]
        {
            (billIdA, UniqueBillNumber(), (IReadOnlyList<Guid>)new[] { item1.Id }),
            (billIdB, UniqueBillNumber(), (IReadOnlyList<Guid>)new[] { item2.Id, item3.Id })
        };

        var splitBills = BillSourceOperations.CreateSplitBills(order, partitions);

        await _bills.AddAsync(splitBills[0]);
        await _bills.AddAsync(splitBills[1]);

        var loadedA = await _bills.GetByIdAsync(billIdA);
        var loadedB = await _bills.GetByIdAsync(billIdB);

        Assert.NotNull(loadedA);
        Assert.NotNull(loadedB);
        Assert.Single(loadedA.Items);
        Assert.Equal(2, loadedB.Items.Count);

        // Sum of both bills equals original order total
        Assert.Equal(order.Total, loadedA.PayableAmount + loadedB.PayableAmount);
    }

    [Fact]
    public async Task DoubleBillingOrderItemThrowsPostgresUniqueViolation()
    {
        // Negative 2 (V0-DOM-002): An order_item billed twice must be rejected by global unique constraint
        var product = await SeedProduct("Kavurma", 300m);
        var order = await CreateAndSaveOrder(product, "Kavurma", 300m);
        var orderItem = order.Items[0];

        var bill1 = new Bill(
            Guid.NewGuid(),
            UniqueBillNumber(),
            new[] { BillItem.FromOrderItem(Guid.NewGuid(), orderItem) });
        await _bills.AddAsync(bill1);

        // Verify IsOrderItemBilledAsync returns true
        var isBilled = await _bills.IsOrderItemBilledAsync(orderItem.Id);
        Assert.True(isBilled);

        // Attempting to attach the same orderItem to bill2 must fail with unique constraint violation
        var bill2 = new Bill(
            Guid.NewGuid(),
            UniqueBillNumber(),
            new[] { BillItem.FromOrderItem(Guid.NewGuid(), orderItem) });

        var exception = await Assert.ThrowsAsync<PostgresException>(() => _bills.AddAsync(bill2));
        Assert.Equal(UniqueViolation, exception.SqlState);
    }

    [Fact]
    public async Task OptimisticConcurrencyThrowsOnStaleRowVersion()
    {
        var product = await SeedProduct("Manti", 160m);
        var order = await CreateAndSaveOrder(product, "Manti", 160m);

        var bill = new Bill(
            Guid.NewGuid(),
            UniqueBillNumber(),
            new[] { BillItem.FromOrderItem(Guid.NewGuid(), order.Items[0]) });
        await _bills.AddAsync(bill);

        var loaded = await _bills.GetByIdAsync(bill.Id);
        Assert.NotNull(loaded);

        // First save increments row version to 2
        var cancelled = loaded.Cancel();
        var newVersion = await _bills.SaveAsync(cancelled, expectedRowVersion: 1);
        Assert.Equal(2, newVersion);

        // Second save with stale expectedRowVersion 1 throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bills.SaveAsync(cancelled, expectedRowVersion: 1));
    }

    [Fact]
    public async Task ForeignKeysEnforced()
    {
        var product = await SeedProduct("Ayran", 20m);
        var order = await CreateAndSaveOrder(product, "Ayran", 20m);

        // Unknown Table ID
        var billWithBadTable = new Bill(Guid.NewGuid(), UniqueBillNumber(), tableId: Guid.NewGuid());
        var ex1 = await Assert.ThrowsAsync<PostgresException>(() => _bills.AddAsync(billWithBadTable));
        Assert.Equal(ForeignKeyViolation, ex1.SqlState);

        // Unknown Order ID
        var billWithBadOrder = new Bill(Guid.NewGuid(), UniqueBillNumber(), orderId: Guid.NewGuid());
        var ex2 = await Assert.ThrowsAsync<PostgresException>(() => _bills.AddAsync(billWithBadOrder));
        Assert.Equal(ForeignKeyViolation, ex2.SqlState);

        // Unknown Product ID in BillItem
        var badItem = new BillItem(Guid.NewGuid(), Guid.NewGuid(), order.Items[0].Id, Guid.NewGuid(), "Fake", 1, 10m, 10m);
        var billWithBadItem = new Bill(Guid.NewGuid(), UniqueBillNumber(), new[] { badItem });
        var ex3 = await Assert.ThrowsAsync<PostgresException>(() => _bills.AddAsync(billWithBadItem));
        Assert.Equal(ForeignKeyViolation, ex3.SqlState);

        // Unknown OrderItem ID in BillItem
        var badOrderItem = new BillItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), product, "Fake", 1, 10m, 10m);
        var billWithBadOrderItem = new Bill(Guid.NewGuid(), UniqueBillNumber(), new[] { badOrderItem });
        var ex4 = await Assert.ThrowsAsync<PostgresException>(() => _bills.AddAsync(billWithBadOrderItem));
        Assert.Equal(ForeignKeyViolation, ex4.SqlState);
    }

    [Fact]
    public async Task CheckConstraintsEnforced()
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO billing.bills (
                bill_id, bill_number, status, opened_at, created_at, updated_at)
            VALUES (
                @bill_id, @bill_number, 'BogusStatus', NOW(), NOW(), NOW());
            """);
        command.Parameters.AddWithValue("bill_id", Guid.NewGuid());
        command.Parameters.AddWithValue("bill_number", UniqueBillNumber());

        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task SaveAsyncAddsAndRemovesItemsCorrectly()
    {
        var product1 = await SeedProduct("Tavuk Sis", 140m);
        var product2 = await SeedProduct("Salata", 50m);
        var order1 = await CreateAndSaveOrder(product1, "Tavuk Sis", 140m);
        var order2 = await CreateAndSaveOrder(product2, "Salata", 50m);

        var billId = Guid.NewGuid();
        var item1 = BillItem.FromOrderItem(billId, order1.Items[0]);
        var bill = new Bill(billId, UniqueBillNumber(), new[] { item1 });
        await _bills.AddAsync(bill);

        var loaded = await _bills.GetByIdAsync(billId);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Items);

        // Add item2 to bill
        var item2 = BillItem.FromOrderItem(billId, order2.Items[0]);
        var withAdded = loaded.AddItem(item2);
        await _bills.SaveAsync(withAdded, loaded.RowVersion);

        var reloaded = await _bills.GetByIdAsync(billId);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Items.Count);

        // Remove item1 from bill
        var removed = reloaded.RemoveItem(item1.Id);
        await _bills.SaveAsync(removed, reloaded.RowVersion);

        var finalReload = await _bills.GetByIdAsync(billId);
        Assert.NotNull(finalReload);
        Assert.Single(finalReload.Items);
        Assert.Equal(item2.Id, finalReload.Items[0].Id);
    }

    [Fact]
    public async Task GetByOrderIdAndGetByTableIdReturnMatchingBills()
    {
        var product = await SeedProduct("Mercimek", 60m);
        var table = await SeedTable("Masa 4");
        var order = await CreateAndSaveOrder(product, "Mercimek", 60m, table);

        var bill = Bill.FromOrder(Guid.NewGuid(), UniqueBillNumber(), order);
        await _bills.AddAsync(bill);

        var byOrder = await _bills.GetByOrderIdAsync(order.Id);
        Assert.Single(byOrder);
        Assert.Equal(bill.Id, byOrder[0].Id);

        var byTable = await _bills.GetByTableIdAsync(table);
        Assert.Single(byTable);
        Assert.Equal(bill.Id, byTable[0].Id);

        var billedIds = await _bills.GetBilledOrderItemIdsAsync(new[] { order.Items[0].Id, Guid.NewGuid() });
        Assert.Single(billedIds);
        Assert.Contains(order.Items[0].Id, billedIds);
    }

    private async Task<Guid> SeedProduct(string name, decimal price)
    {
        var productId = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, @name, @product_type, @stock_mode, @current_price);
            """);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("product_type", 1);
        command.Parameters.AddWithValue("stock_mode", 1);
        command.Parameters.AddWithValue("current_price", price);
        await command.ExecuteNonQueryAsync();
        return productId;
    }

    private async Task<Guid> SeedTable(string name)
    {
        var tableId = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Available');
            """);
        command.Parameters.AddWithValue("table_id", tableId);
        command.Parameters.AddWithValue("table_number", "TBL-" + Guid.NewGuid().ToString("N")[..6]);
        await command.ExecuteNonQueryAsync();
        return tableId;
    }

    private async Task<Order> CreateAndSaveOrder(Guid productId, string productName, decimal price = 100m, Guid? tableId = null)
    {
        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: productId,
            productNameSnapshot: productName,
            quantity: 1,
            unitPrice: price,
            taxRate: 10m);

        var order = new Order(
            id: orderId,
            source: OrderSource.Waiter,
            orderNumber: "ORD-" + Guid.NewGuid().ToString("N")[..8],
            items: new[] { item },
            tableId: tableId);

        await _orders.AddAsync(order);
        return order;
    }

    private static string UniqueBillNumber()
        => "BILL-" + Guid.NewGuid().ToString("N")[..8];
}

/// <summary>
/// Tests the rollback (down) script of 019-billing migration.
/// </summary>
public sealed class PostgresBillDownSqlTests : IClassFixture<BillingTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBillDownSqlTests(BillingTestDatabase database)
    {
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task DownSqlDropsTablesAndSchemaAndUpRecreatesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "019-billing.down.sql"));

        await using (var command = _dataSource.CreateCommand(downSql))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var check = _dataSource.CreateCommand(
            """
            SELECT to_regclass('billing.bills'),
                   to_regclass('billing.bill_items');
            """))
        {
            await using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.IsDBNull(0));
            Assert.True(reader.IsDBNull(1));
        }

        // Reapply up SQL to leave test database in working condition
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "019-billing.up.sql"));

        await using (var command = _dataSource.CreateCommand(upSql))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var check = _dataSource.CreateCommand(
            """
            SELECT to_regclass('billing.bills'),
                   to_regclass('billing.bill_items');
            """))
        {
            await using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.IsDBNull(0));
            Assert.False(reader.IsDBNull(1));
        }
    }
}
