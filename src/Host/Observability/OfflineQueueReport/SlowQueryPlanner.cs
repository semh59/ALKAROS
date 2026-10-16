using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Observability.OfflineQueueReport;

/// <summary>
/// Adds the query plan to a slow SELECT. The driver reports the statement with named parameters and without their
/// values, so the plan is taken as a generic plan (placeholders stay unbound and nothing is executed). Only a single
/// SELECT is planned, on the monitoring pool, with a short timeout, at most one at a time, and each statement
/// shape at most once per cooldown.
/// </summary>
public sealed partial class SlowQueryPlanner : IDisposable
{
    public const string Category = "db.slow_query_plan";
    private const int MaxTrackedShapes = 500;
    private const int MaxSummaryLength = 200;

    private readonly ErrorEventWriter _writer;
    private readonly NpgsqlDataSource _monitoring;
    private readonly TimeSpan _cooldown;
    private readonly TimeSpan _timeout;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _busy = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastPlanned = new(StringComparer.Ordinal);
    private long _failures;
    private long _planned;

    public SlowQueryPlanner(
        ErrorEventWriter writer, NpgsqlDataSource monitoring, TimeSpan? cooldown = null, TimeSpan? timeout = null,
        Func<DateTimeOffset>? clock = null)
    {
        _writer = writer;
        _monitoring = monitoring;
        _cooldown = cooldown ?? TimeSpan.FromMinutes(10);
        _timeout = timeout ?? TimeSpan.FromSeconds(3);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public long Failures => Interlocked.Read(ref _failures);

    public long Planned => Interlocked.Read(ref _planned);

    /// <summary>The statement with positional placeholders and their count, or null when it is not a single SELECT.</summary>
    public static (string Sql, int Parameters)? ToExplainable(string statement)
    {
        var text = statement.Trim().TrimEnd(';').TrimEnd();
        if (!text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || text.Contains(';', StringComparison.Ordinal))
            return null;

        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var sql = QuotedOrNamed().Replace(text, match =>
        {
            if (!match.Groups["name"].Success)
                return match.Value;
            if (!positions.TryGetValue(match.Groups["name"].Value, out var position))
                positions[match.Groups["name"].Value] = position = positions.Count + 1;
            return "$" + position;
        });
        return (sql, positions.Count);
    }

    /// <summary>Starts planning in the background when allowed; never blocks the caller.</summary>
    public void Request(string shape, string statement, string? traceId)
    {
        var explainable = ToExplainable(statement);
        if (explainable is null)
            return;

        var now = _clock();
        if (_lastPlanned.TryGetValue(shape, out var last) && now - last < _cooldown)
            return;
        if (!_busy.Wait(0))
            return;

        if (_lastPlanned.Count >= MaxTrackedShapes)
            _lastPlanned.Clear();
        _lastPlanned[shape] = now;
        _ = Task.Run(() => PlanAsync(shape, explainable.Value, traceId));
    }

    private async Task PlanAsync(string shape, (string Sql, int Parameters) explainable, string? traceId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(_timeout);
            await using var command = _monitoring.CreateCommand("EXPLAIN (GENERIC_PLAN, FORMAT JSON) " + explainable.Sql);
            for (var index = 0; index < explainable.Parameters; index++)
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Unknown, Value = DBNull.Value });
            command.CommandTimeout = (int)Math.Ceiling(_timeout.TotalSeconds);
            var json = await command.ExecuteScalarAsync(timeout.Token).ConfigureAwait(false) as string;
            var summary = Summarize(json);
            if (summary is null)
            {
                Interlocked.Increment(ref _failures);
                return;
            }

            _writer.TryEnqueue(new ErrorEventInput(
                ErrorLayer.Database, ErrorSeverity.Warning, Category, $"{shape} => {summary}", traceId));
            Interlocked.Increment(ref _planned);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Interlocked.Increment(ref _failures);
        }
        finally
        {
            _busy.Release();
        }
    }

    /// <summary>Node kinds and table names only: no costs, no row counts, no filter text.</summary>
    public static string? Summarize(string? planJson)
    {
        if (string.IsNullOrWhiteSpace(planJson))
            return null;

        using var document = JsonDocument.Parse(planJson);
        var nodes = new List<string>();
        Collect(document.RootElement[0].GetProperty("Plan"), nodes);
        var summary = string.Join(" > ", nodes);
        return summary.Length > MaxSummaryLength ? summary[..MaxSummaryLength] : summary;
    }

    private static void Collect(JsonElement node, List<string> into)
    {
        var kind = node.GetProperty("Node Type").GetString() ?? "?";
        into.Add(node.TryGetProperty("Relation Name", out var relation) ? $"{kind} on {relation.GetString()}" : kind);
        if (node.TryGetProperty("Plans", out var children))
        {
            foreach (var child in children.EnumerateArray())
                Collect(child, into);
        }
    }

    public void Dispose() => _busy.Dispose();

    [GeneratedRegex(@"'[^']*'|@(?<name>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex QuotedOrNamed();
}

public static class SlowQueryPlannerExtensions
{
    public static IServiceCollection AddSlowQueryPlans(this IServiceCollection services) =>
        services.AddSingleton(sp => new SlowQueryPlanner(
            sp.GetRequiredService<ErrorEventWriter>(), sp.GetRequiredService<MonitoringDataSource>().Inner));
}
