using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using ALKAROS.Host.Observability.BrowserErrors;
using ALKAROS.Host.Observability.OfflineQueueReport;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.QrRelay.LocalConnector;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ALKAROS.Host.Observability.ServerDetectorsDeep;

public sealed class ServerDetectorsDeepOptions
{
    public double PoolWarnRatio { get; set; } = 0.85;
    public int NPlusOneThreshold { get; set; } = 20;
    public int NPlusOneSamples { get; set; } = 8;
    public int SlowWarnMilliseconds { get; set; } = 500;
    public int SlowErrorMilliseconds { get; set; } = 2000;
    public double MissedJobFactor { get; set; } = 2.0;
}

/// <summary>Reads the driver's own pool gauges; the monitoring pool is left out so it never reports on itself.</summary>
public sealed class PoolUsageReader : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Dictionary<string, int> _used = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _max = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public PoolUsageReader()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Npgsql" && instrument.Name is "db.client.connections.usage" or "db.client.connections.max")
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<int>(OnMeasurement);
        _listener.Start();
    }

    /// <summary>The highest busy-to-maximum ratio over the application pools, or 0 when none reports.</summary>
    public double WorstRatio()
    {
        lock (_gate)
        {
            _used.Clear();
            _max.Clear();
            _listener.RecordObservableInstruments();
            return _max
                .Where(pool => pool.Value > 0 && !pool.Key.Contains(MonitoringDataSource.ApplicationName, StringComparison.Ordinal))
                .Select(pool => (double)_used.GetValueOrDefault(pool.Key) / pool.Value)
                .DefaultIfEmpty(0)
                .Max();
        }
    }

    private void OnMeasurement(Instrument instrument, int value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        string? pool = null;
        string? poolState = null;
        foreach (var tag in tags)
        {
            if (tag.Key == "pool.name")
                pool = tag.Value as string;
            else if (tag.Key == "state")
                poolState = tag.Value as string;
        }

        if (pool is null)
            return;
        if (instrument.Name == "db.client.connections.max")
            _max[pool] = value;
        else if (poolState == "used")
            _used[pool] = value;
    }

    public void Dispose() => _listener.Dispose();
}

/// <summary>
/// Listens to the driver's command activities: reports slow statements and requests that run too many statements
/// (N+1). Statements of the monitoring system itself (its own schema and the database catalogs) are ignored.
/// </summary>
public sealed partial class DbActivityTap : IDisposable
{
    private const string RequestOperation = "Microsoft.AspNetCore.Hosting.HttpRequestIn";

    private readonly ErrorEventWriter _writer;
    private readonly ServerDetectorsDeepOptions _options;
    private readonly ActivityListener _listener;
    private readonly ConcurrentDictionary<Activity, RequestQueries> _requests = new();
    private readonly SlowQueryPlanner? _planner;

    public DbActivityTap(ErrorEventWriter writer, ServerDetectorsDeepOptions? options = null, SlowQueryPlanner? planner = null)
    {
        _writer = writer;
        _planner = planner;
        _options = options ?? new ServerDetectorsDeepOptions();
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Npgsql" or "Microsoft.AspNetCore",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = OnStopped,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public static bool IsMonitoringStatement(string statement) =>
        statement.Contains("observability.", StringComparison.OrdinalIgnoreCase)
        || statement.Contains("pg_stat_", StringComparison.OrdinalIgnoreCase)
        || statement.Contains("pg_locks", StringComparison.OrdinalIgnoreCase)
        || statement.Contains("pg_class", StringComparison.OrdinalIgnoreCase)
        || statement.Contains("pg_settings", StringComparison.OrdinalIgnoreCase);

    public static string Shape(string statement)
    {
        var shape = Space().Replace(Number().Replace(Quoted().Replace(statement, "'?'"), "?"), " ").Trim();
        return shape.Length > 160 ? shape[..160] : shape;
    }

    private void OnStopped(Activity activity)
    {
        try
        {
            if (activity.Source.Name == "Npgsql")
                OnCommand(activity);
            else if (activity.OperationName == RequestOperation)
                OnRequest(activity);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Failures++;
        }
    }

    public long Failures { get; private set; }

    private void OnCommand(Activity command)
    {
        if (command.GetTagItem("db.statement") is not string statement || IsMonitoringStatement(statement))
            return;

        var shape = Shape(statement);
        var milliseconds = command.Duration.TotalMilliseconds;
        if (milliseconds >= _options.SlowWarnMilliseconds)
        {
            var severity = milliseconds >= _options.SlowErrorMilliseconds ? ErrorSeverity.Error : ErrorSeverity.Warning;
            _writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Database, severity, "db.slow_query", shape, command.TraceId.ToString()));
            _planner?.Request(shape, statement, command.TraceId.ToString());
        }

