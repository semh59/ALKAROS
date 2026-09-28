using ALKAROS.Reporting.BusinessDayTotals;

namespace ALKAROS.Reporting.V1Operations;

/// <summary>
/// Domain service interface for operational business day management and EOD reports (V1-RPT-001).
/// </summary>
public interface IOperationalReportService
{
    Task<BusinessDayRecord> OpenBusinessDayAsync(DateOnly businessDate, DateTimeOffset openedAt, CancellationToken cancellationToken = default);
    /// <summary>
    /// Closes the business day. Revenue and order count are read from the recorded payments and orders
    /// (V1-RMD-421, V1-RMD-393 F-10), never taken from the caller.
    /// </summary>
    Task<BusinessDayReportResult> CloseBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        int cancelledItems,
        int printFailures,
        IReadOnlyList<WaiterPerformanceRecord>? waiterSummaries = null,
        IReadOnlyList<PrintErrorSummaryRecord>? printSummaries = null,
        CancellationToken cancellationToken = default);
    Task<BusinessDayRecord?> GetBusinessDayByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<BusinessDayReportResult?> GetFullDailyReportAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    ServiceWindowFilter CalculateServiceWindow(DateOnly businessDate, TimeZoneInfo? timeZone = null);
}

/// <summary>
/// Domain service implementation for V1 Operational Reports (V1-RPT-001).
/// </summary>
public sealed class OperationalReportService : IOperationalReportService
{
    private readonly IOperationalReportRepository _repository;
    private readonly IBusinessDayTotalsReader _totals;

    public OperationalReportService(IOperationalReportRepository repository, IBusinessDayTotalsReader totals)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _totals = totals ?? throw new ArgumentNullException(nameof(totals));
    }

    public Task<BusinessDayRecord> OpenBusinessDayAsync(DateOnly businessDate, DateTimeOffset openedAt, CancellationToken cancellationToken = default)
    {
        return _repository.OpenBusinessDayAsync(businessDate, openedAt, cancellationToken);
    }

    public async Task<BusinessDayReportResult> CloseBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        int cancelledItems,
        int printFailures,
        IReadOnlyList<WaiterPerformanceRecord>? waiterSummaries = null,
        IReadOnlyList<PrintErrorSummaryRecord>? printSummaries = null,
        CancellationToken cancellationToken = default)
    {
        // Business-day close plus waiter and print-error summaries are written in a single
        // transaction; a mid-sequence failure never leaves the day closed with partial
        // summaries (V1-RMD-085).
        var totals = await _totals.ReadAsync(businessDate, cancellationToken);
        return await _repository.CloseBusinessDayWithSummariesAsync(
            businessDate,
            closedAt,
            totals.Revenue,
            totals.OrderCount,
            cancelledItems,
            printFailures,
            waiterSummaries ?? Array.Empty<WaiterPerformanceRecord>(),
            printSummaries ?? Array.Empty<PrintErrorSummaryRecord>(),
            cancellationToken);
    }

    public Task<BusinessDayRecord?> GetBusinessDayByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        return _repository.GetBusinessDayByDateAsync(businessDate, cancellationToken);
    }

    public async Task<BusinessDayReportResult?> GetFullDailyReportAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var day = await _repository.GetBusinessDayByDateAsync(businessDate, cancellationToken);
        if (day is null) return null;

        var waiters = await _repository.GetWaiterSummariesByDateAsync(businessDate, cancellationToken);
        var prints = await _repository.GetPrintErrorSummariesByDateAsync(businessDate, cancellationToken);

        return new BusinessDayReportResult(day, waiters, prints);
    }

    public ServiceWindowFilter CalculateServiceWindow(DateOnly businessDate, TimeZoneInfo? timeZone = null)
    {
        var tz = timeZone ?? TimeZoneInfo.Utc;
        var startDateTime = businessDate.ToDateTime(new TimeOnly(6, 0, 0)); // Service day starts at 06:00
        var endDateTime = businessDate.AddDays(1).ToDateTime(new TimeOnly(5, 59, 59, 999)); // Ends next day 05:59:59

        var startOffset = new DateTimeOffset(startDateTime, tz.GetUtcOffset(startDateTime));
        var endOffset = new DateTimeOffset(endDateTime, tz.GetUtcOffset(endDateTime));

        return new ServiceWindowFilter(businessDate, startOffset, endOffset, tz.Id);
    }
}
