using ALKAROS.Invoicing.SourceSelection.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Invoicing.SourceSelection.Tests;

/// <summary>
/// V14-INV-001 on a real PostgreSQL: period closing, the Istanbul period window, the one-live-set-per-transaction
/// invariant, rerun behavior, cancellation, concurrency, and that the account ledger is never written.
/// </summary>
public sealed class InvoiceSourceSelectionTests : IAsyncLifetime
{
    // August 2026 in Istanbul (UTC+3): [2026-07-31T21:00Z, 2026-08-31T21:00Z).
    private static readonly DateOnly August = new(2026, 8, 1);
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateOnly July = new(2026, 7, 1);
    private static readonly DateTimeOffset AugustStartUtc = new(2026, 7, 31, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AugustEndUtc = new(2026, 8, 31, 21, 0, 0, TimeSpan.Zero);

    private readonly SourceSelectionTestDatabase _database = new();
    private readonly Guid _operator = Guid.NewGuid();
    private PostgresInvoiceSourceSelectionService _service = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _service = new PostgresInvoiceSourceSelectionService(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ClosingAPeriodRecordsWhoAndWhenAndClosingItAgainReturnsTheSamePeriod()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var closed = await _service.ClosePeriodAsync(August, September, _operator);
        var again = await _service.ClosePeriodAsync(August, September, Guid.NewGuid());

        Assert.Equal(August, closed.PeriodStart);
        Assert.Equal(September, closed.PeriodEnd);
        Assert.Equal(_operator, closed.ClosedBy);
        Assert.True(closed.ClosedAt >= before);
        Assert.Equal(closed, again);
        Assert.Equal(1, await _database.CountAsync("invoicing.invoice_periods"));
    }

    [Fact]
    public async Task AnOverlappingARunningOrAnEmptyPeriodCannotBeClosed()
    {
        await _service.ClosePeriodAsync(August, September, _operator);

        await Assert.ThrowsAsync<InvoicePeriodOverlapException>(
            () => _service.ClosePeriodAsync(new DateOnly(2026, 8, 15), new DateOnly(2026, 9, 15), _operator));
        await Assert.ThrowsAsync<InvoicePeriodRangeInvalidException>(
            () => _service.ClosePeriodAsync(September, September, _operator));

        var istanbulToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
        await Assert.ThrowsAsync<InvoicePeriodNotEndedException>(
            () => _service.ClosePeriodAsync(istanbulToday.AddDays(-3), istanbulToday.AddDays(1), _operator));
        Assert.Equal(1, await _database.CountAsync("invoicing.invoice_periods"));
    }

    [Fact]
    public async Task SelectionTakesOnlyThisCustomersUninvoicedRowsInsideTheIstanbulWindow()
    {
        var customer = Guid.NewGuid();
        var firstMinute = await _database.RecordAsync(customer, "Charge", 100m, AugustStartUtc);
        var charge = await _database.RecordAsync(customer, "Charge", 150m, AugustStartUtc.AddDays(10));
        var payment = await _database.RecordAsync(customer, "Payment", 80m, AugustEndUtc.AddSeconds(-1));
        await _database.RecordAsync(customer, "Charge", 999m, AugustStartUtc.AddSeconds(-1)); // July in Istanbul
        await _database.RecordAsync(customer, "Charge", 999m, AugustEndUtc); // September in Istanbul
        await _database.RecordAsync(customer, "Invoice", 999m, AugustStartUtc.AddDays(5)); // never re-invoiced
        await _database.RecordAsync(Guid.NewGuid(), "Charge", 999m, AugustStartUtc.AddDays(5)); // another customer
        var period = await _service.ClosePeriodAsync(August, September, _operator);

        var result = await _service.SelectAsync(period.PeriodId, customer);

        Assert.False(result.WasAlreadySelected);
        var set = result.SourceSet;
        Assert.Equal(InvoiceSourceSetStatus.Selected, set.Status);
        Assert.Equal([firstMinute, charge, payment], set.Lines.Select(line => line.TransactionId));
        // V0-DOM-007 section 4: charges 100 + 150, payment 80, invoice balance 170.
        Assert.Equal(250m, set.DebitTotal);
        Assert.Equal(80m, set.CreditTotal);
        Assert.Equal(170m, set.NetAmount);
    }

    [Fact]
    public async Task ASignedAdjustmentCountsByItsDirection()
    {
        var customer = Guid.NewGuid();
        await _database.RecordAsync(customer, "Charge", 200m, AugustStartUtc.AddDays(1));
        await _database.ExecuteAsync(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, note, created_by, occurred_at)
            VALUES (gen_random_uuid(), @customer_id, 'Adjustment', -20, 'Test', gen_random_uuid(), 'duzeltme', @created_by, @occurred_at);
            """,
            ("customer_id", customer),
            ("created_by", _operator),
            ("occurred_at", AugustStartUtc.AddDays(2)));
        var period = await _service.ClosePeriodAsync(August, September, _operator);

        var set = (await _service.SelectAsync(period.PeriodId, customer)).SourceSet;

        Assert.Equal(200m, set.DebitTotal);
        Assert.Equal(20m, set.CreditTotal);
        Assert.Equal(180m, set.NetAmount);
    }

    [Fact]
    public async Task ARerunReturnsTheSameLockedSetEvenAfterNewRowsInThePeriod()
    {
        var customer = Guid.NewGuid();
        await _database.RecordAsync(customer, "Charge", 100m, AugustStartUtc.AddDays(1));
        var period = await _service.ClosePeriodAsync(August, September, _operator);
        var first = await _service.SelectAsync(period.PeriodId, customer);
        await _database.RecordAsync(customer, "Charge", 40m, AugustStartUtc.AddDays(2));

        var rerun = await _service.SelectAsync(period.PeriodId, customer);

        Assert.True(rerun.WasAlreadySelected);
        Assert.Equal(first.SourceSet.SourceSetId, rerun.SourceSet.SourceSetId);
        Assert.Equal(first.SourceSet.Lines, rerun.SourceSet.Lines);
        Assert.Equal(100m, rerun.SourceSet.DebitTotal);
        Assert.Equal(1, await _database.CountAsync("invoicing.invoice_source_sets"));
    }

    [Fact]
    public async Task TheDatabaseRefusesATransactionInTwoLiveSets()
    {
        var customer = Guid.NewGuid();
        var charge = await _database.RecordAsync(customer, "Charge", 100m, AugustStartUtc.AddDays(1));
        var period = await _service.ClosePeriodAsync(August, September, _operator);
        await _service.SelectAsync(period.PeriodId, customer);
        var otherSet = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO invoicing.invoice_source_sets (source_set_id, period_id, customer_id, debit_total, credit_total, line_count)
            VALUES (@set_id, @period_id, @customer_id, 100, 0, 1);
            """,
            ("set_id", otherSet), ("period_id", period.PeriodId), ("customer_id", Guid.NewGuid()));

        var error = await Assert.ThrowsAsync<PostgresException>(() => _database.ExecuteAsync(
            """
            INSERT INTO invoicing.invoice_source_lines (source_set_id, transaction_id, transaction_type, direction, amount, occurred_at)
            VALUES (@set_id, @transaction_id, 'Charge', 'Debit', 100, now());
            """,
            ("set_id", otherSet), ("transaction_id", charge)));

        Assert.Equal("ux_invoice_source_lines_live_transaction", error.ConstraintName);
    }

