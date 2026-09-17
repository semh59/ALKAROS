using ALKAROS.Billing.BillFoundation;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TransactionLedger.Tests.Fixtures;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.PaymentAggregate;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Cash.TransactionLedger.Tests;

/// <summary>
/// Integration tests for PostgresCashTransactionLedgerRepository against
/// real Postgres. Proves the session's expected cash is reconstructed
/// purely from its own immutable ledger entries (Acceptance evidence), and
/// that the database-level constraints reject the same violations the
/// CashTransaction constructor already refuses, even bypassing it.
/// </summary>
public sealed class PostgresCashTransactionLedgerRepositoryTests : IClassFixture<CashLedgerTestDatabase>
{
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private readonly PostgresCashTransactionLedgerRepository _ledger;
    private readonly PostgresCashSessionRepository _sessions;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCashTransactionLedgerRepositoryTests(CashLedgerTestDatabase database)
    {
        _dataSource = database.DataSource;
        _ledger = new PostgresCashTransactionLedgerRepository(database.DataSource);
        _sessions = new PostgresCashSessionRepository(database.DataSource);
        _payments = new PostgresPaymentRepository(database.DataSource);
        _bills = new PostgresBillRepository(database.DataSource);
        _orders = new PostgresOrderRepository(database.DataSource);
    }

    [Fact]
    public async Task RecordAsyncPersistsAndRoundTrips()
    {
        var sessionId = await SeedCashSessionAsync();
        var transaction = new CashTransaction(
            Guid.NewGuid(), sessionId, CashTransactionType.CashIn, 25m, CashTransactionDirection.In, notes: "Ek para ustu");

        await _ledger.RecordAsync(transaction);

        var loaded = await _ledger.GetBySessionIdAsync(sessionId);
        loaded.Should().ContainSingle(t => t.Id == transaction.Id && t.Amount == 25m);
    }

    [Fact]
    public async Task ComputeExpectedCashAsyncReconstructsTheRunningTotalFromImmutableEntriesAlone()
    {
        // docs/domain/cash-session-design.md §4: ExpectedCash = OpeningBalance
        // + CashIn + CashSales - CashOut - CashRefunds. Opening 500, CashIn 20,
        // Sale 100, CashOut 15, Refund 5 -> 500+20+100-15-5 = 600.
        var sessionId = await SeedCashSessionAsync();
        var (paymentA, paymentB) = await SeedTwoPaymentsAsync();

        await _ledger.RecordAsync(new CashTransaction(Guid.NewGuid(), sessionId, CashTransactionType.Opening, 500m, CashTransactionDirection.In));
        await _ledger.RecordAsync(new CashTransaction(Guid.NewGuid(), sessionId, CashTransactionType.CashIn, 20m, CashTransactionDirection.In, notes: "Kasadan takviye"));
        await _ledger.RecordAsync(new CashTransaction(Guid.NewGuid(), sessionId, CashTransactionType.Sale, 100m, CashTransactionDirection.In, paymentA.Id));
        await _ledger.RecordAsync(new CashTransaction(Guid.NewGuid(), sessionId, CashTransactionType.CashOut, 15m, CashTransactionDirection.Out, notes: "Kurye odemesi"));
        await _ledger.RecordAsync(new CashTransaction(Guid.NewGuid(), sessionId, CashTransactionType.Refund, 5m, CashTransactionDirection.Out, paymentB.Id));

        var expected = await _ledger.ComputeExpectedCashAsync(sessionId);

        expected.Should().Be(600m);
    }

