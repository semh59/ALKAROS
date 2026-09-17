using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.PaymentAggregate.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Payments.PaymentAggregate.Tests;

/// <summary>
/// Integration tests for PostgresPaymentRepository and the 120-payments
/// schema, against real Postgres. Exercises round-trip persistence,
/// optimistic concurrency, status-history append-only behaviour and the
/// database-level FK/CHECK constraints that back up the aggregate's own
/// invariants (defense in depth, matching PostgresBillTests' own pattern).
/// </summary>
public sealed class PostgresPaymentTests : IClassFixture<PaymentsTestDatabase>
{
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";

    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPaymentTests(PaymentsTestDatabase database)
    {
        _dataSource = database.DataSource;
        _payments = new PostgresPaymentRepository(database.DataSource);
        _bills = new PostgresBillRepository(database.DataSource);
        _orders = new PostgresOrderRepository(database.DataSource);
    }

    [Fact]
    public async Task RoundTripPersistsAndLoadsAnInitiatedPayment()
    {
        var billId = await SeedBillAsync();
        var payment = new Payment(Guid.NewGuid(), billId, 80m);

        await _payments.AddAsync(payment);
        var loaded = await _payments.GetByIdAsync(payment.Id);

        Assert.NotNull(loaded);
        Assert.Equal(payment.Id, loaded.Id);
        Assert.Equal(billId, loaded.BillId);
        Assert.Equal(PaymentStatus.Initiated, loaded.Status);
        Assert.Equal(80m, loaded.RequestedAmount);
        Assert.Null(loaded.TenderedAmount);
        Assert.Null(loaded.ApprovedAmount);
        Assert.Equal(0m, loaded.ChangeAmount);
        Assert.Equal(1, loaded.RowVersion);
        Assert.Empty(loaded.History);
    }

    [Fact]
    public async Task SaveAfterTenderPersistsTheNewStatusAmountAndOneHistoryRow()
    {
        var billId = await SeedBillAsync();
        var payment = new Payment(Guid.NewGuid(), billId, 80m);
        await _payments.AddAsync(payment);

        var tendered = payment.Tender(100m, reason: "Nakit");
        var newRowVersion = await _payments.SaveAsync(tendered, expectedRowVersion: 1);

        Assert.Equal(2, newRowVersion);
        var loaded = await _payments.GetByIdAsync(payment.Id);
        Assert.NotNull(loaded);
        Assert.Equal(PaymentStatus.Pending, loaded.Status);
        Assert.Equal(100m, loaded.TenderedAmount);
        Assert.Equal(2, loaded.RowVersion);
        var entry = Assert.Single(loaded.History);
        Assert.Equal(PaymentStatus.Initiated, entry.OldStatus);
        Assert.Equal(PaymentStatus.Pending, entry.NewStatus);
        Assert.Equal("Nakit", entry.Reason);
    }

    [Fact]
    public async Task SaveAfterApproveWithOverpaymentPersistsChangeAndBothHistoryRows()
    {
        var billId = await SeedBillAsync();
        var payment = new Payment(Guid.NewGuid(), billId, 80m);
        await _payments.AddAsync(payment);

        var tendered = payment.Tender(100m);
        await _payments.SaveAsync(tendered, expectedRowVersion: 1);

        var approved = tendered.Approve(80m);
        var newRowVersion = await _payments.SaveAsync(approved, expectedRowVersion: 2);

        Assert.Equal(3, newRowVersion);
        var loaded = await _payments.GetByIdAsync(payment.Id);
        Assert.NotNull(loaded);
        Assert.Equal(PaymentStatus.Approved, loaded.Status);
        Assert.Equal(80m, loaded.ApprovedAmount);
        Assert.Equal(20m, loaded.ChangeAmount);
        Assert.Equal(2, loaded.History.Count);
    }

