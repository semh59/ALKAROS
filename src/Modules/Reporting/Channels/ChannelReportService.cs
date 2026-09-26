using System.Globalization;
using Npgsql;

namespace ALKAROS.Reporting.Channels;

public interface IChannelReportService
{
    Task<ChannelReport> GetReportAsync(ChannelReportFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// V12-RPT-001: QR and online channel volume, value, rejections/cancellations and reconciliation
/// difference per business date, derived read-only from the ledgers (V0-DOM-008 rule 1):
/// <c>orders.orders</c> for the orders, the Yemeksepeti webhook inbox for provider orders that never
/// became a local order, and <c>reconciliation.cases</c>/<c>online_order_retry_attempts</c>
/// (V12-REC-001) for the reconciliation difference and retries. The same range and ledger state always
/// produce the same numbers.
/// </summary>
public sealed class PostgresChannelReportService : IChannelReportService
{
    public const string ReportVersion = "channel-report.v1";

    // At most 31 dates x 2 channels of order rows, and a handful of reconciliation kinds per date.
    private const int MaxRows = 1000;

    private const string AwaitingStatuses = "'Draft', 'Submitted', 'PendingConfirmation'";
    private const string AcceptedStatuses = "'Accepted', 'Preparing', 'Ready', 'Served', 'Completed'";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresChannelReportService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ChannelReport> GetReportAsync(ChannelReportFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Validate();
        var (start, end) = filter.ResolveWindow();
        var includesOnline = filter.Source is null or ChannelSource.Online;

        var days = await ReadDaysAsync(filter, start, end, cancellationToken).ConfigureAwait(false);
        if (includesOnline)
            days = await AddProviderRefusalsAsync(days, filter, start, end, cancellationToken).ConfigureAwait(false);
        var reconciliation = includesOnline
            ? await ReadReconciliationAsync(filter, start, end, cancellationToken).ConfigureAwait(false)
            : [];
        var retries = includesOnline ? await CountRetriesAsync(start, end, cancellationToken).ConfigureAwait(false) : 0;
        var check = await CheckAsync(filter, start, end, days, cancellationToken).ConfigureAwait(false);

        return new ChannelReport(ReportVersion, filter.From, filter.To, filter.Source, filter.TimeZoneId,
            days, reconciliation, retries, check);
    }

    private async Task<List<ChannelDayRow>> ReadDaysAsync(
        ChannelReportFilter filter, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT (o.created_at AT TIME ZONE $3)::date AS business_date, o.source,
                   count(*)::int,
                   count(*) FILTER (WHERE o.status IN ({AwaitingStatuses}))::int,
                   count(*) FILTER (WHERE o.status IN ({AcceptedStatuses}))::int,
                   count(*) FILTER (WHERE o.status = 'Rejected')::int,
                   count(*) FILTER (WHERE o.status = 'Cancelled')::int,
                   COALESCE(sum(o.total) FILTER (WHERE o.status IN ({AcceptedStatuses})), 0),
                   COALESCE(sum(o.subtotal - o.discount_total) FILTER (WHERE o.status IN ({AcceptedStatuses})), 0),
                   COALESCE(sum(o.tax_total) FILTER (WHERE o.status IN ({AcceptedStatuses})), 0),
                   COALESCE(sum(o.discount_total) FILTER (WHERE o.status IN ({AcceptedStatuses})), 0),
                   COALESCE(sum(o.total) FILTER (WHERE o.status = 'Cancelled'), 0)
            FROM orders.orders o
            WHERE o.source IN ('Qr', 'Online')
              AND ($4::text IS NULL OR o.source = $4)
              AND o.created_at >= $1 AND o.created_at < $2
            GROUP BY 1, 2
            ORDER BY 1, 2
            LIMIT {MaxRows + 1};
            """);
        AddWindow(command, filter, start, end);
        command.Parameters.AddWithValue((object?)filter.Source ?? DBNull.Value).NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;

        var rows = new List<ChannelDayRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ChannelDayRow(
                DateOnly.FromDateTime(reader.GetDateTime(0)), reader.GetString(1),
                reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6),
                reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10),
                reader.GetDecimal(11), 0));
        }

        EnsureBounded(rows.Count, "channel order");
        return rows;
    }

    /// <summary>
    /// Provider orders refused locally (unmapped item, no stock) that never became a local order, once per
    /// provider order: a repeated webhook or a second refused event for the same order is still one, and an
    /// event that was reprocessed into an order afterwards is no longer a refusal.
    /// </summary>
    private async Task<List<ChannelDayRow>> AddProviderRefusalsAsync(
        List<ChannelDayRow> days, ChannelReportFilter filter, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT business_date, count(*)::int
            FROM (
                SELECT i.external_order_id, (min(i.received_at) AT TIME ZONE $3)::date AS business_date
                FROM online_ordering.provider_inbox i
                WHERE i.processing_outcome IN ('Rejected', 'Diverged')
                  AND i.order_id IS NULL
                  -- V12-RMD-006: only events before the window's end can decide an order's first refusal time
                  -- there (the minimum below is unchanged by it), so the inbox is never scanned whole.
                  AND i.received_at < $2
                  AND NOT EXISTS (SELECT 1 FROM orders.orders o
                                  WHERE o.source = 'Online' AND o.source_external_id = i.external_order_id)
                GROUP BY i.external_order_id
                HAVING min(i.received_at) >= $1 AND min(i.received_at) < $2
            ) refused
            GROUP BY business_date
            ORDER BY business_date
            LIMIT {MaxRows + 1};
            """);
        AddWindow(command, filter, start, end);

        var refusals = new Dictionary<DateOnly, int>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                refusals[DateOnly.FromDateTime(reader.GetDateTime(0))] = reader.GetInt32(1);
        }

        EnsureBounded(refusals.Count, "provider refusal");
        foreach (var (date, count) in refusals)
        {
            var index = days.FindIndex(row => row.BusinessDate == date && row.Source == ChannelSource.Online);
            if (index >= 0)
                days[index] = days[index] with { ProviderRefused = count };
            else
                days.Add(new ChannelDayRow(date, ChannelSource.Online, 0, 0, 0, 0, 0, 0m, 0m, 0m, 0m, 0m, count));
        }

        return days.OrderBy(row => row.BusinessDate).ThenBy(row => row.Source, StringComparer.Ordinal).ToList();
    }

