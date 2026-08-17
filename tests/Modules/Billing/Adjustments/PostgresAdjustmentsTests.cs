using ALKAROS.Billing.Adjustments.Tests.Fixtures;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using Xunit;

namespace ALKAROS.Billing.Adjustments.Tests;

/// <summary>
/// PostgreSQL integration tests for PostgresBillAdjustmentRepository and 021-bill-adjustments migration.
/// </summary>
public sealed class PostgresAdjustmentsTests : IClassFixture<AdjustmentsTestDatabase>
{
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";

    private readonly PostgresBillAdjustmentRepository _adjRepo;
    private readonly PostgresBillRepository _billRepo;
    private readonly PostgresOrderRepository _orderRepo;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdjustmentsTests(AdjustmentsTestDatabase database)
    {
        _dataSource = database.DataSource;
        _adjRepo = new PostgresBillAdjustmentRepository(database.DataSource);
        _billRepo = new PostgresBillRepository(database.DataSource);
        _orderRepo = new PostgresOrderRepository(database.DataSource);
    }

    [Fact]
    public async Task AdjustmentRoundTripPersistsAndLoadsAllFields()
    {
        var (bill, item) = await CreateAndSaveBillWithItem("Karisik Izgara", 350m);
        var managerId = Guid.NewGuid();

        var discount = BillAdjustment.CreateDiscountPercentage(
            id: Guid.NewGuid(),
            billId: bill.Id,
            rate: 10m,
            baseGrossAmount: 350m,
            taxRate: 10m,
            reason: "VIP Guest",
            authorizedBy: managerId,
            billItemId: item.Id,
            notes: "Approved by manager");

        var serviceFee = BillAdjustment.CreateServiceFee(
            id: Guid.NewGuid(),
            billId: bill.Id,
            amount: 35m,
            taxRate: 10m,
            reason: "Kuver",
            authorizedBy: managerId,
            isKuver: true);

        await _adjRepo.AddAsync(discount);
        await _adjRepo.AddAsync(serviceFee);

        var loaded = await _adjRepo.GetByBillIdAsync(bill.Id);
        Assert.Equal(2, loaded.Count);

        var loadedDiscount = loaded.First(a => a.Id == discount.Id);
        Assert.Equal(bill.Id, loadedDiscount.BillId);
        Assert.Equal(item.Id, loadedDiscount.BillItemId);
        Assert.Equal(AdjustmentType.DiscountPercentage, loadedDiscount.AdjustmentType);
        Assert.Equal(AdjustmentCalculationType.Percentage, loadedDiscount.CalculationType);
        Assert.Equal(10m, loadedDiscount.Rate);
        Assert.Equal(35.00m, loadedDiscount.GrossAmount);
        Assert.True(loadedDiscount.IsDeduction);
        Assert.Equal("VIP Guest", loadedDiscount.Reason);
        Assert.Equal(managerId, loadedDiscount.AuthorizedBy);
        Assert.Equal("Approved by manager", loadedDiscount.Notes);

        var loadedFee = loaded.First(a => a.Id == serviceFee.Id);
        Assert.Equal(AdjustmentType.Kuver, loadedFee.AdjustmentType);
        Assert.False(loadedFee.IsDeduction);

        var totalDiscount = await _adjRepo.GetTotalDiscountAmountAsync(bill.Id);
        Assert.Equal(35.00m, totalDiscount);

        var totalFee = await _adjRepo.GetTotalFeeAmountAsync(bill.Id);
        Assert.Equal(35.00m, totalFee);
    }

    [Fact]
    public async Task RemoveAdjustmentDeletesFromDatabase()
    {
        var (bill, _) = await CreateAndSaveBillWithItem("Pide", 150m);
        var managerId = Guid.NewGuid();

        var tip = BillAdjustment.CreateTip(
            Guid.NewGuid(), bill.Id, 20m, "Service Tip", managerId);
        await _adjRepo.AddAsync(tip);

        var before = await _adjRepo.GetByBillIdAsync(bill.Id);
        Assert.Single(before);

        await _adjRepo.RemoveAsync(tip.Id);

        var after = await _adjRepo.GetByBillIdAsync(bill.Id);
        Assert.Empty(after);

        var totalFee = await _adjRepo.GetTotalFeeAmountAsync(bill.Id);
        Assert.Equal(0m, totalFee);
    }