    [Fact]
    public async Task ComputeExpectedCashAsyncExcludesCountAdjustmentAndClosingDifferenceEntries()
    {
        var sessionId = await SeedCashSessionAsync();

        await _ledger.RecordAsync(new CashTransaction(Guid.NewGuid(), sessionId, CashTransactionType.Opening, 500m, CashTransactionDirection.In));
        await _ledger.RecordAsync(new CashTransaction(Guid.NewGuid(), sessionId, CashTransactionType.CountAdjustment, 1000m, CashTransactionDirection.In, notes: "Denetim notu"));
        await _ledger.RecordAsync(new CashTransaction(Guid.NewGuid(), sessionId, CashTransactionType.ClosingDifference, 1000m, CashTransactionDirection.Out));

        var expected = await _ledger.ComputeExpectedCashAsync(sessionId);

        expected.Should().Be(500m, "CountAdjustment and ClosingDifference are audit entries, not inputs to the running expected cash");
    }

    [Fact]
    public async Task ComputeExpectedCashAsyncReturnsZeroForASessionWithNoEntriesYet()
    {
        var sessionId = await SeedCashSessionAsync();

        var expected = await _ledger.ComputeExpectedCashAsync(sessionId);

        expected.Should().Be(0m);
    }

    [Fact]
    public async Task DatabaseRejectsADirectionThatDoesNotMatchAFixedDirectionType()
    {
        var sessionId = await SeedCashSessionAsync();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO cash.cash_transactions (
                cash_transaction_id, cash_session_id, type, direction, amount, occurred_at)
            VALUES (@id, @session_id, 'CashIn', 'Out', 10, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("session_id", sessionId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsASaleEntryWithNoRelatedPayment()
    {
        var sessionId = await SeedCashSessionAsync();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO cash.cash_transactions (
                cash_transaction_id, cash_session_id, type, direction, amount, occurred_at)
            VALUES (@id, @session_id, 'Sale', 'In', 10, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("session_id", sessionId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsACountAdjustmentWithNoNotes()
    {
        var sessionId = await SeedCashSessionAsync();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO cash.cash_transactions (
                cash_transaction_id, cash_session_id, type, direction, amount, occurred_at)
            VALUES (@id, @session_id, 'CountAdjustment', 'In', 10, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("session_id", sessionId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsAZeroOrNegativeAmount()
    {
        var sessionId = await SeedCashSessionAsync();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO cash.cash_transactions (
                cash_transaction_id, cash_session_id, type, direction, amount, occurred_at)
            VALUES (@id, @session_id, 'CashIn', 'In', 0, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("session_id", sessionId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsAnEntryAgainstANonExistentSession()
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO cash.cash_transactions (
                cash_transaction_id, cash_session_id, type, direction, amount, occurred_at)
            VALUES (@id, @session_id, 'CashIn', 'In', 10, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("session_id", Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(ForeignKeyViolation, exception.SqlState);
    }

    private async Task<Guid> SeedCashSessionAsync()
    {
        var sessionId = Guid.NewGuid();
        var session = new CashSessionRecord(
            new Contracts.CashSessionSnapshot(
                sessionId, Guid.NewGuid(), Guid.NewGuid(), CashSessionStatus.Open,
                500m, 500m, 0m, 0m, DateTimeOffset.UtcNow, null, 1),
            ClosedBy: null, IsSupervisorOverride: false, OverrideReason: null,
            ReconciledBy: null, ReconciliationNotes: null, ReconciledAt: null);
        await _sessions.AddAsync(session);
        return sessionId;
    }

    private async Task<(Payment PaymentA, Payment PaymentB)> SeedTwoPaymentsAsync()
    {
        var billId = await SeedBillAsync();
        var paymentA = new Payment(Guid.NewGuid(), billId, 100m, currencyCode: "TRY");
        var paymentB = new Payment(Guid.NewGuid(), billId, 5m, currencyCode: "TRY");
        await _payments.AddAsync(paymentA);
        await _payments.AddAsync(paymentB);
        return (paymentA, paymentB);
    }

    private async Task<Guid> SeedBillAsync()
    {
        var productId = await SeedProductAsync("Iskender", 100m);
        var tableId = await SeedTableAsync();
        var order = await CreateAndSaveOrderAsync(productId, "Iskender", 100m, tableId);

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
            taxRate: 0m);

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
