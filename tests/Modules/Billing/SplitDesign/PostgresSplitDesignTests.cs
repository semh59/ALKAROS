using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.SplitDesign.Tests.Fixtures;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using Xunit;

namespace ALKAROS.Billing.SplitDesign.Tests;

/// <summary>
/// PostgreSQL integration tests for PostgresSplitDesignRepository and 020-split-design migration.
/// </summary>
public sealed class PostgresSplitDesignTests : IClassFixture<SplitDesignTestDatabase>
{
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";

    private readonly PostgresSplitDesignRepository _splitRepo;
    private readonly PostgresBillRepository _billRepo;
    private readonly PostgresOrderRepository _orderRepo;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSplitDesignTests(SplitDesignTestDatabase database)
    {
        _dataSource = database.DataSource;
        _splitRepo = new PostgresSplitDesignRepository(database.DataSource);
        _billRepo = new PostgresBillRepository(database.DataSource);
        _orderRepo = new PostgresOrderRepository(database.DataSource);
    }

    [Fact]
    public async Task SplitDesignRoundTripPersistsAndLoadsAllFields()
    {
        var (bill, item) = await CreateAndSaveBillWithItem("Adana Kebap", 240m);

        var alloc1 = new BillAllocation(
            id: Guid.NewGuid(),
            billId: bill.Id,
            ownerType: AllocationOwnerType.Person,
            ownerReference: "Musteri 1",
            allocatedAmount: 132.00m,
            taxAmount: 12.00m,
            billItemId: item.Id,
            allocatedQuantity: 0.5m);

        var alloc2 = new BillAllocation(
            id: Guid.NewGuid(),
            billId: bill.Id,
            ownerType: AllocationOwnerType.Person,
            ownerReference: "Musteri 2",
            allocatedAmount: 132.00m,
            taxAmount: 12.00m,
            billItemId: item.Id,
            allocatedQuantity: 0.5m);

        await _splitRepo.SaveSplitDesignAsync(bill.Id, new[] { alloc1, alloc2 });

        var loaded = await _splitRepo.GetAllocationsByBillIdAsync(bill.Id);
        Assert.Equal(2, loaded.Count);

        var loaded1 = loaded.First(a => a.Id == alloc1.Id);
        Assert.Equal(bill.Id, loaded1.BillId);
        Assert.Equal(item.Id, loaded1.BillItemId);
        Assert.Equal(AllocationOwnerType.Person, loaded1.OwnerType);
        Assert.Equal("Musteri 1", loaded1.OwnerReference);
        Assert.Equal(132.00m, loaded1.AllocatedAmount);
        Assert.Equal(12.00m, loaded1.TaxAmount);
        Assert.Equal(0.5m, loaded1.AllocatedQuantity);

        var totalAllocated = await _splitRepo.GetTotalAllocatedAmountAsync(bill.Id);
        Assert.Equal(264.00m, totalAllocated);
    }

    [Fact]
    public async Task SaveSplitDesignReplacesPreviousAllocationsAtomically()
    {
        var (bill, _) = await CreateAndSaveBillWithItem("Pide", 150m);

        var initial = SplitEngine.CreateEqualSplit(bill, 2);
        await _splitRepo.SaveSplitDesignAsync(bill.Id, initial);

        var loadedInitial = await _splitRepo.GetAllocationsByBillIdAsync(bill.Id);
        Assert.Equal(2, loadedInitial.Count);

        // Replace with 3-person split
        var updated = SplitEngine.CreateEqualSplit(bill, 3);
        await _splitRepo.SaveSplitDesignAsync(bill.Id, updated);

        var loadedUpdated = await _splitRepo.GetAllocationsByBillIdAsync(bill.Id);
        Assert.Equal(3, loadedUpdated.Count);

        // Sum matches bill payable
        Assert.Equal(bill.PayableAmount, loadedUpdated.Sum(a => a.AllocatedAmount));
    }

