using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Observability.PerformanceMetrics;

public static class PerformanceMetric
{
    public const string Request = "request";
    public const string DbCommand = "db_command";
    public const string QueueDepth = "queue_depth";
    public const string UiInteraction = "ui_interaction";
}

/// <summary>
/// Fixed logarithmic buckets (each upper bound is 1.1 times the previous), so a percentile read from summed
/// bucket counts is within about 5% of the true value for any distribution, and histograms of different minutes add up.
/// </summary>
public static class LogHistogram
{
    public const int BucketCount = 116;
    private const double Ratio = 1.1;

    public static int IndexOf(double value)
    {
        if (double.IsNaN(value) || value <= 1)
            return 0;
        return Math.Min(BucketCount - 1, (int)Math.Ceiling(Math.Log(value) / Math.Log(Ratio)));
    }

    public static double UpperBound(int index) => Math.Pow(Ratio, index);

    public static double Percentile(IReadOnlyList<long> buckets, double fraction)
    {
        var total = buckets.Sum();
        if (total == 0)
            return 0;

        var target = fraction * total;
        long before = 0;
        for (var i = 0; i < buckets.Count; i++)
        {
            if (buckets[i] == 0 || before + buckets[i] < target)
            {
                before += buckets[i];
                continue;
            }

            var inside = (target - before) / buckets[i];
            if (i == 0)
                return inside;
            var lower = UpperBound(i - 1);
            return lower * Math.Pow(UpperBound(i) / lower, inside);
        }

        return UpperBound(buckets.Count - 1);
    }
}

public sealed record PerformanceSummary(
    string Name, long Count, long Errors, double ErrorRate, double Mean, double Max, double P50, double P95, double P99);

/// <summary>
/// Collects measurements in memory and writes one summary row per (minute, metric, name) with a histogram.
/// Distinct names per minute are capped so the write volume stays bounded; the rest is counted under "other".
/// </summary>
public sealed class PerformanceRecorder : IAsyncDisposable
{
    public const int MaxRowsPerMinute = 600;
    private const int OtherReserve = 10;
    private const int MaxNameLength = 120;
    private const string OtherName = "other";

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _interval;
    private readonly int _retentionDays;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<CancellationToken, Task<long>> _queueDepth;
    private readonly ConcurrentDictionary<(long Minute, string Metric, string Name), Cell> _cells = new();
    private readonly ConcurrentDictionary<long, int> _rowsPerMinute = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loop;
    private DateTimeOffset _lastPurge = DateTimeOffset.MinValue;

    public PerformanceRecorder(
        NpgsqlDataSource dataSource, TimeSpan? interval = null, int retentionDays = 14, Func<DateTimeOffset>? clock = null,
        Func<CancellationToken, Task<long>>? queueDepth = null)
    {
        _dataSource = dataSource;
        _interval = interval ?? TimeSpan.FromSeconds(60);
        _retentionDays = retentionDays;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _queueDepth = queueDepth ?? ReadOutboxDepthAsync;
    }

    public long FailedFlushes { get; private set; }

    public long SamplerFailures { get; private set; }

    public void Record(string metric, string name, double value, bool failed = false)
    {
        var minute = _clock().UtcTicks / TimeSpan.TicksPerMinute;
        name = name.Length > MaxNameLength ? name[..MaxNameLength] : name;
        var key = (minute, metric, name);
        if (!_cells.ContainsKey(key) && _rowsPerMinute.AddOrUpdate(minute, 1, (_, count) => count + 1) > MaxRowsPerMinute - OtherReserve)
        {
            _rowsPerMinute.AddOrUpdate(minute, 0, (_, count) => count - 1);
            key = (minute, metric, OtherName);
        }

        _cells.GetOrAdd(key, _ => new Cell()).Add(value, failed);
    }

