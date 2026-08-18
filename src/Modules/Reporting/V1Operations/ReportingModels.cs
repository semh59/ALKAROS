namespace ALKAROS.Reporting.V1Operations;

/// <summary>
/// Domain record for a daily operational business day (V1-RPT-001, PDF:II.2.20).
/// </summary>
public sealed record BusinessDayRecord(
    Guid BusinessDayId,
    DateOnly BusinessDate,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    BusinessDayStatus Status,
    decimal TotalRevenue,
    int TotalOrdersCount,
    int TotalCancelledItemsCount,
    int TotalPrintFailuresCount);

/// <summary>
/// Aggregated performance metrics for a waiter during a business day (V1-RPT-001, PDF:III.31).
/// </summary>
public sealed record WaiterPerformanceRecord(
    Guid SummaryId,
    DateOnly BusinessDate,
    Guid WaiterUserId,
    int OrdersServedCount,
    decimal TotalSalesAmount,
    int CancellationsCount,
    decimal DiscountsAppliedAmount,
    DateTimeOffset CapturedAt);

/// <summary>
/// Aggregated summary of printing errors per kitchen station for a business day (V1-RPT-001, PDF:II.10).
/// </summary>
public sealed record PrintErrorSummaryRecord(
    Guid ErrorSummaryId,
    DateOnly BusinessDate,
    string StationName,
    int TotalPrintJobs,
    int FailedPrintJobs,
    int RecoveredPrintJobs,
    DateTimeOffset CapturedAt);

/// <summary>
/// Time zone service day window filter (V1-RPT-001, Acceptance Evidence).
/// </summary>
public sealed record ServiceWindowFilter(
    DateOnly BusinessDate,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string TimeZoneId = "Europe/Istanbul");

/// <summary>
/// Consolidated end-of-day operational report result (V1-RPT-001).
/// </summary>
public sealed record BusinessDayReportResult(
    BusinessDayRecord BusinessDay,
    IReadOnlyList<WaiterPerformanceRecord> WaiterSummaries,
    IReadOnlyList<PrintErrorSummaryRecord> PrintSummaries);