    [Fact]
    public async Task DeleteSplitDesignRemovesAllAllocationsForBill()
    {
        var (bill, _) = await CreateAndSaveBillWithItem("Lahmacun", 80m);

        var allocations = SplitEngine.CreateEqualSplit(bill, 2);
        await _splitRepo.SaveSplitDesignAsync(bill.Id, allocations);

        var loaded = await _splitRepo.GetAllocationsByBillIdAsync(bill.Id);
        Assert.NotEmpty(loaded);

        await _splitRepo.DeleteSplitDesignAsync(bill.Id);

        var afterDelete = await _splitRepo.GetAllocationsByBillIdAsync(bill.Id);
        Assert.Empty(afterDelete);

        var total = await _splitRepo.GetTotalAllocatedAmountAsync(bill.Id);
        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task CascadeDeleteOnBillDeletesAllocations()
    {
        var (bill, _) = await CreateAndSaveBillWithItem("Corba", 60m);

        var allocations = SplitEngine.CreateEqualSplit(bill, 2);
        await _splitRepo.SaveSplitDesignAsync(bill.Id, allocations);

        // Direct delete of bill in database to trigger cascade
        await using var command = _dataSource.CreateCommand("DELETE FROM billing.bills WHERE bill_id = @bill_id;");
        command.Parameters.AddWithValue("bill_id", bill.Id);
        await command.ExecuteNonQueryAsync();

        var loaded = await _splitRepo.GetAllocationsByBillIdAsync(bill.Id);
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task AllOwnerTypesAndFractionalQuantitiesPersistAccurately()
    {
        var (bill, item) = await CreateAndSaveBillWithItem("Dana Biftek", 500m);

        var personAlloc = new BillAllocation(
            id: Guid.NewGuid(),
            billId: bill.Id,
            ownerType: AllocationOwnerType.Person,
            ownerReference: "Masa Başı 1",
            allocatedAmount: 150m,
            taxAmount: 15m);

        var accountAlloc = new BillAllocation(
            id: Guid.NewGuid(),
            billId: bill.Id,
            ownerType: AllocationOwnerType.CustomerAccount,
            ownerReference: "CUST-ACC-9988",
            allocatedAmount: 200m,
            taxAmount: 20m);

        var itemAlloc = new BillAllocation(
            id: Guid.NewGuid(),
            billId: bill.Id,
            ownerType: AllocationOwnerType.Item,
            ownerReference: "Ortak Pay",
            allocatedAmount: 200m,
            taxAmount: 20m,
            billItemId: item.Id,
            allocatedQuantity: 0.333m);

        await _splitRepo.SaveSplitDesignAsync(bill.Id, new[] { personAlloc, accountAlloc, itemAlloc });

        var loaded = await _splitRepo.GetAllocationsByBillIdAsync(bill.Id);
        Assert.Equal(3, loaded.Count);

        var loadedAccount = loaded.Single(a => a.OwnerType == AllocationOwnerType.CustomerAccount);
        Assert.Equal("CUST-ACC-9988", loadedAccount.OwnerReference);
        Assert.Equal(200m, loadedAccount.AllocatedAmount);

        var loadedItem = loaded.Single(a => a.OwnerType == AllocationOwnerType.Item);
        Assert.Equal(0.333m, loadedItem.AllocatedQuantity);
        Assert.Equal(item.Id, loadedItem.BillItemId);
    }

    [Fact]
    public async Task ForeignKeyConstraintsEnforced()
    {
        // Unknown Bill ID
        var badBillAlloc = new BillAllocation(
            id: Guid.NewGuid(),
            billId: Guid.NewGuid(),
            ownerType: AllocationOwnerType.Person,
            ownerReference: "Ghost",
            allocatedAmount: 50m);

        var ex1 = await Assert.ThrowsAsync<PostgresException>(() =>
            _splitRepo.SaveSplitDesignAsync(badBillAlloc.BillId, new[] { badBillAlloc }));
        Assert.Equal(ForeignKeyViolation, ex1.SqlState);

        // Unknown BillItem ID
        var (bill, _) = await CreateAndSaveBillWithItem("Salata", 40m);
        var badItemAlloc = new BillAllocation(
            id: Guid.NewGuid(),
            billId: bill.Id,
            ownerType: AllocationOwnerType.Item,
            ownerReference: "Ghost Item",
            allocatedAmount: 20m,
            billItemId: Guid.NewGuid());

        var ex2 = await Assert.ThrowsAsync<PostgresException>(() =>
            _splitRepo.SaveSplitDesignAsync(bill.Id, new[] { badItemAlloc }));
        Assert.Equal(ForeignKeyViolation, ex2.SqlState);
    }

    [Fact]
    public async Task CheckConstraintsEnforced()
    {
        var (bill, _) = await CreateAndSaveBillWithItem("Kola", 30m);

        // Negative/Zero allocated_amount
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO billing.bill_allocations (
                bill_allocation_id, bill_id, owner_type, owner_reference,
                allocated_amount, tax_amount, created_at, row_version)
            VALUES (
                @id, @bill_id, 'Person', 'Guest', 0, 0, NOW(), 1);
            """))
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("bill_id", bill.Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(CheckViolation, ex.SqlState);
        }

        // Negative/Zero allocated_quantity
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO billing.bill_allocations (
                bill_allocation_id, bill_id, owner_type, owner_reference,
                allocated_quantity, allocated_amount, tax_amount, created_at, row_version)
            VALUES (
                @id, @bill_id, 'Person', 'Guest', -1, 10, 0, NOW(), 1);
            """))
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("bill_id", bill.Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(CheckViolation, ex.SqlState);
        }

        // Invalid owner_type
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO billing.bill_allocations (
                bill_allocation_id, bill_id, owner_type, owner_reference,
                allocated_amount, tax_amount, created_at, row_version)
            VALUES (
                @id, @bill_id, 'InvalidOwnerType', 'Guest', 10, 0, NOW(), 1);
            """))
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("bill_id", bill.Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(CheckViolation, ex.SqlState);
        }
    }

    private async Task<(Bill Bill, BillItem Item)> CreateAndSaveBillWithItem(string productName, decimal price)
    {
        var productId = Guid.NewGuid();
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, @name, 1, 1, @current_price);
            """))
        {
            command.Parameters.AddWithValue("product_id", productId);
            command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            command.Parameters.AddWithValue("name", productName);
            command.Parameters.AddWithValue("current_price", price);
            await command.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var orderItem = new OrderItem(
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
            items: new[] { orderItem });
        await _orderRepo.AddAsync(order);

        var billId = Guid.NewGuid();
        var billItem = BillItem.FromOrderItem(billId, orderItem);
        var bill = new Bill(billId, "BILL-" + Guid.NewGuid().ToString("N")[..8], new[] { billItem });
        await _billRepo.AddAsync(bill);

        return (bill, billItem);
    }
}

/// <summary>
/// Migration rollback tests for 020-split-design.
/// </summary>
public sealed class PostgresSplitDesignDownSqlTests : IClassFixture<SplitDesignTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSplitDesignDownSqlTests(SplitDesignTestDatabase database)
    {
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task DownSqlDropsAllocationsTableAndUpRecreatesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "020-split-design.down.sql"));

        await using (var command = _dataSource.CreateCommand(downSql))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var check = _dataSource.CreateCommand(
            "SELECT to_regclass('billing.bill_allocations');"))
        {
            await using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.IsDBNull(0));
        }

        // Reapply up SQL
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "020-split-design.up.sql"));

        await using (var command = _dataSource.CreateCommand(upSql))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var check = _dataSource.CreateCommand(
            "SELECT to_regclass('billing.bill_allocations');"))
        {
            await using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.IsDBNull(0));
        }
    }
}