    public void Start()
    {
        if (_loop is not null)
            return;
        _loopCts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_loopCts.Token));
    }

    public async Task FlushAsync(bool includeCurrentMinute = false, CancellationToken cancellationToken = default)
    {
        var current = _clock().UtcTicks / TimeSpan.TicksPerMinute;
        var due = _cells.Keys.Where(key => includeCurrentMinute || key.Minute < current).ToList();
        var taken = new List<((long Minute, string Metric, string Name) Key, Cell Cell)>();
        foreach (var key in due)
        {
            if (_cells.TryRemove(key, out var cell))
                taken.Add((key, cell));
        }

        if (taken.Count == 0)
            return;

        try
        {
            await WriteAsync(taken, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            FailedFlushes++;
            foreach (var (key, cell) in taken)
                _cells.AddOrUpdate(key, cell, (_, existing) => existing.Merge(cell));
        }
        finally
        {
            foreach (var minute in _rowsPerMinute.Keys.Where(minute => minute < current - 5))
                _rowsPerMinute.TryRemove(minute, out _);
        }
    }

    private async Task WriteAsync(
        List<((long Minute, string Metric, string Name) Key, Cell Cell)> rows, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var batch = new NpgsqlBatch(connection, transaction);
        foreach (var (key, cell) in rows)
        {
            var snapshot = cell.Snapshot();
            var command = new NpgsqlBatchCommand(
                """
                INSERT INTO observability.perf_minute_summaries AS s
                    (minute, metric, name, sample_count, error_count, sum_value, max_value, buckets)
                VALUES (@minute, @metric, @name, @count, @errors, @sum, @max, @buckets)
                ON CONFLICT (minute, metric, name) DO UPDATE SET
                    sample_count = s.sample_count + EXCLUDED.sample_count,
                    error_count = s.error_count + EXCLUDED.error_count,
                    sum_value = s.sum_value + EXCLUDED.sum_value,
                    max_value = GREATEST(s.max_value, EXCLUDED.max_value),
                    buckets = ARRAY(SELECT a + b FROM unnest(s.buckets, EXCLUDED.buckets) AS u(a, b));
                """);
            command.Parameters.Add(new NpgsqlParameter("minute", new DateTimeOffset(key.Minute * TimeSpan.TicksPerMinute, TimeSpan.Zero)));
            command.Parameters.Add(new NpgsqlParameter("metric", key.Metric));
            command.Parameters.Add(new NpgsqlParameter("name", key.Name));
            command.Parameters.Add(new NpgsqlParameter("count", snapshot.Count));
            command.Parameters.Add(new NpgsqlParameter("errors", snapshot.Errors));
            command.Parameters.Add(new NpgsqlParameter("sum", snapshot.Sum));
            command.Parameters.Add(new NpgsqlParameter("max", snapshot.Max));
            command.Parameters.Add(new NpgsqlParameter("buckets", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = snapshot.Buckets });
            batch.BatchCommands.Add(command);
        }

        await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ReadOutboxDepthAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("SELECT count(*) FROM outbox_messages WHERE status IN ('pending', 'in_flight');");
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
                await SampleQueueAsync(cancellationToken).ConfigureAwait(false);
                await FlushAsync(false, cancellationToken).ConfigureAwait(false);
                await PurgeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                FailedFlushes++;
            }
        }
    }

    public async Task SampleQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            Record(PerformanceMetric.QueueDepth, "outbox", await _queueDepth(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SamplerFailures++;
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        var now = _clock();
        if (now - _lastPurge < TimeSpan.FromHours(1))
            return;
        _lastPurge = now;
        await using var command = _dataSource.CreateCommand(
            "DELETE FROM observability.perf_minute_summaries WHERE minute < now() - make_interval(days => @days);");
        command.Parameters.AddWithValue("days", _retentionDays);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync().ConfigureAwait(false);
            if (_loop is not null)
                await _loop.ConfigureAwait(false);
            _loopCts.Dispose();
        }

        await FlushAsync(true).ConfigureAwait(false);
    }

    private sealed class Cell
    {
        private readonly object _gate = new();
        private readonly long[] _buckets = new long[LogHistogram.BucketCount];
        private long _count;
        private long _errors;
        private double _sum;
        private double _max;

        public void Add(double value, bool failed)
        {
            lock (_gate)
            {
                _buckets[LogHistogram.IndexOf(value)]++;
                _count++;
                _sum += value;
                _max = Math.Max(_max, value);
                if (failed)
                    _errors++;
            }
        }

        public Cell Merge(Cell other)
        {
            var (count, errors, sum, max, buckets) = other.Snapshot();
            lock (_gate)
            {
                _count += count;
                _errors += errors;
                _sum += sum;
                _max = Math.Max(_max, max);
                for (var i = 0; i < _buckets.Length; i++)
                    _buckets[i] += buckets[i];
            }

            return this;
        }

        public (long Count, long Errors, double Sum, double Max, long[] Buckets) Snapshot()
        {
            lock (_gate)
                return (_count, _errors, _sum, _max, (long[])_buckets.Clone());
        }
    }
}

