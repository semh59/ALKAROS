using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;

namespace ALKAROS.Audit.PartitionDisposal;

/// <summary>One yearly partition of <c>audit.audit_events</c>; <paramref name="Expired"/> is true once its retention has ended.</summary>
public sealed record AuditPartitionStatus(string Name, int Year, long Rows, bool Expired);

/// <summary>The yearly partitions found, the ones dropped (empty on a dry run) and the rows held in the default partition.</summary>
public sealed record AuditDisposalResult(
    IReadOnlyList<AuditPartitionStatus> Partitions, IReadOnlyList<AuditPartitionStatus> Dropped, long DefaultPartitionRows);

/// <summary>
/// Drops yearly partitions of <c>audit.audit_events</c> whose retention has ended. The retention counts from the end of the
/// calendar year: year Y may go once 1 January of Y + RetentionYears + 1 (UTC) has arrived. The default partition is never dropped.
/// A partition whose name or bounds are not exactly a calendar year is left alone.
/// </summary>
public sealed partial class AuditPartitionDisposal(NpgsqlDataSource dataSource)
{
    public const int RetentionYears = 10;

    [GeneratedRegex(@"^audit_events_y(\d{4})$")]
    private static partial Regex YearlyName();

    public static bool IsExpired(int year, DateTimeOffset now)
        => now.UtcDateTime >= new DateTime(year + RetentionYears + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<AuditDisposalResult> RunAsync(DateTimeOffset now, bool apply, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var zone = new NpgsqlCommand("SET TIME ZONE 'UTC';", connection))
            await zone.ExecuteNonQueryAsync(cancellationToken);

        var found = new List<(string Name, int Year, string Bound)>();
        await using (var list = new NpgsqlCommand(
            """
            SELECT c.relname, pg_get_expr(c.relpartbound, c.oid)
            FROM pg_inherits i
            JOIN pg_class c ON c.oid = i.inhrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE i.inhparent = 'audit.audit_events'::regclass AND n.nspname = 'audit'
            ORDER BY c.relname;
            """, connection))
        await using (var reader = await list.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var match = YearlyName().Match(reader.GetString(0));
                if (match.Success)
                    found.Add((reader.GetString(0), int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), reader.GetString(1)));
            }
        }

        var partitions = new List<AuditPartitionStatus>();
        foreach (var (name, year, bound) in found)
        {
            if (bound != $"FOR VALUES FROM ('{year}-01-01 00:00:00+00') TO ('{year + 1}-01-01 00:00:00+00')")
                continue;
            partitions.Add(new AuditPartitionStatus(name, year, await CountAsync(connection, name, cancellationToken), IsExpired(year, now)));
        }

        var dropped = new List<AuditPartitionStatus>();
        if (apply)
        {
            foreach (var partition in partitions.Where(p => p.Expired))
            {
                await DropAsync(connection, partition, cancellationToken);
                dropped.Add(partition);
            }
        }

        return new AuditDisposalResult(partitions, dropped, await CountAsync(connection, "audit_events_default", cancellationToken));
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string partition, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM audit.\"{partition}\";", connection);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task DropAsync(NpgsqlConnection connection, AuditPartitionStatus partition, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var rows = await CountAsync(connection, partition.Name, cancellationToken);
        await using (var drop = new NpgsqlCommand($"DROP TABLE audit.\"{partition.Name}\";", connection, transaction))
            await drop.ExecuteNonQueryAsync(cancellationToken);
        await using (var record = new NpgsqlCommand(
            """
            INSERT INTO audit.audit_events (id, event_name, aggregate_type, aggregate_id, actor_type, reason, correlation_id, metadata_json)
            VALUES (gen_random_uuid(), 'audit.partition.disposed', 'audit_partition', gen_random_uuid(), 'system',
                    'retention ended', @correlation, jsonb_build_object('partition', @partition, 'year', @year, 'rows', @rows));
            """, connection, transaction))
        {
            record.Parameters.AddWithValue("correlation", $"audit-disposal-{partition.Name}");
            record.Parameters.AddWithValue("partition", partition.Name);
            record.Parameters.AddWithValue("year", partition.Year);
            record.Parameters.AddWithValue("rows", rows);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
