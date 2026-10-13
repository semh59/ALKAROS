using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.PaymentAggregate.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Payments.PaymentAggregate.Tests;

public sealed class PaymentReversalTests : IClassFixture<PaymentsTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentReversalRepository _reversals;

    public PaymentReversalTests(PaymentsTestDatabase database)
    {
        _dataSource = database.DataSource;
        _payments = new PostgresPaymentRepository(_dataSource);
        _reversals = new PostgresPaymentReversalRepository(_dataSource, _payments);
    }

    [Fact]
    public async Task AFullRefundWritesOneLedgerRowAndMovesThePaymentToRefunded()
    {
        var payment = await SeedApprovedAsync(100m);
        var userId = Guid.NewGuid();

        var reversal = await RefundAsync(payment.Id, 100m, "Müşteri iadesi", userId, "full-" + payment.Id);

        Assert.Equal("FullRefund", reversal.ReversalType);
        var loaded = (await _payments.GetByIdAsync(payment.Id))!;
        Assert.Equal(PaymentStatus.Refunded, loaded.Status);
        Assert.Equal(100m, loaded.ApprovedAmount);
        Assert.Contains(loaded.History, h => h.NewStatus == PaymentStatus.Refunded && h.ChangedBy == userId);
        Assert.Equal(100m, await _reversals.GetRefundedTotalAsync(payment.Id));
    }

    [Fact]
    public async Task PartialRefundsKeepThePaymentPartiallyRefundedUntilTheLastOneBringsItAllBack()
    {
        var payment = await SeedApprovedAsync(100m);

        var first = await RefundAsync(payment.Id, 20m, "Eksik ürün", Guid.NewGuid(), "p1-" + payment.Id);
        Assert.Equal("PartialRefund", first.ReversalType);
        Assert.Equal(PaymentStatus.PartiallyRefunded, (await _payments.GetByIdAsync(payment.Id))!.Status);

        var last = await RefundAsync(payment.Id, 80m, "Kalan iade", Guid.NewGuid(), "p2-" + payment.Id);
        Assert.Equal("FullRefund", last.ReversalType);
        Assert.Equal(PaymentStatus.Refunded, (await _payments.GetByIdAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task ARefundAboveWhatIsLeftIsRefusedAndLeavesNothingBehind()
    {
        var payment = await SeedApprovedAsync(100m);
        await RefundAsync(payment.Id, 20m, "Eksik ürün", Guid.NewGuid(), "o1-" + payment.Id);

        await Assert.ThrowsAsync<PaymentRefundException>(
            () => RefundAsync(payment.Id, 90m, "Fazla", Guid.NewGuid(), "o2-" + payment.Id));

        Assert.Equal(20m, await _reversals.GetRefundedTotalAsync(payment.Id));
        Assert.Equal(PaymentStatus.PartiallyRefunded, (await _payments.GetByIdAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task ARetryWithTheSameKeyReturnsTheRowAlreadyWritten()
    {
        var payment = await SeedApprovedAsync(100m);
        var key = "retry-" + payment.Id;

        var first = await RefundAsync(payment.Id, 40m, "İade", Guid.NewGuid(), key);
        var second = await RefundAsync(payment.Id, 40m, "İade", Guid.NewGuid(), key);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(40m, await _reversals.GetRefundedTotalAsync(payment.Id));
        await Assert.ThrowsAsync<PaymentRefundException>(
            () => RefundAsync(payment.Id, 50m, "Başka", Guid.NewGuid(), key));
    }

    [Fact]
    public async Task APaymentThatIsNotApprovedCannotBeRefunded()
    {
        var pending = new Payment(Guid.NewGuid(), await SeedBillAsync(), 50m).Tender(50m);
        await _payments.AddAsync(pending);

        await Assert.ThrowsAnyAsync<PaymentException>(
            () => RefundAsync(pending.Id, 10m, "İade", Guid.NewGuid(), "np-" + pending.Id));
        Assert.Throws<InvalidPaymentTransitionException>(() => pending.ApplyRefund(10m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ARefundWithoutAReasonIsRefused(string reason)
    {
        var payment = await SeedApprovedAsync(100m);

        await Assert.ThrowsAsync<PaymentRefundException>(
            () => RefundAsync(payment.Id, 10m, reason, Guid.NewGuid(), "nr-" + payment.Id));
    }

    [Fact]
    public async Task TheLedgerRefusesEditsDeletesAndOverRefundsAtTheDatabase()
    {
        var payment = await SeedApprovedAsync(100m);
        var reversal = await RefundAsync(payment.Id, 30m, "İade", Guid.NewGuid(), "db-" + payment.Id);

        await using (var update = _dataSource.CreateCommand(
            "UPDATE payments.payment_reversals SET amount = 31 WHERE reversal_id = @id;"))
        {
            update.Parameters.AddWithValue("id", reversal.Id);
            await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        }
        await using (var delete = _dataSource.CreateCommand(
            "DELETE FROM payments.payment_reversals WHERE reversal_id = @id;"))
        {
            delete.Parameters.AddWithValue("id", reversal.Id);
            await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
        }
        await using var overRefund = _dataSource.CreateCommand(
            """
            INSERT INTO payments.payment_reversals
                (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            VALUES (gen_random_uuid(), @payment_id, 'PartialRefund', 71, 'x', gen_random_uuid(), @key, now());
            """);
        overRefund.Parameters.AddWithValue("payment_id", payment.Id);
        overRefund.Parameters.AddWithValue("key", "over-" + payment.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => overRefund.ExecuteNonQueryAsync());
        Assert.Equal("23514", error.SqlState);
    }

    private async Task<PaymentReversal> RefundAsync(
        Guid paymentId, decimal amount, string reason, Guid userId, string key)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var reversal = await _reversals.AppendAsync(
            paymentId, amount, reason, userId, key, connection, transaction);
        await transaction.CommitAsync();
        return reversal;
    }

    private async Task<Payment> SeedApprovedAsync(decimal amount)
    {
        var payment = new Payment(Guid.NewGuid(), await SeedBillAsync(), amount).Tender(amount).Approve(amount);
        await _payments.AddAsync(payment);
        return payment;
    }

    private async Task<Guid> SeedBillAsync()
    {
        var productId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await using (var seed = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, 'Iskender', 1, 1, 220);
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Available');
            """))
        {
            seed.Parameters.AddWithValue("product_id", productId);
            seed.Parameters.AddWithValue("sku", "SKU-" + productId.ToString("N")[..8]);
            seed.Parameters.AddWithValue("table_id", tableId);
            seed.Parameters.AddWithValue("table_number", "TBL-" + tableId.ToString("N")[..6]);
            await seed.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var item = new OrderItem(Guid.NewGuid(), orderId, productId, "Iskender", 1, 220m, 10m);
        var order = new Order(orderId, OrderSource.Waiter, "ORD-" + orderId.ToString("N")[..8], [item], tableId);
        await new PostgresOrderRepository(_dataSource).AddAsync(order);

        var billId = Guid.NewGuid();
        var bill = new Bill(
            billId, "BILL-" + billId.ToString("N")[..8], [BillItem.FromOrderItem(billId, order.Items[0])],
            tableId: tableId, orderId: order.Id, status: BillState.Open, currencyCode: "TRY");
        await new PostgresBillRepository(_dataSource).AddAsync(bill);
        return billId;
    }
}