/// <summary>Reads the stored summaries; percentiles come from adding the buckets of all minutes in the range.</summary>
public sealed class PerformanceReader(NpgsqlDataSource dataSource)
{
    public const int MaxRows = 200_000;

    public async Task<IReadOnlyList<PerformanceSummary>> QueryAsync(
        string metric, DateTimeOffset from, DateTimeOffset to, string? name = null, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT name, sample_count, error_count, sum_value, max_value, buckets FROM observability.perf_minute_summaries
            WHERE metric = @metric AND minute >= @from AND minute < @to AND (@name IS NULL OR name = @name)
            LIMIT 200001;
            """);
        command.Parameters.AddWithValue("metric", metric);
        command.Parameters.AddWithValue("from", from);
        command.Parameters.AddWithValue("to", to);
        command.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Text) { Value = (object?)name ?? DBNull.Value });
        var merged = new Dictionary<string, (long Count, long Errors, double Sum, double Max, long[] Buckets)>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (++rows > MaxRows)
                throw new InvalidOperationException("The requested range holds too many summary rows; narrow the range or the name.");
            var key = reader.GetString(0);
            var buckets = (long[])reader.GetValue(5);
            if (!merged.TryGetValue(key, out var total))
                total = (0, 0, 0, 0, new long[LogHistogram.BucketCount]);
            for (var i = 0; i < buckets.Length && i < total.Buckets.Length; i++)
                total.Buckets[i] += buckets[i];
            merged[key] = (total.Count + reader.GetInt64(1), total.Errors + reader.GetInt64(2), total.Sum + reader.GetDouble(3),
                Math.Max(total.Max, reader.GetDouble(4)), total.Buckets);
        }

        return merged
            .Select(pair => new PerformanceSummary(
                pair.Key, pair.Value.Count, pair.Value.Errors,
                pair.Value.Count == 0 ? 0 : (double)pair.Value.Errors / pair.Value.Count,
                pair.Value.Count == 0 ? 0 : pair.Value.Sum / pair.Value.Count, pair.Value.Max,
                LogHistogram.Percentile(pair.Value.Buckets, 0.50), LogHistogram.Percentile(pair.Value.Buckets, 0.95),
                LogHistogram.Percentile(pair.Value.Buckets, 0.99)))
            .OrderBy(summary => summary.Name, StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>Turns finished request and database command activities into measurements; the monitoring system's own statements are skipped.</summary>
public sealed partial class ActivityPerformanceFeeder : IDisposable
{
    private readonly PerformanceRecorder _recorder;
    private readonly ActivityListener _listener;

    public ActivityPerformanceFeeder(PerformanceRecorder recorder)
    {
        _recorder = recorder;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Npgsql" or "Microsoft.AspNetCore",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = OnStopped,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public long Failures { get; private set; }

    private void OnStopped(Activity activity)
    {
        try
        {
            var milliseconds = activity.Duration.TotalMilliseconds;
            if (activity.Source.Name == "Npgsql")
            {
                if (activity.GetTagItem("db.statement") is not string statement || IsMonitoringStatement(statement))
                    return;
                _recorder.Record(PerformanceMetric.DbCommand, Shape(statement), milliseconds, activity.Status == ActivityStatusCode.Error);
            }
            else if (activity.OperationName == "Microsoft.AspNetCore.Hosting.HttpRequestIn")
            {
                var method = activity.GetTagItem("http.request.method") as string ?? activity.GetTagItem("http.method") as string ?? "?";
                var route = activity.GetTagItem("http.route") as string ?? "unmatched";
                var status = activity.GetTagItem("http.response.status_code") is int code ? code : 0;
                _recorder.Record(PerformanceMetric.Request, $"{method} {route}", milliseconds, status >= 500);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Failures++;
        }
    }

    public static bool IsMonitoringStatement(string statement) =>
        statement.Contains("observability.", StringComparison.OrdinalIgnoreCase)
        || statement.Contains("pg_stat_", StringComparison.OrdinalIgnoreCase)
        || statement.Contains("pg_locks", StringComparison.OrdinalIgnoreCase);

    public static string Shape(string statement) =>
        Space().Replace(Number().Replace(Quoted().Replace(statement, "'?'"), "?"), " ").Trim();

    public void Dispose() => _listener.Dispose();

    [GeneratedRegex("'[^']*'")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"\b\d+\b")]
    private static partial Regex Number();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Space();
}
