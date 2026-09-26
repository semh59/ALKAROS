using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.PaymentClosure.Tests.Fixtures;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Billing.PaymentClosure.Tests;

/// <summary>
/// Integration tests for <see cref="BillPaymentClosureProjector"/> against
/// real Postgres (V13-ALC-002): rebuild determinism, a genuine "not found"
/// and a real end-to-end satisfied projection built from a seeded Payment +
/// PaymentAllocation.
/// </summary>
public sealed class BillPaymentClosureProjectorTests : IClassFixture<BillPaymentClosureTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly BillPaymentClosureProjector _projector;

    public BillPaymentClosureProjectorTests(BillPaymentClosureTestDatabase database)
    {
        _dataSource = database.DataSource;
        _bills = new PostgresBillRepository(_dataSource);
        _orders = new PostgresOrderRepository(_dataSource);
        _payments = new PostgresPaymentRepository(_dataSource);
        _allocations = new PostgresPaymentAllocationRepository(_dataSource, new PostgresBillAdjustmentRepository(_dataSource));
        _projector = new BillPaymentClosureProjector(_bills, _payments, _allocations, new PostgresBillAdjustmentRepository(_dataSource));
    }

    [Fact]
    public async Task RebuildAsyncThrowsWhenTheBillDoesNotExist()
    {
        var act = () => _projector.RebuildAsync(Guid.NewGuid());

        var exception = await act.Should().ThrowAsync<PaymentClosureBillNotFoundException>();
        exception.Which.BillId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RebuildAsyncReflectsARealApprovedPaymentAndAllocationAsSatisfied()
    {
        var billId = await SeedBillAsync(payable: 60m);
        var payment = new Payment(Guid.NewGuid(), billId, 60m, currencyCode: "TRY")
            .Tender(60m)
            .Approve(60m);
        await _payments.AddAsync(payment);
        await _allocations.AllocateAsync(payment, (await _bills.GetByIdAsync(billId))!, 60m, "key-" + Guid.NewGuid());

        var projection = await _projector.RebuildAsync(billId);

        projection.PaymentSatisfied.Should().BeTrue();
        projection.AllocatedTotal.Should().Be(60m);
        projection.PaidTotal.Should().Be(60m);
        projection.ChangeTotal.Should().Be(0m);
        projection.Blockers.Should().BeEmpty();
    }

    [Fact]
    public async Task RebuildAsyncIsDeterministicAcrossRepeatedCalls()
    {
        var billId = await SeedBillAsync(payable: 45m);
        var payment = new Payment(Guid.NewGuid(), billId, 45m, currencyCode: "TRY")
            .Tender(50m)
            .Approve(45m);
        await _payments.AddAsync(payment);
        await _allocations.AllocateAsync(payment, (await _bills.GetByIdAsync(billId))!, 45m, "key-" + Guid.NewGuid());

        var first = await _projector.RebuildAsync(billId);
        var second = await _projector.RebuildAsync(billId);

        // BeEquivalentTo, not Be: the record's auto-generated equality
        // compares Blockers (an IReadOnlyList<T>, backed by List<T>) by
        // reference, not by content, so two separately-built empty/equal
        // lists are never Equals() even with identical values.
        second.Should().BeEquivalentTo(first);
    }

    private async Task<Guid> SeedBillAsync(decimal payable)
    {
        var productId = Guid.NewGuid();
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, @name, @product_type, @stock_mode, @current_price);
            """))
        {
            command.Parameters.AddWithValue("product_id", productId);
            command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            command.Parameters.AddWithValue("name", "Test Item");
            command.Parameters.AddWithValue("product_type", 1);
            command.Parameters.AddWithValue("stock_mode", 1);
            command.Parameters.AddWithValue("current_price", payable);
            await command.ExecuteNonQueryAsync();
        }

        var tableId = Guid.NewGuid();
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Available');
            """))
        {
            command.Parameters.AddWithValue("table_id", tableId);
            command.Parameters.AddWithValue("table_number", "TBL-" + Guid.NewGuid().ToString("N")[..6]);
            await command.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            id: Guid.NewGuid(), orderId: orderId, productId: productId, productNameSnapshot: "Test Item",
            quantity: 1, unitPrice: payable, taxRate: 0m);
        var order = new Order(orderId, OrderSource.Cashier, "ORD-" + Guid.NewGuid().ToString("N")[..8], [item], tableId: tableId);
        await _orders.AddAsync(order);

        var billId = Guid.NewGuid();
        var billItem = BillItem.FromOrderItem(billId, order.Items[0]);
        var bill = new Bill(
            id: billId,
            billNumber: "BILL-" + Guid.NewGuid().ToString("N")[..8],
            items: [billItem],
            tableId: tableId,
            orderId: order.Id,
            status: BillState.Open,
            currencyCode: "TRY");
        await _bills.AddAsync(bill);
        return billId;
    }
}