    [Fact]
    public async Task CascadeDeleteOnBillDeletesAdjustments()
    {
        var (bill, _) = await CreateAndSaveBillWithItem("Kebab", 200m);
        var managerId = Guid.NewGuid();

        var discount = BillAdjustment.CreateDiscountAmount(
            Guid.NewGuid(), bill.Id, 20m, 10m, "Discount", managerId);
        await _adjRepo.AddAsync(discount);

        // Delete bill in database
        await using var command = _dataSource.CreateCommand("DELETE FROM billing.bills WHERE bill_id = @bill_id;");
        command.Parameters.AddWithValue("bill_id", bill.Id);
        await command.ExecuteNonQueryAsync();

        var loaded = await _adjRepo.GetByBillIdAsync(bill.Id);
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task ForeignKeyConstraintsEnforced()
    {
        // Unknown Bill ID
        var badBillAdj = BillAdjustment.CreateTip(
            Guid.NewGuid(), Guid.NewGuid(), 10m, "Tip", Guid.NewGuid());
        var ex1 = await Assert.ThrowsAsync<PostgresException>(() => _adjRepo.AddAsync(badBillAdj));
        Assert.Equal(ForeignKeyViolation, ex1.SqlState);

        // Unknown BillItem ID
        var (bill, _) = await CreateAndSaveBillWithItem("Salata", 50m);
        var badItemAdj = BillAdjustment.CreateDiscountAmount(
            Guid.NewGuid(), bill.Id, 10m, 10m, "Discount", Guid.NewGuid(), billItemId: Guid.NewGuid());
        var ex2 = await Assert.ThrowsAsync<PostgresException>(() => _adjRepo.AddAsync(badItemAdj));
        Assert.Equal(ForeignKeyViolation, ex2.SqlState);
    }

    [Fact]
    public async Task CheckConstraintsEnforced()
    {
        var (bill, _) = await CreateAndSaveBillWithItem("Ayran", 20m);
        var managerId = Guid.NewGuid();

        // Amount <= 0
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO billing.bill_adjustments (
                bill_adjustment_id, bill_id, adjustment_type, calculation_type,
                amount, net_amount, gross_amount, is_deduction, reason, authorized_by, created_at, row_version)
            VALUES (
                @id, @bill_id, 'Tip', 'FixedAmount', 0, 0, 0, false, 'Tip', @auth_by, NOW(), 1);
            """))
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("bill_id", bill.Id);
            command.Parameters.AddWithValue("auth_by", managerId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(CheckViolation, ex.SqlState);
        }

        // Rate > 100
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO billing.bill_adjustments (
                bill_adjustment_id, bill_id, adjustment_type, calculation_type,
                rate, amount, net_amount, gross_amount, is_deduction, reason, authorized_by, created_at, row_version)
            VALUES (
                @id, @bill_id, 'DiscountPercentage', 'Percentage', 150, 10, 10, 10, true, 'Discount', @auth_by, NOW(), 1);
            """))
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("bill_id", bill.Id);
            command.Parameters.AddWithValue("auth_by", managerId);
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
/// Rollback migration tests for 021-bill-adjustments.
/// </summary>
public sealed class PostgresAdjustmentsDownSqlTests : IClassFixture<AdjustmentsTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdjustmentsDownSqlTests(AdjustmentsTestDatabase database)
    {
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task DownSqlDropsAdjustmentsTableAndUpRecreatesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "021-bill-adjustments.down.sql"));

        await using (var command = _dataSource.CreateCommand(downSql))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var check = _dataSource.CreateCommand(
            "SELECT to_regclass('billing.bill_adjustments');"))
        {
            await using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.IsDBNull(0));
        }

        // Reapply up SQL
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "021-bill-adjustments.up.sql"));

        await using (var command = _dataSource.CreateCommand(upSql))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using (var check = _dataSource.CreateCommand(
            "SELECT to_regclass('billing.bill_adjustments');"))
        {
            await using var reader = await check.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.IsDBNull(0));
        }
    }
}