    private async Task<IReadOnlyList<ChannelReconciliationRow>> ReadReconciliationAsync(
        ChannelReportFilter filter, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT (c.opened_at AT TIME ZONE $3)::date, COALESCE(c.details->>'kind', 'Unclassified'),
                   count(*) FILTER (WHERE c.status IN ('Open', 'Investigating', 'Escalated'))::int,
                   count(*) FILTER (WHERE c.status IN ('Resolved', 'Dismissed'))::int,
                   COALESCE(sum(c.discrepancy_amount) FILTER (WHERE c.status IN ('Open', 'Investigating', 'Escalated')), 0)
            FROM reconciliation.cases c
            WHERE c.case_type = 'OnlineOrderMismatch'
              AND c.opened_at >= $1 AND c.opened_at < $2
            GROUP BY 1, 2
            ORDER BY 1, 2
            LIMIT {MaxRows + 1};
            """);
        AddWindow(command, filter, start, end);

        var rows = new List<ChannelReconciliationRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ChannelReconciliationRow(
                DateOnly.FromDateTime(reader.GetDateTime(0)), reader.GetString(1),
                reader.GetInt32(2), reader.GetInt32(3), reader.GetDecimal(4)));
        }

        EnsureBounded(rows.Count, "reconciliation");
        return rows;
    }

    private async Task<int> CountRetriesAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT count(*)::int FROM reconciliation.online_order_retry_attempts
            WHERE performed_at >= $1 AND performed_at < $2;
            """);
        command.Parameters.AddWithValue(start.UtcDateTime);
        command.Parameters.AddWithValue(end.UtcDateTime);
        return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>The ledger total, read with one query over the same range, independent of the grouping.</summary>
    private async Task<ChannelReportCheck> CheckAsync(
        ChannelReportFilter filter, DateTimeOffset start, DateTimeOffset end, IReadOnlyList<ChannelDayRow> days, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT count(*)::int, COALESCE(sum(total) FILTER (WHERE status IN ({AcceptedStatuses})), 0),
                   COALESCE(sum(subtotal - discount_total) FILTER (WHERE status IN ({AcceptedStatuses})), 0)
            FROM orders.orders
            WHERE source IN ('Qr', 'Online')
              AND ($3::text IS NULL OR source = $3)
              AND created_at >= $1 AND created_at < $2;
            """);
        command.Parameters.AddWithValue(start.UtcDateTime);
        command.Parameters.AddWithValue(end.UtcDateTime);
        command.Parameters.AddWithValue((object?)filter.Source ?? DBNull.Value).NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new ChannelReportCheck(
            reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2),
            days.Sum(row => row.OrdersReceived), days.Sum(row => row.AcceptedValue), days.Sum(row => row.AcceptedNetValue),
            days.All(row => row.AwaitingConfirmation + row.Accepted + row.Rejected + row.Cancelled == row.OrdersReceived));
    }

    private static void AddWindow(NpgsqlCommand command, ChannelReportFilter filter, DateTimeOffset start, DateTimeOffset end)
    {
        command.Parameters.AddWithValue(start.UtcDateTime);
        command.Parameters.AddWithValue(end.UtcDateTime);
        command.Parameters.AddWithValue(filter.TimeZoneId);
    }

    private static void EnsureBounded(int count, string what)
    {
        if (count > MaxRows)
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"The channel report returned more than {MaxRows} {what} rows; narrow the range."));
    }
}
