using System.Data.Common;
using ALKAROS.Reporting.BusinessDayTotals;

namespace ALKAROS.Reporting.V1Operations;

/// <summary>
/// Domain service interface for operational business day management and EOD reports.
/// </summary>
public interface IOperationalReportService
{
    Task<BusinessDayRecord> OpenBusinessDayAsync(DateOnly businessDate, DateTimeOffset openedAt, CancellationToken cancellationToken = default);

    /// <summary>What a close of this business date would store and warn about right now.</summary>
    Task<BusinessDayFigures> PreviewCloseAsync(DateOnly businessDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the business day. Every figure (revenue, tips, orders, cancellations, print failures, the waiter, print
    /// station, table and cash breakdowns) is read by the server inside the closing transaction, never taken from the
    /// caller. Cash sessions, bills or payments still open do not block the close; they must be acknowledged, and are
    /// stored with the day. <paramref name="inTransaction"/> commits with the close (the audit event).
    /// </summary>
    Task<BusinessDayReportResult> CloseBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        bool acknowledgeOpenItems,
        Func<BusinessDayRecord, BusinessDayFigures, DbConnection, DbTransaction, CancellationToken, Task>? inTransaction = null,
        CancellationToken cancellationToken = default);

    Task<BusinessDayRecord?> GetBusinessDayByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<BusinessDayReportResult?> GetFullDailyReportAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain service implementation for V1 Operational Reports.
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

    public Task<BusinessDayFigures> PreviewCloseAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        => _totals.ReadAsync(businessDate, cancellationToken);

    public Task<BusinessDayReportResult> CloseBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        bool acknowledgeOpenItems,
        Func<BusinessDayRecord, BusinessDayFigures, DbConnection, DbTransaction, CancellationToken, Task>? inTransaction = null,
        CancellationToken cancellationToken = default)
    {
        // Business-day close plus waiter and print-station summaries are written in a single transaction; a
        // mid-sequence failure never leaves the day closed with partial summaries.
        return _repository.CloseBusinessDayWithFiguresAsync(
            businessDate,
            closedAt,
            acknowledgeOpenItems,
            (connection, transaction, token) => _totals.ReadAsync(businessDate, connection, transaction, token),
            inTransaction,
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
}