    [Fact]
    public async Task CancellingASetFreesItsTransactionsAndKeepsTheRecord()
    {
        var customer = Guid.NewGuid();
        await _database.RecordAsync(customer, "Charge", 100m, AugustStartUtc.AddDays(1));
        var period = await _service.ClosePeriodAsync(August, September, _operator);
        var first = (await _service.SelectAsync(period.PeriodId, customer)).SourceSet;

        await _service.CancelAsync(first.SourceSetId, _operator, "Yanlis musteri");
        var second = await _service.SelectAsync(period.PeriodId, customer);

        Assert.False(second.WasAlreadySelected);
        Assert.NotEqual(first.SourceSetId, second.SourceSet.SourceSetId);
        Assert.Equal(first.Lines.Select(line => line.TransactionId), second.SourceSet.Lines.Select(line => line.TransactionId));
        var cancelled = await _service.GetSourceSetAsync(first.SourceSetId);
        Assert.Equal(InvoiceSourceSetStatus.Cancelled, cancelled!.Status);
        Assert.Single(cancelled.Lines);
        await Assert.ThrowsAsync<InvoiceSourceSetAlreadyCancelledException>(
            () => _service.CancelAsync(first.SourceSetId, _operator, "tekrar"));
    }

    [Fact]
    public async Task ConcurrentSelectionsOfOneCustomerResolveToOneSet()
    {
        var customer = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
            await _database.RecordAsync(customer, "Charge", 10m + i, AugustStartUtc.AddHours(i + 1));
        var period = await _service.ClosePeriodAsync(August, September, _operator);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => new PostgresInvoiceSourceSelectionService(_database.DataSource).SelectAsync(period.PeriodId, customer)));

        Assert.Single(results.Select(result => result.SourceSet.SourceSetId).Distinct());
        Assert.Single(results, result => !result.WasAlreadySelected);
        Assert.Equal(5, await _database.CountAsync("invoicing.invoice_source_lines"));
    }

    [Fact]
    public async Task SelectionNeverWritesTheAccountLedger()
    {
        var customer = Guid.NewGuid();
        await _database.RecordAsync(customer, "Charge", 100m, AugustStartUtc.AddDays(1));
        await _database.RecordAsync(customer, "Payment", 30m, AugustStartUtc.AddDays(2));
        const string LedgerFingerprint =
            "SELECT count(*)::text || ':' || coalesce(sum(amount), 0)::text FROM customer_account.account_transactions;";
        var before = await _database.ScalarAsync<string>(LedgerFingerprint);
        var period = await _service.ClosePeriodAsync(August, September, _operator);

        await _service.SelectAllAsync(period.PeriodId);

        Assert.Equal(before, await _database.ScalarAsync<string>(LedgerFingerprint));
    }

    [Fact]
    public async Task SelectAllSelectsEveryCustomerWithUninvoicedRowsAndSkipsTheRest()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await _database.RecordAsync(first, "Charge", 100m, AugustStartUtc.AddDays(1));
        await _database.RecordAsync(second, "Charge", 70m, AugustStartUtc.AddDays(3));
        await _database.RecordAsync(Guid.NewGuid(), "Charge", 50m, AugustEndUtc.AddDays(1)); // September only
        var period = await _service.ClosePeriodAsync(August, September, _operator);
        await _service.SelectAsync(period.PeriodId, first);

        var results = await _service.SelectAllAsync(period.PeriodId);

        var only = Assert.Single(results);
        Assert.Equal(second, only.SourceSet.CustomerId);
        Assert.Equal(2, await _database.CountAsync("invoicing.invoice_source_sets"));
    }

    [Fact]
    public async Task ACustomerWithNothingToInvoiceOrAnUnknownPeriodIsRefused()
    {
        var period = await _service.ClosePeriodAsync(August, September, _operator);

        await Assert.ThrowsAsync<NoInvoiceableTransactionsException>(
            () => _service.SelectAsync(period.PeriodId, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvoicePeriodNotFoundException>(
            () => _service.SelectAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(0, await _database.CountAsync("invoicing.invoice_source_sets"));
    }

    [Fact]
    public async Task ARowOfTheNextPeriodIsSelectedThereNotInTheClosedOne()
    {
        var customer = Guid.NewGuid();
        await _database.RecordAsync(customer, "Charge", 100m, AugustStartUtc.AddDays(-10));
        var august = await _database.RecordAsync(customer, "Charge", 60m, AugustStartUtc.AddDays(1));
        var julyPeriod = await _service.ClosePeriodAsync(July, August, _operator);
        await _service.SelectAsync(julyPeriod.PeriodId, customer);
        var augustPeriod = await _service.ClosePeriodAsync(August, September, _operator);

        var set = (await _service.SelectAsync(augustPeriod.PeriodId, customer)).SourceSet;

        Assert.Equal([august], set.Lines.Select(line => line.TransactionId));
    }
}
