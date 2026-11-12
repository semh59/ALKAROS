using ALKAROS.Reporting.BusinessDayTotals;
using Npgsql;

namespace ALKAROS.Host.Experience.Reporting;

/// <summary>Items the till added to an open check without a kitchen ticket, counted from the audit trail inside a business day's window.</summary>
public sealed class KitchenlessAddedCounter
{
    private readonly NpgsqlDataSource _dataSource;

    public KitchenlessAddedCounter(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<int> CountAsync(DateOnly businessDate, CancellationToken cancellationToken)
    {
        var (start, end) = BusinessDayWindow.Of(businessDate);
        await using var command = _dataSource.CreateCommand(
            "SELECT count(*) FROM audit.audit_events WHERE event_name = 'order.item-added-without-kitchen' AND occurred_at >= $1 AND occurred_at < $2;");
        command.Parameters.AddWithValue(start.UtcDateTime);
        command.Parameters.AddWithValue(end.UtcDateTime);
        return checked((int)(long)(await command.ExecuteScalarAsync(cancellationToken))!);
    }
}
