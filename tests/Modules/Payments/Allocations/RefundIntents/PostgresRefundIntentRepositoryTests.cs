using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.Allocations.RefundIntents.Tests.Fixtures;
using ALKAROS.Payments.PaymentAggregate;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Payments.Allocations.RefundIntents.Tests;

/// <summary>
/// Integration tests for <see cref="PostgresRefundIntentRepository"/>
/// against real Postgres (V13-ALC-003). Exercises the task's own
/// Acceptance evidence directly: a 20-of-100 request produces one Pending
/// intent, a duplicate submit replays it, a request that would push the
/// cumulative total over 100 is rejected before anything is persisted, and
/// a truly concurrent duplicate submit still produces exactly one real row.
/// </summary>
public sealed class PostgresRefundIntentRepositoryTests : IClassFixture<RefundIntentTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly PostgresRefundIntentRepository _refundIntents;

    public PostgresRefundIntentRepositoryTests(RefundIntentTestDatabase database)
    {
        _dataSource = database.DataSource;
        _bills = new PostgresBillRepository(_dataSource);
        _orders = new PostgresOrderRepository(_dataSource);
        _payments = new PostgresPaymentRepository(_dataSource);
        _allocations = new PostgresPaymentAllocationRepository(_dataSource);
        _refundIntents = new PostgresRefundIntentRepository(_dataSource);
    }

    [Fact]
    public async Task CreateAsyncFor20Of100ProducesExactlyOnePendingIntent()
    {
        var (payment, allocation) = await SeedApprovedPaymentAndAllocationAsync(100m);

        var intent = await _refundIntents.CreateAsync(payment.Id, allocation, 20m, "key-" + Guid.NewGuid());

        intent.Status.Should().Be(RefundIntentStatus.Pending);
        intent.RequestedAmount.Should().Be(20m);
        var stored = await _refundIntents.GetByAllocationIdAsync(allocation.Id);
        stored.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsyncReplaysADuplicateSubmitInsteadOfCreatingASecondIntent()
    {
        var (payment, allocation) = await SeedApprovedPaymentAndAllocationAsync(100m);
        var idempotencyKey = "key-" + Guid.NewGuid();

        var first = await _refundIntents.CreateAsync(payment.Id, allocation, 20m, idempotencyKey);
        var second = await _refundIntents.CreateAsync(payment.Id, allocation, 20m, idempotencyKey);

        second.Id.Should().Be(first.Id);
        (await _refundIntents.GetByAllocationIdAsync(allocation.Id)).Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsyncRejectsARequestThatWouldPushTheCumulativeTotalOverTheAllocationAndPersistsNothing()
    {
        var (payment, allocation) = await SeedApprovedPaymentAndAllocationAsync(100m);
        await _refundIntents.CreateAsync(payment.Id, allocation, 20m, "key-" + Guid.NewGuid());

        // 20 already Pending; a further 81 would reach 101 > 100.
        var act = () => _refundIntents.CreateAsync(payment.Id, allocation, 81m, "key-" + Guid.NewGuid());

        await act.Should().ThrowAsync<RefundIntentOverLimitException>();
        (await _refundIntents.GetByAllocationIdAsync(allocation.Id)).Should().HaveCount(1, "the rejected request must not have persisted anything");
    }

    [Fact]
    public async Task ConcurrentDuplicateSubmitsProduceExactlyOneRealIntent()
    {
        var (payment, allocation) = await SeedApprovedPaymentAndAllocationAsync(100m);
        var idempotencyKey = "key-" + Guid.NewGuid();

        var first = _refundIntents.CreateAsync(payment.Id, allocation, 20m, idempotencyKey);
        var second = _refundIntents.CreateAsync(payment.Id, allocation, 20m, idempotencyKey);
        var results = await Task.WhenAll(first, second);

        results[0].Id.Should().Be(results[1].Id);
        (await _refundIntents.GetByAllocationIdAsync(allocation.Id)).Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveAsyncPersistsTheRejectTransition()
    {
        var (payment, allocation) = await SeedApprovedPaymentAndAllocationAsync(100m);
        var intent = await _refundIntents.CreateAsync(payment.Id, allocation, 20m, "key-" + Guid.NewGuid());

        var rejected = intent.Reject("Customer withdrew the request");
        var newRowVersion = await _refundIntents.SaveAsync(rejected, intent.RowVersion);

        newRowVersion.Should().Be(intent.RowVersion + 1);
        var reloaded = await _refundIntents.GetByIdempotencyKeyAsync(intent.IdempotencyKey);
        reloaded.Should().NotBeNull();
        reloaded!.Status.Should().Be(RefundIntentStatus.Rejected);
        reloaded.RejectionReason.Should().Be("Customer withdrew the request");

        // Rejected intents no longer count toward cumulative eligibility -
        // the full 100 is refund-eligible again.
        var afterReject = await _refundIntents.CreateAsync(payment.Id, allocation, 100m, "key-" + Guid.NewGuid());
        afterReject.RequestedAmount.Should().Be(100m);
    }

    private async Task<(Payment Payment, PaymentAllocation Allocation)> SeedApprovedPaymentAndAllocationAsync(decimal payable)
    {
        var billId = await SeedBillAsync(payable);
        var bill = await _bills.GetByIdAsync(billId);
        var payment = new Payment(Guid.NewGuid(), billId, payable, currencyCode: "TRY")
            .Tender(payable)
            .Approve(payable);
        await _payments.AddAsync(payment);
        var allocation = await _allocations.AllocateAsync(payment, bill!, payable, "alloc-key-" + Guid.NewGuid());
        return (payment, allocation);
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
