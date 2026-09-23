using ALKAROS.Reporting.Payments.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Reporting.Payments.Tests;

public sealed class PaymentSettlementReportTests : IAsyncLifetime
{
    private static readonly DateOnly BusinessDate = new(2026, 6, 15);
    private static readonly DateTimeOffset InWindow = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OutsideWindow = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly PaymentReportTestDatabase _database = new();
    private PostgresPaymentSettlementReportRepository _repository = null!;
    private PaymentSettlementReportService _service = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _repository = new PostgresPaymentSettlementReportRepository(_database.DataSource);
        _service = new PaymentSettlementReportService(_repository);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task PaymentMixBucketsCashBankCardAndEftSeparatelyByInference()
    {
        var bill = await _database.SeedBillAsync(300m);

        var cashPayment = await _database.SeedPaymentAsync(bill, "Approved", 50m, InWindow);
        var cashSession = await _database.SeedCashSessionAsync(Guid.NewGuid(), "Open", 0, 0, InWindow);
        await _database.SeedCashSaleTransactionAsync(cashSession, cashPayment, 50m);

        var cardPayment = await _database.SeedPaymentAsync(bill, "Approved", 75m, InWindow);
        await _database.SeedCardSettlementAttemptAsync(cardPayment, bill, 75m);

        var eftPayment = await _database.SeedPaymentAsync(bill, "Approved", 40m, InWindow);

        var result = await _service.GetReportAsync(new PaymentSettlementReportFilter(BusinessDate));

        result.PaymentMix.Should().HaveCount(3);
        result.PaymentMix.Should().ContainSingle(e => e.Method == "Cash" && e.ApprovedAmount == 50m && e.ApprovedCount == 1);
        result.PaymentMix.Should().ContainSingle(e => e.Method == "BankCard" && e.ApprovedAmount == 75m && e.ApprovedCount == 1);
        result.PaymentMix.Should().ContainSingle(e => e.Method == "Eft" && e.ApprovedAmount == 40m && e.ApprovedCount == 1);
    }

    [Fact]
    public async Task PaymentMixExcludesApprovedPaymentsOutsideTheBusinessDateWindow()
    {
        var bill = await _database.SeedBillAsync(50m);
        await _database.SeedPaymentAsync(bill, "Approved", 50m, OutsideWindow);

        var result = await _service.GetReportAsync(new PaymentSettlementReportFilter(BusinessDate));

        result.PaymentMix.Should().BeEmpty();
    }

    [Fact]
    public async Task UnsettledPaymentsAreShownSeparatelyAndNeverInPaymentMix()
    {
        var bill = await _database.SeedBillAsync(200m);
        await _database.SeedPaymentAsync(bill, "Unknown", 30m, InWindow);
        await _database.SeedPaymentAsync(bill, "ReconciliationRequired", 45m, InWindow);
        await _database.SeedPaymentAsync(bill, "Approved", 20m, InWindow);

        var result = await _service.GetReportAsync(new PaymentSettlementReportFilter(BusinessDate));

        result.UnsettledPayments.UnknownCount.Should().Be(1);
        result.UnsettledPayments.UnknownAmount.Should().Be(30m);
        result.UnsettledPayments.ReconciliationRequiredCount.Should().Be(1);
        result.UnsettledPayments.ReconciliationRequiredAmount.Should().Be(45m);
        // Only the genuinely Approved payment appears in the mix — Unknown/
        // ReconciliationRequired never get silently folded into a method bucket.
        result.PaymentMix.Sum(e => e.ApprovedCount).Should().Be(1);
    }

    [Fact]
    public async Task CashSessionsReportDifferenceAndFlagOpenSessionsDistinctly()
    {
        var terminal = Guid.NewGuid();
        await _database.SeedCashSessionAsync(terminal, "Open", 100m, 100m, InWindow);
        await _database.SeedCashSessionAsync(terminal, "Closed", 200m, 195m, InWindow);

        var result = await _service.GetReportAsync(new PaymentSettlementReportFilter(BusinessDate));

        result.CashSessions.Should().HaveCount(2);
        result.CashSessions.Should().ContainSingle(s => s.Status == "Open" && s.IsOpen);
        result.CashSessions.Should().ContainSingle(s => s.Status == "Closed" && !s.IsOpen && s.Difference == -5m);
    }

    [Fact]
    public async Task CashSessionsTerminalFilterNarrowsToOneTerminal()
    {
        var terminalA = Guid.NewGuid();
        var terminalB = Guid.NewGuid();
        await _database.SeedCashSessionAsync(terminalA, "Closed", 100m, 100m, InWindow);
        await _database.SeedCashSessionAsync(terminalB, "Closed", 100m, 90m, InWindow);

        var result = await _service.GetReportAsync(new PaymentSettlementReportFilter(BusinessDate, TerminalId: terminalA));

        result.CashSessions.Should().ContainSingle();
        result.CashSessions.Single().TerminalId.Should().Be(terminalA);
    }

    [Fact]
    public async Task ReconciliationTotalsGroupByCaseTypeSeparatingOpenAndResolved()
    {
        await _database.SeedReconciliationCaseAsync("PaymentMismatch", "Open", 25m, InWindow);
        await _database.SeedReconciliationCaseAsync("PaymentMismatch", "Resolved", 15m, InWindow);
        await _database.SeedReconciliationCaseAsync("CashVariance", "Open", 5m, InWindow);

        var result = await _service.GetReportAsync(new PaymentSettlementReportFilter(BusinessDate));

        var paymentMismatch = result.ReconciliationTotals.Single(t => t.CaseType == "PaymentMismatch");
        paymentMismatch.OpenCount.Should().Be(1);
        paymentMismatch.ResolvedCount.Should().Be(1);
        paymentMismatch.OpenDiscrepancyAmount.Should().Be(25m);

        var cashVariance = result.ReconciliationTotals.Single(t => t.CaseType == "CashVariance");
        cashVariance.OpenCount.Should().Be(1);
        cashVariance.OpenDiscrepancyAmount.Should().Be(5m);
    }

    [Fact]
    public async Task EmptyBusinessDateReturnsWellFormedZeroResultWithoutThrowing()
    {
        var result = await _service.GetReportAsync(new PaymentSettlementReportFilter(BusinessDate));

        result.PaymentMix.Should().BeEmpty();
        result.UnsettledPayments.UnknownCount.Should().Be(0);
        result.UnsettledPayments.ReconciliationRequiredAmount.Should().Be(0m);
        result.CashSessions.Should().BeEmpty();
        result.ReconciliationTotals.Should().BeEmpty();
    }

    [Fact]
    public async Task DisabledSectionsAreAlwaysReportedHonestlyRegardlessOfData()
    {
        var result = await _service.GetReportAsync(new PaymentSettlementReportFilter(BusinessDate));

        result.NetRefunds.BlockedBy.Should().Be("V13-ALC-004");
        result.NetRefunds.Reason.Should().NotBeNullOrWhiteSpace();
        result.FiscalStatus.BlockedBy.Should().Be("V13-FSC-001");
        result.MealCardClosure.BlockedBy.Should().Be("V13-MCD-002");
    }

    [Fact]
    public void FilterRejectsAnUnknownTimeZoneId()
    {
        var filter = new PaymentSettlementReportFilter(BusinessDate, TimeZoneId: "Not/AZone");
        var act = () => filter.Validate();
        act.Should().Throw<ArgumentException>();
    }
}
