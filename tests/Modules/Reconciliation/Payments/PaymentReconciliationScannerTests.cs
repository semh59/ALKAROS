using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace ALKAROS.Reconciliation.Payments.Tests;

/// <summary>
/// Integration tests for the V13-REC-001 source pairs and
/// <see cref="PaymentReconciliationScanner"/> against real Postgres. Each
/// real source pair detects its own scenario exactly once (no duplicate
/// case on re-scan); each disabled source pair reports itself as
/// unavailable without throwing; a mixed enabled/disabled scan always
/// completes.
/// </summary>
public sealed class PaymentReconciliationScannerTests : IClassFixture<PaymentReconciliationTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly ReconciliationService _reconciliationService;

    public PaymentReconciliationScannerTests(PaymentReconciliationTestDatabase database)
    {
        _dataSource = database.DataSource;
        _bills = new PostgresBillRepository(_dataSource);
        _orders = new PostgresOrderRepository(_dataSource);
        _payments = new PostgresPaymentRepository(_dataSource);
        _allocations = new PostgresPaymentAllocationRepository(_dataSource, new PostgresBillAdjustmentRepository(_dataSource));
        _reconciliationService = new ReconciliationService(new PostgresReconciliationRepository(_dataSource));
    }

    private PaymentReconciliationScanner BuildScanner(params IReconciliationSourcePair[] sources)
        => new(sources, _reconciliationService, NullLogger<PaymentReconciliationScanner>.Instance);

    [Fact]
    public async Task ApprovedWithoutAllocationSourceDetectsOnceAndIsNotReadAgainOnceItHasACase()
    {
        var billId = await SeedBillAsync(payable: 40m);
        var paymentId = await SeedApprovedPaymentWithoutAllocationAsync(billId, 40m);

        var scanner = BuildScanner(new ApprovedWithoutAllocationSourcePair(_dataSource));

        // The shared class-fixture database may carry approved-without-allocation payments of other tests.
        var expected = await CountApprovedWithoutAllocationAndCaseAsync();
        var firstScan = await scanner.ScanAllAsync();
        firstScan.Should().ContainSingle();
        firstScan[0].WasEnabled.Should().BeTrue();
        firstScan[0].CasesCreatedOrDeduplicated.Should().Be(expected);

        var caseRecord = await _reconciliationService.GetActiveCaseByDedupKeyAsync($"approved-without-allocation:{paymentId}");
        caseRecord.Should().NotBeNull();
        caseRecord!.CaseType.Should().Be(CaseType.PaymentMismatch);

        // Re-scan: the payment still has no allocation, but its case already exists, so it is not read again.
        (await scanner.ScanAllAsync())[0].CasesCreatedOrDeduplicated.Should().Be(0);
        var actions = await _reconciliationService.GetCaseActionsAsync(caseRecord.CaseId);
        actions.Should().ContainSingle(); // Created only: no Deduplicated action per scan.
    }

    [Fact]
    public async Task HuginUnknownSourceDetectsAPaymentStuckAtUnknown()
    {
        var billId = await SeedBillAsync(payable: 55m);
        var paymentId = await SeedUnknownPaymentAsync(billId, 55m);

        var scanner = BuildScanner(new PaymentUnknownSourcePair(_dataSource));
        var results = await scanner.ScanAllAsync();

        results.Should().ContainSingle();

        var caseRecord = await _reconciliationService.GetActiveCaseByDedupKeyAsync($"hugin-unknown:{paymentId}");
        caseRecord.Should().NotBeNull();
        caseRecord!.Severity.Should().Be(CaseSeverity.Critical);

        (await scanner.ScanAllAsync())[0].CasesCreatedOrDeduplicated.Should().Be(0);
        (await _reconciliationService.GetCaseActionsAsync(caseRecord.CaseId)).Should().ContainSingle();
    }

    [Fact]
    public async Task AllocationProviderMismatchSourceDetectsADriftedAllocationAmount()
    {
        var attemptId = await SeedDriftedCardAttemptAsync();

        var scanner = BuildScanner(new CardSettlementAllocationMismatchSourcePair(_dataSource));
        var results = await scanner.ScanAllAsync();

        results.Should().ContainSingle();
        results[0].CasesCreatedOrDeduplicated.Should().BeGreaterThanOrEqualTo(1);

        var caseRecord = await _reconciliationService.GetActiveCaseByDedupKeyAsync($"allocation-provider-mismatch:{attemptId}");
        caseRecord.Should().NotBeNull();
        caseRecord!.DiscrepancyAmount.Should().Be(5m);
    }

    [Fact]
    public async Task CashSessionDifferenceSourceDetectsANonZeroClosedSessionDifference()
    {
        var sessionId = await SeedClosedCashSessionWithDifferenceAsync(expected: 500m, actual: 480m);

        var scanner = BuildScanner(new CashSessionDifferenceSourcePair(_dataSource));
        var results = await scanner.ScanAllAsync();

        results.Should().ContainSingle();
        results[0].CasesCreatedOrDeduplicated.Should().Be(1);

        var caseRecord = await _reconciliationService.GetActiveCaseByDedupKeyAsync($"cash-difference:{sessionId}");
        caseRecord.Should().NotBeNull();
        caseRecord!.CaseType.Should().Be(CaseType.CashVariance);
        caseRecord.DiscrepancyAmount.Should().Be(20m);
    }

    [Fact]
    public async Task CashSessionWithZeroDifferenceProducesNoCase()
    {
        await SeedClosedCashSessionWithDifferenceAsync(expected: 300m, actual: 300m);

        var scanner = BuildScanner(new CashSessionDifferenceSourcePair(_dataSource));
        var results = await scanner.ScanAllAsync();

        results.Should().ContainSingle();
        results[0].CasesCreatedOrDeduplicated.Should().Be(0);
    }

    [Fact]
    public async Task ACashDifferenceThatAlreadyHasACaseIsNotReadAgain()
    {
        var sessionId = await SeedClosedCashSessionWithDifferenceAsync(expected: 500m, actual: 470m);
        var scanner = BuildScanner(new CashSessionDifferenceSourcePair(_dataSource));

        (await scanner.ScanAllAsync())[0].CasesCreatedOrDeduplicated.Should().Be(1);
        (await scanner.ScanAllAsync())[0].CasesCreatedOrDeduplicated.Should().Be(0);

        var caseRecord = await _reconciliationService.GetActiveCaseByDedupKeyAsync($"cash-difference:{sessionId}");
        (await _reconciliationService.GetCaseActionsAsync(caseRecord!.CaseId)).Should().ContainSingle();
    }

    [Fact]
    public async Task AResolvedCaseKeepsItsCashDifferenceOutOfLaterScansAndOpensNoSecondCase()
    {
        var sessionId = await SeedClosedCashSessionWithDifferenceAsync(expected: 500m, actual: 460m);
        var scanner = BuildScanner(new CashSessionDifferenceSourcePair(_dataSource));
        await scanner.ScanAllAsync();
        await using (var resolve = _dataSource.CreateCommand(
            "UPDATE reconciliation.cases SET status = 'Resolved', resolved_at = now() WHERE deduplication_key = @key;"))
        {
            resolve.Parameters.AddWithValue("key", $"cash-difference:{sessionId}");
            await resolve.ExecuteNonQueryAsync();
        }

        (await scanner.ScanAllAsync())[0].CasesCreatedOrDeduplicated.Should().Be(0);

        (await _reconciliationService.GetActiveCaseByDedupKeyAsync($"cash-difference:{sessionId}")).Should().BeNull();
    }

    [Fact]
    public async Task ADriftedCardAttemptThatAlreadyHasACaseIsNotReadAgain()
    {
        var attemptId = await SeedDriftedCardAttemptAsync();
        var scanner = BuildScanner(new CardSettlementAllocationMismatchSourcePair(_dataSource));

        (await scanner.ScanAllAsync())[0].CasesCreatedOrDeduplicated.Should().BeGreaterThanOrEqualTo(1);
        (await scanner.ScanAllAsync())[0].CasesCreatedOrDeduplicated.Should().Be(0);

        var caseRecord = await _reconciliationService.GetActiveCaseByDedupKeyAsync($"allocation-provider-mismatch:{attemptId}");
        (await _reconciliationService.GetCaseActionsAsync(caseRecord!.CaseId)).Should().ContainSingle();
    }

    [Theory]
    [InlineData(400, 100, CaseSeverity.High)]
    [InlineData(400.01, 99.99, CaseSeverity.Medium)]
    public async Task ACashDifferenceFromOneHundredLiraIsHighSeverity(double actual, double expectedAmount, CaseSeverity severity)
    {
        var sessionId = await SeedClosedCashSessionWithDifferenceAsync(expected: 500m, actual: (decimal)actual);

        await BuildScanner(new CashSessionDifferenceSourcePair(_dataSource)).ScanAllAsync();

        var caseRecord = await _reconciliationService.GetActiveCaseByDedupKeyAsync($"cash-difference:{sessionId}");
        caseRecord!.Severity.Should().Be(severity);
        caseRecord.DiscrepancyAmount.Should().Be((decimal)expectedAmount);
    }

    [Fact]
    public async Task AFailingSourceReportsAFixedReasonNeverTheRawExceptionText()
    {
        var results = await BuildScanner(new ThrowingSourcePair()).ScanAllAsync();

        results.Should().ContainSingle();
        results[0].FailureReason.Should().Be(PaymentReconciliationScanner.SourceUnreadableReason);
    }

    [Fact]
    public async Task AFailingSourceIsLoggedWithItsNameAndTheOriginalException()
    {
        var logger = new CapturingLogger();
        var scanner = new PaymentReconciliationScanner([new ThrowingSourcePair()], _reconciliationService, logger);

        await scanner.ScanAllAsync();

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().Contain("Throwing");
        entry.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task DisabledSourceReportsItselfWithoutThrowingOrCreatingACase()
    {
        var scanner = BuildScanner(
            new DisabledReconciliationSourcePair("FiscalMismatch", "V13-FSC-001/002 bloke."));

        var results = await scanner.ScanAllAsync();

        results.Should().ContainSingle();
        results[0].WasEnabled.Should().BeFalse();
        results[0].DisabledReason.Should().Be("V13-FSC-001/002 bloke.");
        results[0].CasesCreatedOrDeduplicated.Should().Be(0);
    }

    [Fact]
    public async Task AMixedEnabledAndDisabledScanCompletesAndReportsBothIndependently()
    {
        var billId = await SeedBillAsync(payable: 25m);
        await SeedApprovedPaymentWithoutAllocationAsync(billId, 25m);

        var scanner = BuildScanner(
            new ApprovedWithoutAllocationSourcePair(_dataSource),
            new DisabledReconciliationSourcePair("MealCardSettlementMismatch", "V13-MCD-002/004 bloke."));

        var results = await scanner.ScanAllAsync();

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.SourceName == "ApprovedWithoutAllocation" && r.WasEnabled && r.CasesCreatedOrDeduplicated >= 1);
        results.Should().Contain(r => r.SourceName == "MealCardSettlementMismatch" && !r.WasEnabled);
    }

    // --- Seeding helpers (mirrors ALKAROS.Payments.CardSettlement.Tests's own established pattern) ---

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
            source: OrderSource.Cashier,
            orderNumber: "ORD-" + Guid.NewGuid().ToString("N")[..8],
            items: [item],
            tableId: tableId);

        await _orders.AddAsync(order);
        return order;
    }

    private async Task<Guid> SeedBillAsync(decimal payable)
    {
        var productId = await SeedProductAsync("Test Item", payable);
        var tableId = await SeedTableAsync();
        var order = await CreateAndSaveOrderAsync(productId, "Test Item", payable, tableId);

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

    private async Task<Guid> SeedApprovedPaymentWithoutAllocationAsync(Guid billId, decimal amount)
    {
        var payment = new Payment(Guid.NewGuid(), billId, amount)
            .Tender(amount)
            .Approve(amount);
        await _payments.AddAsync(payment);
        return payment.Id;
    }

    private async Task<Guid> SeedUnknownPaymentAsync(Guid billId, decimal amount)
    {
        var payment = new Payment(Guid.NewGuid(), billId, amount)
            .Tender(amount)
            .MarkUnknown("provider_timeout");
        await _payments.AddAsync(payment);
        return payment.Id;
    }

    private async Task<int> CountApprovedWithoutAllocationAndCaseAsync()
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT count(*) FROM payments.payments p
            WHERE p.status = 'Approved'
              AND NOT EXISTS (SELECT 1 FROM payments.payment_allocations pa WHERE pa.payment_id = p.payment_id)
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases c WHERE c.deduplication_key = 'approved-without-allocation:' || p.payment_id::text);
            """);
        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class CapturingLogger : ILogger<PaymentReconciliationScanner>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class ThrowingSourcePair : IReconciliationSourcePair
    {
        public string Name => "Throwing";
        public bool IsEnabled => true;
        public string? DisabledReason => null;
        public Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("connection string server=10.0.0.5 password=hunter2");
    }

    private async Task<Guid> SeedDriftedCardAttemptAsync()
    {
        var billId = await SeedBillAsync(payable: 90m);
        var payment = new Payment(Guid.NewGuid(), billId, 90m).Tender(90m).Approve(90m);
        await _payments.AddAsync(payment);
        var allocation = await _allocations.AllocateAsync(
            payment, await _bills.GetByIdAsync(billId) ?? throw new InvalidOperationException("Bill not found."),
            90m, Guid.NewGuid().ToString());

        // Simulate a card_settlement_attempts row whose own provider-approved
        // amount has since drifted from the allocation it originally produced.
        var attemptId = Guid.NewGuid();
        await using (var command = _dataSource.CreateCommand(
            """
            INSERT INTO payments.card_settlement_attempts
                (card_settlement_attempt_id, bill_id, idempotency_key, provider_correlation_id, payment_id,
                 outcome, approved_amount, allocation_id, fiscal_handoff_queued, created_at)
            VALUES (@id, @bill, @key, @corr, @payment, 'Approved', @approved, @allocation, true, now());
            """))
        {
            command.Parameters.AddWithValue("id", attemptId);
            command.Parameters.AddWithValue("bill", billId);
            command.Parameters.AddWithValue("key", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("corr", "TXN-DRIFT");
            command.Parameters.AddWithValue("payment", payment.Id);
            command.Parameters.AddWithValue("approved", 85m); // Drifted from the real allocation (90m).
            command.Parameters.AddWithValue("allocation", allocation.Id);
            await command.ExecuteNonQueryAsync();
        }
        return attemptId;
    }

    private async Task<Guid> SeedClosedCashSessionWithDifferenceAsync(decimal expected, decimal actual)
    {
        var sessionId = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO cash.cash_sessions
                (cash_session_id, cashier_user_id, terminal_id, status, opening_balance,
                 expected_cash, actual_cash, difference, opened_at, closed_at, closed_by, created_at, updated_at)
            VALUES
                (@id, @cashier, @terminal, 'Closed', 0, @expected, @actual, @difference, now(), now(), @cashier, now(), now());
            """);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("cashier", Guid.NewGuid());
        command.Parameters.AddWithValue("terminal", Guid.NewGuid());
        command.Parameters.AddWithValue("expected", expected);
        command.Parameters.AddWithValue("actual", actual);
        command.Parameters.AddWithValue("difference", actual - expected);
        await command.ExecuteNonQueryAsync();
        return sessionId;
    }
}
