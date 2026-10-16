using ALKAROS.Observability.ErrorEvents;
using ALKAROS.Observability.PerformanceMetrics;
using Npgsql;

namespace ALKAROS.Observability.PerformanceBudgets;

public sealed class PerformanceBudgetOptions
{
    public double DefaultRequestP95Milliseconds { get; set; } = 800;
    public int ConsecutiveMinutes { get; set; } = 3;
    public int MinSamplesPerMinute { get; set; } = 3;
    public double RegressionRatio { get; set; } = 1.5;
    public int RegressionWindowMinutes { get; set; } = 15;
    public int MinCurrentSamples { get; set; } = 30;
    public int MinBaselineSamples { get; set; } = 100;
    public int BaselineDays { get; set; } = 7;
    public int ReleaseWindowHours { get; set; } = 24;
}

/// <summary>Remembers when each build was first seen; the first sighting of a version never moves.</summary>
public sealed class ReleaseRegistry(NpgsqlDataSource dataSource, Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public async Task<DateTimeOffset> RegisterAsync(string version, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO observability.perf_releases (version, first_seen_at) VALUES (@version, @now)
            ON CONFLICT (version) DO UPDATE SET version = EXCLUDED.version
            RETURNING first_seen_at;
            """);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("now", _clock());
        return new DateTimeOffset(DateTime.SpecifyKind((DateTime)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!, DateTimeKind.Utc));
    }
}

/// <summary>
/// Judges the stored per-minute summaries: a budget breach that holds for several minutes in a row, and a
/// slowdown against the same time of day on earlier days (a regression event when the build is new).
/// Events resolve themselves once the condition is gone.
/// </summary>
public sealed class PerformanceBudgetEvaluator(
    NpgsqlDataSource dataSource, ErrorEventWriter writer, PerformanceBudgetOptions? options = null,
    Func<DateTimeOffset>? clock = null, DateTimeOffset? releaseFirstSeenAt = null)
{
    public const string BudgetCategory = "perf.budget_exceeded";
    public const string SlowdownCategory = "perf.slowdown";
    public const string RegressionCategory = "perf.regression_after_release";
    private const int MaxRows = 200_000;

    private readonly PerformanceBudgetOptions _options = options ?? new PerformanceBudgetOptions();
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    private sealed record Row(DateTime Minute, string Metric, string Name, long Count, long[] Buckets);

    public async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        var now = _clock();
        var minute = new DateTimeOffset(now.UtcTicks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, TimeSpan.Zero);
        var budgets = await ReadBudgetsAsync(cancellationToken).ConfigureAwait(false);
        await CheckBudgetsAsync(minute, budgets, cancellationToken).ConfigureAwait(false);
        await CheckSlowdownAsync(minute, cancellationToken).ConfigureAwait(false);
    }

    private async Task CheckBudgetsAsync(DateTimeOffset minute, Dictionary<(string, string), double> budgets, CancellationToken cancellationToken)
    {
        var from = minute.AddMinutes(-_options.ConsecutiveMinutes);
        var rows = await ReadRowsAsync([(from, minute)], null, cancellationToken).ConfigureAwait(false);
        var active = new List<ErrorEventInput>();
        foreach (var group in rows.GroupBy(row => (row.Metric, row.Name)))
        {
            var limit = budgets.TryGetValue(group.Key, out var explicitLimit)
                ? explicitLimit
                : group.Key.Metric == PerformanceMetric.Request ? _options.DefaultRequestP95Milliseconds : 0;
            if (limit <= 0)
                continue;

            var perMinute = group.GroupBy(row => row.Minute).ToList();
            if (perMinute.Count < _options.ConsecutiveMinutes
                || !perMinute.All(m => m.Sum(r => r.Count) >= _options.MinSamplesPerMinute
                    && LogHistogram.Percentile(Merge(m), 0.95) > limit))
                continue;

            var layer = group.Key.Metric switch { PerformanceMetric.Request => ErrorLayer.Api, PerformanceMetric.DbCommand => ErrorLayer.Database, _ => ErrorLayer.Background };
            active.Add(new ErrorEventInput(layer, ErrorSeverity.Warning, BudgetCategory, $"{group.Key.Metric} {group.Key.Name}"));
        }

        await Publish(BudgetCategory, active, cancellationToken).ConfigureAwait(false);
    }

    private async Task CheckSlowdownAsync(DateTimeOffset minute, CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromMinutes(_options.RegressionWindowMinutes);
        var current = (minute - window, minute);
        var ranges = new List<(DateTimeOffset, DateTimeOffset)> { current };
        for (var day = 1; day <= _options.BaselineDays; day++)
            ranges.Add((current.Item1.AddDays(-day), current.Item2.AddDays(-day)));

        var rows = await ReadRowsAsync(ranges, PerformanceMetric.Request, cancellationToken).ConfigureAwait(false);
        var newRelease = releaseFirstSeenAt is { } seen && minute - seen <= TimeSpan.FromHours(_options.ReleaseWindowHours);
        var slowdown = new List<ErrorEventInput>();
        var regression = new List<ErrorEventInput>();
        foreach (var group in rows.GroupBy(row => row.Name))
        {
            var now = group.Where(row => row.Minute >= current.Item1.UtcDateTime).ToList();
            var before = group.Where(row => row.Minute < current.Item1.UtcDateTime).ToList();
            if (now.Sum(row => row.Count) < _options.MinCurrentSamples || before.Sum(row => row.Count) < _options.MinBaselineSamples)
                continue;

            var currentP95 = LogHistogram.Percentile(Merge(now), 0.95);
            var baselineP95 = LogHistogram.Percentile(Merge(before), 0.95);
            if (baselineP95 <= 0 || currentP95 < baselineP95 * _options.RegressionRatio)
                continue;

            var detail = $"request {group.Key}";
            if (newRelease)
                regression.Add(new ErrorEventInput(ErrorLayer.Api, ErrorSeverity.Error, RegressionCategory, detail));
            else
                slowdown.Add(new ErrorEventInput(ErrorLayer.Api, ErrorSeverity.Warning, SlowdownCategory, detail));
        }

        await Publish(SlowdownCategory, slowdown, cancellationToken).ConfigureAwait(false);
        await Publish(RegressionCategory, regression, cancellationToken).ConfigureAwait(false);
    }

    private static long[] Merge(IEnumerable<Row> rows)
    {
        var total = new long[LogHistogram.BucketCount];
        foreach (var row in rows)
        {
            for (var i = 0; i < total.Length && i < row.Buckets.Length; i++)
                total[i] += row.Buckets[i];
        }

        return total;
    }

    private async Task Publish(string category, List<ErrorEventInput> active, CancellationToken cancellationToken)
    {
        foreach (var input in active)
            writer.TryEnqueue(input);

        await using var command = dataSource.CreateCommand(
            """
            UPDATE observability.error_events SET status = 'Resolved', resolved_at = now()
            WHERE category = @category AND status <> 'Resolved' AND fingerprint <> ALL(@active);
            """);
        command.Parameters.AddWithValue("category", category);
        command.Parameters.AddWithValue(
            "active", active.Select(input => ErrorFingerprint.Compute(input.Layer, input.Category, input.Detail)).ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<(string, string), double>> ReadBudgetsAsync(CancellationToken cancellationToken)
    {
        var budgets = new Dictionary<(string, string), double>();
        await using var command = dataSource.CreateCommand("SELECT metric, name, p95_ms FROM observability.perf_budgets LIMIT 10001;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            budgets[(reader.GetString(0), reader.GetString(1))] = reader.GetDouble(2);
        return budgets;
    }

    private async Task<List<Row>> ReadRowsAsync(
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> ranges, string? metric, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT s.minute, s.metric, s.name, s.sample_count, s.buckets FROM observability.perf_minute_summaries s
            WHERE (@metric IS NULL OR s.metric = @metric)
              AND EXISTS (SELECT 1 FROM unnest(@starts, @ends) AS r(a, b) WHERE s.minute >= r.a AND s.minute < r.b)
            LIMIT 200001;
            """);
        command.Parameters.Add(new NpgsqlParameter("metric", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)metric ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("starts", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.TimestampTz)
        {
            Value = ranges.Select(range => range.From.UtcDateTime).ToArray(),
        });
        command.Parameters.Add(new NpgsqlParameter("ends", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.TimestampTz)
        {
            Value = ranges.Select(range => range.To.UtcDateTime).ToArray(),
        });
        var rows = new List<Row>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (rows.Count >= MaxRows)
                throw new InvalidOperationException("Too many summary rows in the evaluated ranges.");
            rows.Add(new Row(reader.GetDateTime(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), (long[])reader.GetValue(4)));
        }

        return rows;
    }
}

/// <summary>Runs <see cref="PerformanceBudgetEvaluator"/> once a minute; a failed round is counted and tried again.</summary>
public sealed class PerformanceBudgetRunner(PerformanceBudgetEvaluator evaluator, TimeSpan? interval = null) : IAsyncDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public long Failures { get; private set; }

    public void Start()
    {
        if (_loop is not null)
            return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval ?? TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
                await evaluator.EvaluateAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                Failures++;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is null)
            return;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
            await _loop.ConfigureAwait(false);
        _cts.Dispose();
    }
}
