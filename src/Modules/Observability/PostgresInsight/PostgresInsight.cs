using ALKAROS.Observability.ErrorEvents;
using Npgsql;

namespace ALKAROS.Observability.PostgresInsight;

public sealed record RunningQuery(int Pid, double Seconds, string State, string Query);

public sealed record BlockedQuery(int Pid, int[] BlockedBy, double WaitingSeconds, string Query);

public sealed record SlowStatement(string Query, long Calls, double MeanMilliseconds, double TotalMilliseconds);

public sealed record BloatedTable(string Table, long DeadRows, double DeadRatio);

/// <summary>
/// One read of the database's own statistics. <see cref="StatementStatsAvailable"/> is false when the
/// pg_stat_statements extension is not installed or not readable; everything else works without it.
/// </summary>
public sealed record PostgresInsightSnapshot(
    bool StatementStatsAvailable,
    IReadOnlyList<SlowStatement> SlowStatements,
    IReadOnlyList<RunningQuery> LongRunning,
    IReadOnlyList<BlockedQuery> Blocked,
    int Connections,
    int MaxConnections,
    IReadOnlyList<BloatedTable> Bloated);

public sealed class PostgresInsightOptions
{
    public double LongRunningSeconds { get; init; } = 30;
    public double SlowMeanMilliseconds { get; init; } = 500;
    public double ConnectionWarnRatio { get; init; } = 0.85;
    public double DeadRatioWarn { get; init; } = 0.2;
    public long DeadRowsMin { get; init; } = 1000;
}

/// <summary>Read-only: queries statistics views and turns findings into error events; it never writes tables.</summary>
public sealed class PostgresInsightService(NpgsqlDataSource dataSource, PostgresInsightOptions? options = null)
{
    private readonly PostgresInsightOptions _options = options ?? new PostgresInsightOptions();

    public async Task<PostgresInsightSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var (available, slow) = await ReadStatementsAsync(cancellationToken).ConfigureAwait(false);
        return new PostgresInsightSnapshot(
            available,
            slow,
            await ReadLongRunningAsync(cancellationToken).ConfigureAwait(false),
            await ReadBlockedAsync(cancellationToken).ConfigureAwait(false),
            await ScalarIntAsync("SELECT count(*)::int FROM pg_stat_activity WHERE datname = current_database();", cancellationToken).ConfigureAwait(false),
            await ScalarIntAsync("SELECT current_setting('max_connections')::int;", cancellationToken).ConfigureAwait(false),
            await ReadBloatAsync(cancellationToken).ConfigureAwait(false));
    }

    public IReadOnlyList<ErrorEventInput> ToEvents(PostgresInsightSnapshot snapshot)
    {
        var events = new List<ErrorEventInput>();
        foreach (var blocked in snapshot.Blocked)
        {
            events.Add(new ErrorEventInput(
                ErrorLayer.Database, ErrorSeverity.Error, "db.lock_wait", $"waiting {(int)blocked.WaitingSeconds}s behind {blocked.BlockedBy.Length} session(s)"));
        }

        if (snapshot.LongRunning.Any(q => q.Seconds >= _options.LongRunningSeconds))
        {
            var longest = snapshot.LongRunning.Max(q => q.Seconds);
            events.Add(new ErrorEventInput(ErrorLayer.Database, ErrorSeverity.Warning, "db.long_running_query", $"longest {(int)longest}s"));
        }

        if (snapshot.MaxConnections > 0 && (double)snapshot.Connections / snapshot.MaxConnections >= _options.ConnectionWarnRatio)
        {
            events.Add(new ErrorEventInput(
                ErrorLayer.Database, ErrorSeverity.Warning, "db.connection_saturation", $"{snapshot.Connections}/{snapshot.MaxConnections}"));
        }

        foreach (var statement in snapshot.SlowStatements.Where(s => s.MeanMilliseconds >= _options.SlowMeanMilliseconds))
        {
            events.Add(new ErrorEventInput(
                ErrorLayer.Database, ErrorSeverity.Warning, "db.slow_statement", $"mean {(int)statement.MeanMilliseconds} ms x{statement.Calls}"));
        }

        foreach (var table in snapshot.Bloated)
        {
            events.Add(new ErrorEventInput(
                ErrorLayer.Database, ErrorSeverity.Warning, "db.table_bloat", $"{table.Table} dead {(int)(table.DeadRatio * 100)}%"));
        }

        return events;
    }

    private async Task<(bool Available, IReadOnlyList<SlowStatement> Statements)> ReadStatementsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var command = dataSource.CreateCommand(
                """
                SELECT left(query, 200), calls, mean_exec_time, total_exec_time
                FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;
                """);
            var rows = new List<SlowStatement>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                rows.Add(new SlowStatement(reader.GetString(0), reader.GetInt64(1), reader.GetDouble(2), reader.GetDouble(3)));
            return (true, rows);
        }
        catch (PostgresException exception) when (exception.SqlState is "42P01" or "55000" or "42501")
        {
            return (false, []);
        }
    }

    private async Task<IReadOnlyList<RunningQuery>> ReadLongRunningAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT pid, EXTRACT(EPOCH FROM (now() - query_start))::float8, state, left(query, 200)
            FROM pg_stat_activity
            WHERE datname = current_database() AND pid <> pg_backend_pid()
              AND state = 'active' AND query_start IS NOT NULL
            ORDER BY query_start LIMIT 10;
            """);
        var rows = new List<RunningQuery>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new RunningQuery(reader.GetInt32(0), reader.GetDouble(1), reader.GetString(2), reader.GetString(3)));
        return rows;
    }

    private async Task<IReadOnlyList<BlockedQuery>> ReadBlockedAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT pid, pg_blocking_pids(pid), EXTRACT(EPOCH FROM (now() - query_start))::float8, left(query, 200)
            FROM pg_stat_activity
            WHERE datname = current_database() AND cardinality(pg_blocking_pids(pid)) > 0;
            """);
        var rows = new List<BlockedQuery>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new BlockedQuery(reader.GetInt32(0), reader.GetFieldValue<int[]>(1), reader.GetDouble(2), reader.GetString(3)));
        return rows;
    }

    private async Task<IReadOnlyList<BloatedTable>> ReadBloatAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT schemaname || '.' || relname, n_dead_tup,
                   n_dead_tup::float8 / NULLIF(n_live_tup + n_dead_tup, 0)
            FROM pg_stat_user_tables
            WHERE n_dead_tup >= @min
              AND n_dead_tup::float8 / NULLIF(n_live_tup + n_dead_tup, 0) >= @ratio
            ORDER BY n_dead_tup DESC LIMIT 5;
            """);
        command.Parameters.AddWithValue("min", _options.DeadRowsMin);
        command.Parameters.AddWithValue("ratio", _options.DeadRatioWarn);
        var rows = new List<BloatedTable>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new BloatedTable(reader.GetString(0), reader.GetInt64(1), reader.GetDouble(2)));
        return rows;
    }

    private async Task<int> ScalarIntAsync(string sql, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0);
    }
}