    [Fact]
    public async Task SaveWithStaleRowVersionFailsClosed()
    {
        var billId = await SeedBillAsync();
        var payment = new Payment(Guid.NewGuid(), billId, 80m);
        await _payments.AddAsync(payment);

        var tendered = payment.Tender(100m);
        await _payments.SaveAsync(tendered, expectedRowVersion: 1);

        // Someone else already saved this payment (real row version is now
        // 2); replaying against the stale version 1 must fail closed rather
        // than silently overwrite the concurrent change.
        var staleRetry = payment.Tender(50m);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _payments.SaveAsync(staleRetry, expectedRowVersion: 1));
    }

    [Fact]
    public async Task GetByBillIdReturnsEveryPaymentAgainstThatBill()
    {
        // V0-DOM-004 positive example 2's numbers (bill payable 80; Payment
        // A allocated 50; Payment B allocated the remaining 30, change 20),
        // but this test does NOT exercise an actual bill-payable cap
        // mechanism -- Payment B's 30 approved amount is hand-supplied by
        // the test, not computed or enforced by any cap in Payment.cs or
        // the database. A real cap (rejecting/capping approvals so the sum
        // of a bill's approved payments cannot exceed its payable) is
        // V13-ALC-001's job, not built yet -- this test only proves that
        // GetByBillIdAsync returns every payment row against a bill.
        var billId = await SeedBillAsync();
        var paymentA = new Payment(Guid.NewGuid(), billId, 50m).Tender(50m).Approve(50m);
        var paymentB = new Payment(Guid.NewGuid(), billId, 30m).Tender(50m).Approve(30m);
        await _payments.AddAsync(paymentA);
        await _payments.AddAsync(paymentB);

        var loaded = await _payments.GetByBillIdAsync(billId);

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, p => p.Id == paymentA.Id && p.ApprovedAmount == 50m);
        Assert.Contains(loaded, p => p.Id == paymentB.Id && p.ApprovedAmount == 30m && p.ChangeAmount == 20m);
    }

    [Fact]
    public async Task InsertRejectsAPaymentAgainstANonExistentBill()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 80m);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => _payments.AddAsync(payment));
        Assert.Equal(ForeignKeyViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsAZeroRequestedAmountEvenIfSomeFutureCallerBypassesTheAggregate()
    {
        var billId = await SeedBillAsync();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO payments.payments (
                payment_id, bill_id, status, currency_code, requested_amount,
                initiated_at, created_at, updated_at)
            VALUES (@payment_id, @bill_id, 'Initiated', 'TRY', 0,
                    now(), now(), now());
            """);
        command.Parameters.AddWithValue("payment_id", Guid.NewGuid());
        command.Parameters.AddWithValue("bill_id", billId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsInitiatedStatusWithATenderedAmount()
    {
        var billId = await SeedBillAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRawPaymentAsync(
            billId, status: "Initiated", requestedAmount: 80m, tenderedAmount: 100m,
            approvedAmount: null, changeAmount: 0m));
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsPendingStatusWithNoTenderedAmount()
    {
        var billId = await SeedBillAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRawPaymentAsync(
            billId, status: "Pending", requestedAmount: 80m, tenderedAmount: null,
            approvedAmount: null, changeAmount: 0m));
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsApprovedStatusWithNoApprovedAmount()
    {
        var billId = await SeedBillAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRawPaymentAsync(
            billId, status: "Approved", requestedAmount: 80m, tenderedAmount: 100m,
            approvedAmount: null, changeAmount: 0m));
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsPendingStatusWithAnApprovedAmount()
    {
        var billId = await SeedBillAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRawPaymentAsync(
            billId, status: "Pending", requestedAmount: 80m, tenderedAmount: 100m,
            approvedAmount: 80m, changeAmount: 20m));
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsApprovedAmountExceedingTenderedAmount()
    {
        var billId = await SeedBillAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRawPaymentAsync(
            billId, status: "Approved", requestedAmount: 80m, tenderedAmount: 100m,
            approvedAmount: 150m, changeAmount: -50m));
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsChangeAmountThatDoesNotReconcileWithTenderedMinusApproved()
    {
        var billId = await SeedBillAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRawPaymentAsync(
            billId, status: "Approved", requestedAmount: 80m, tenderedAmount: 100m,
            approvedAmount: 80m, changeAmount: 5m));
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    private async Task InsertRawPaymentAsync(
        Guid billId, string status, decimal requestedAmount, decimal? tenderedAmount,
        decimal? approvedAmount, decimal changeAmount)
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO payments.payments (
                payment_id, bill_id, status, currency_code, requested_amount,
                tendered_amount, approved_amount, change_amount,
                initiated_at, created_at, updated_at)
            VALUES (@payment_id, @bill_id, @status, 'TRY', @requested_amount,
                    @tendered_amount, @approved_amount, @change_amount,
                    now(), now(), now());
            """);
        command.Parameters.AddWithValue("payment_id", Guid.NewGuid());
        command.Parameters.AddWithValue("bill_id", billId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("requested_amount", requestedAmount);
        command.Parameters.AddWithValue("tendered_amount", (object?)tenderedAmount ?? DBNull.Value);
        command.Parameters.AddWithValue("approved_amount", (object?)approvedAmount ?? DBNull.Value);
        command.Parameters.AddWithValue("change_amount", changeAmount);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> SeedBillAsync()
    {
        var productId = await SeedProductAsync("Iskender", 220m);
        var tableId = await SeedTableAsync();
        var order = await CreateAndSaveOrderAsync(productId, "Iskender", 220m, tableId);

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

    private async Task<Guid> SeedProductAsync(string name, decimal price)
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

    private async Task<Guid> SeedTableAsync()
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

    private async Task<Order> CreateAndSaveOrderAsync(Guid productId, string productName, decimal price, Guid tableId)
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
            items: [item],
            tableId: tableId);

        await _orders.AddAsync(order);
        return order;
    }
}
