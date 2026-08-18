namespace ALKAROS.Reporting.V1Operations;

/// <summary>
/// Domain service interface for operational business day management and EOD reports (V1-RPT-001).
/// </summary>
public interface IOperationalReportService
{
    Task<BusinessDayRecord> OpenBusinessDayAsync(DateOnly businessDate, DateTimeOffset openedAt, CancellationToken cancellationToken = default);
    Task<BusinessDayReportResult> CloseBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        decimal totalRevenue,
        int totalOrders,
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

    public OperationalReportService(IOperationalReportRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<BusinessDayRecord> OpenBusinessDayAsync(DateOnly businessDate, DateTimeOffset openedAt, CancellationToken cancellationToken = default)
    {
        return _repository.OpenBusinessDayAsync(businessDate, openedAt, cancellationToken);
    }

    public async Task<BusinessDayReportResult> CloseBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        decimal totalRevenue,
        int totalOrders,
        int cancelledItems,
        int printFailures,
        IReadOnlyList<WaiterPerformanceRecord>? waiterSummaries = null,
        IReadOnlyList<PrintErrorSummaryRecord>? printSummaries = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Close Business Day
        var closedDay = await _repository.CloseBusinessDayAsync(
            businessDate,
            closedAt,
            totalRevenue,
            totalOrders,
            cancelledItems,
            printFailures,
            cancellationToken);

        // 2. Persist Waiter Summaries
        if (waiterSummaries is not null)
        {
            foreach (var waiter in waiterSummaries)
            {
                await _repository.RecordWaiterSummaryAsync(waiter, cancellationToken);
            }
        }

        // 3. Persist Print Summaries
        if (printSummaries is not null)
        {
            foreach (var print in printSummaries)
            {
                await _repository.RecordPrintErrorSummaryAsync(print, cancellationToken);
            }
        }

        var persistedWaiters = await _repository.GetWaiterSummariesByDateAsync(businessDate, cancellationToken);
        var persistedPrints = await _repository.GetPrintErrorSummariesByDateAsync(businessDate, cancellationToken);

        return new BusinessDayReportResult(closedDay, persistedWaiters, persistedPrints);
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
