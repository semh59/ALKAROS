using Npgsql;

namespace ALKAROS.Reporting.BusinessDayTotals;

/// <summary>
/// V1-RMD-421 (V1-RMD-393 F-10): the revenue and order count a business-day close stores, read from what was
/// recorded rather than taken from the caller.
/// </summary>
public interface IBusinessDayTotalsReader
{
    Task<RecordedBusinessDayTotals> ReadAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
}

/// <summary>What the business day recorded.</summary>
/// <param name="Revenue">Approved amount of every payment approved inside the business-day window.</param>
/// <param name="OrderCount">Orders submitted inside the window, excluding drafts, rejected and cancelled orders and
/// platform (online) orders, which carry no payment here and are reported per channel instead.</param>
public sealed record RecordedBusinessDayTotals(decimal Revenue, int OrderCount);

/// <summary>
/// The business-day window is the one the payment settlement report (V13-RPT-001) uses: 06:00 Europe/Istanbul on the
/// business date up to, not including, 06:00 the next day. Revenue follows that report's payment mix: payments whose
/// status is Approved, summed by approved amount.
/// </summary>
public sealed class PostgresBusinessDayTotalsReader : IBusinessDayTotalsReader
{
    private static readonly TimeZoneInfo BusinessZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private readonly NpgsqlDataSource _dataSource;

    public PostgresBusinessDayTotalsReader(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<RecordedBusinessDayTotals> ReadAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var (start, end) = Window(businessDate);

        await using var command = _dataSource.CreateCommand(
            """
            SELECT
                (SELECT COALESCE(SUM(approved_amount), 0)
                 FROM payments.payments
                 WHERE status = 'Approved' AND approved_at >= $1 AND approved_at < $2),
                (SELECT COUNT(*)
                 FROM orders.orders
                 WHERE submitted_at >= $1 AND submitted_at < $2
                   AND status NOT IN ('Draft', 'Rejected', 'Cancelled')
                   AND source <> 'Online');
            """);
        command.Parameters.AddWithValue(start.UtcDateTime);
        command.Parameters.AddWithValue(end.UtcDateTime);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new RecordedBusinessDayTotals(reader.GetDecimal(0), checked((int)reader.GetInt64(1)));
    }

    /// <summary>The business date's [start, end) window in UTC.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Window(DateOnly businessDate)
    {
        var localStart = businessDate.ToDateTime(new TimeOnly(6, 0));
        var localEnd = businessDate.AddDays(1).ToDateTime(new TimeOnly(6, 0));
        return (
            new DateTimeOffset(localStart, BusinessZone.GetUtcOffset(localStart)),
            new DateTimeOffset(localEnd, BusinessZone.GetUtcOffset(localEnd)));
    }
}