        var request = command.Parent;
        while (request is not null && request.OperationName != RequestOperation)
            request = request.Parent;
        if (request is not null)
            _requests.GetOrAdd(request, _ => new RequestQueries()).Add(shape, _options.NPlusOneSamples);
    }

    private void OnRequest(Activity request)
    {
        if (!_requests.TryRemove(request, out var queries) || queries.Count <= _options.NPlusOneThreshold)
            return;

        var route = request.GetTagItem("http.route") as string
            ?? BrowserErrorIngest.RouteTemplate(request.GetTagItem("url.path") as string ?? request.DisplayName);
        _writer.TryEnqueue(new ErrorEventInput(
            ErrorLayer.Database, ErrorSeverity.Warning, "db.n_plus_one",
            $"{route} ran {queries.Count} queries; last: {string.Join(" | ", queries.Samples())}", request.TraceId.ToString()));
    }

    public void Dispose() => _listener.Dispose();

    private sealed class RequestQueries
    {
        private readonly object _gate = new();
        private readonly Queue<string> _last = new();

        public int Count { get; private set; }

        public void Add(string shape, int keep)
        {
            lock (_gate)
            {
                Count++;
                _last.Enqueue(shape);
                while (_last.Count > keep)
                    _last.Dequeue();
            }
        }

        public string[] Samples()
        {
            lock (_gate)
                return [.. _last];
        }
    }

    [GeneratedRegex("'[^']*'")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"\b\d+\b")]
    private static partial Regex Number();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Space();
}

/// <summary>Checks pool pressure, missed scheduled jobs and the QR tunnel; events resolve once the state recovers.</summary>
public sealed class ServerDetectorsDeepRunner(
    NpgsqlDataSource monitoringDataSource, ErrorEventWriter writer, PoolUsageReader pool,
    Func<IReadOnlyList<MaintenanceJobStatusV1>>? jobStatuses,
    IRelayConnectorStatusReporter? tunnel, ServerDetectorsDeepOptions? options = null, Func<DateTimeOffset>? clock = null,
    TimeSpan? interval = null) : BackgroundService
{
    private readonly ServerDetectorsDeepOptions _options = options ?? new ServerDetectorsDeepOptions();
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly DateTimeOffset _startedAt = (clock ?? (() => DateTimeOffset.UtcNow))();

    public long Failures { get; private set; }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await CheckAsync(ErrorLayer.Database, "db.pool_saturation", PoolConditions, cancellationToken).ConfigureAwait(false);
        await CheckAsync(ErrorLayer.Background, "background.job_missed", JobConditions, cancellationToken).ConfigureAwait(false);
        await CheckAsync(ErrorLayer.Integration, "integration.qr_tunnel_down", TunnelConditions, cancellationToken).ConfigureAwait(false);
    }

    private IEnumerable<(ErrorSeverity, string)> PoolConditions()
    {
        if (pool.WorstRatio() >= _options.PoolWarnRatio)
            yield return (ErrorSeverity.Warning, "application connection pool nearly full");
    }

    private IEnumerable<(ErrorSeverity, string)> JobConditions()
    {
        if (jobStatuses is null)
            yield break;
        var now = _clock();
        foreach (var job in jobStatuses().Where(job => job.Enabled))
        {
            var allowed = TimeSpan.FromSeconds(job.IntervalSeconds * _options.MissedJobFactor);
            if (now - (job.LastRunAt ?? _startedAt) > allowed)
                yield return (ErrorSeverity.Warning, $"job={job.Name} missed its schedule");
        }
    }

    private IEnumerable<(ErrorSeverity, string)> TunnelConditions()
    {
        if (tunnel is null)
            yield break;
        var state = tunnel.GetCurrentStatusAsync(CancellationToken.None).GetAwaiter().GetResult().State;
        if (state is not (RelayConnectorState.Running or RelayConnectorState.NotConfigured))
            yield return (ErrorSeverity.Error, "qr tunnel process is down");
    }

    private async Task CheckAsync(
        ErrorLayer layer, string category, Func<IEnumerable<(ErrorSeverity, string)>> read, CancellationToken cancellationToken)
    {
        try
        {
            var active = read().ToList();
            foreach (var (severity, detail) in active)
                writer.TryEnqueue(new ErrorEventInput(layer, severity, category, detail));
            await using var command = monitoringDataSource.CreateCommand(
                """
                UPDATE observability.error_events SET status = 'Resolved', resolved_at = now()
                WHERE category = @category AND status <> 'Resolved' AND fingerprint <> ALL(@active);
                """);
            command.Parameters.AddWithValue("category", category);
            command.Parameters.AddWithValue("active", active.Select(a => ErrorFingerprint.Compute(layer, category, a.Item2)).ToArray());
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Failures++;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? TimeSpan.FromSeconds(15);
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(wait, stoppingToken).ConfigureAwait(false);
        }
    }
}

public static class ServerDetectorsDeepExtensions
{
    public static IServiceCollection AddServerDetectorsDeep(this IServiceCollection services, ServerDetectorsDeepOptions? options = null)
    {
        services.AddSingleton<PoolUsageReader>();
        services.AddSingleton(sp => new DbActivityTap(sp.GetRequiredService<ErrorEventWriter>(), options, sp.GetService<SlowQueryPlanner>()));
        services.AddHostedService(sp =>
        {
            _ = sp.GetRequiredService<DbActivityTap>();
            return new ServerDetectorsDeepRunner(
                sp.GetRequiredService<MonitoringDataSource>().Inner, sp.GetRequiredService<ErrorEventWriter>(),
                sp.GetRequiredService<PoolUsageReader>(), sp.GetService<MaintenanceJobRunner>() is { } jobs ? jobs.GetStatuses : null,
                sp.GetService<IRelayConnectorStatusReporter>(), options);
        });
        return services;
    }
}
