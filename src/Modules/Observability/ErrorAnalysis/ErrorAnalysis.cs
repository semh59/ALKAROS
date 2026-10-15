using ALKAROS.Observability.ErrorEvents;
using Npgsql;

namespace ALKAROS.Observability.ErrorAnalysis;

/// <summary>Occurrences of one error in the current 5-minute window against the same weekday and hour in past weeks.</summary>
public sealed record ErrorBaseline(int WeeksOfHistory, double MeanPerWindow, double StdDevPerWindow);

public sealed record SeverityDecision(bool Escalate, double ZScore, string Reason);

/// <summary>
/// Z-score escalation with guards against noise: it needs enough weeks of history, a minimum absolute count,
/// and a floor on the deviation so a near-zero series never alarms on a single event.
/// </summary>
public static class SeverityAnalyzer
{
    public const int MinWeeks = 4;
    public const int MinAbsoluteCount = 5;
    public const double ZThreshold = 3.0;
    public const double StdDevFloor = 1.0;

    public static SeverityDecision Evaluate(ErrorBaseline baseline, long currentCount)
    {
        if (baseline.WeeksOfHistory < MinWeeks)
            return new SeverityDecision(false, 0, "not_enough_history");
        if (currentCount < MinAbsoluteCount)
            return new SeverityDecision(false, 0, "below_minimum_count");

        var deviation = Math.Max(baseline.StdDevPerWindow, StdDevFloor);
        var z = (currentCount - baseline.MeanPerWindow) / deviation;
        return z >= ZThreshold
            ? new SeverityDecision(true, z, "above_baseline")
            : new SeverityDecision(false, z, "within_baseline");
    }

    public static ErrorSeverity Escalated(ErrorSeverity current) =>
        current >= ErrorSeverity.Critical ? ErrorSeverity.Critical : current + 1;
}

/// <summary>Reads the same-weekday-and-hour history of one fingerprint from the occurrence table.</summary>
public sealed class ErrorBaselineReader(NpgsqlDataSource dataSource)
{
    public const int WindowMinutes = 5;
    public const int LookbackWeeks = 8;

    public async Task<(ErrorBaseline Baseline, long Current)> ReadAsync(
        string fingerprint, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var windowStart = now.UtcDateTime.AddMinutes(-WindowMinutes);
        var current = await ScalarAsync(
            "SELECT COALESCE(sum(occurrences), 0)::bigint FROM observability.error_occurrences WHERE fingerprint = @f AND occurred_at > @from AND occurred_at <= @to;",
            fingerprint, windowStart, now.UtcDateTime, cancellationToken).ConfigureAwait(false);

        var totals = new List<double>();
        for (var week = 1; week <= LookbackWeeks; week++)
        {
            var hourStart = new DateTime(now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day, now.UtcDateTime.Hour, 0, 0, DateTimeKind.Utc)
                .AddDays(-7 * week);
            var total = await ScalarAsync(
                "SELECT COALESCE(sum(occurrences), 0)::bigint FROM observability.error_occurrences WHERE fingerprint = @f AND occurred_at >= @from AND occurred_at < @to;",
                fingerprint, hourStart, hourStart.AddHours(1), cancellationToken).ConfigureAwait(false);
            totals.Add(total);
        }

        var windowsPerHour = 60.0 / WindowMinutes;
        var perWindow = totals.Select(t => t / windowsPerHour).ToArray();
        var mean = perWindow.Average();
        var variance = perWindow.Sum(v => (v - mean) * (v - mean)) / perWindow.Length;
        var weeksWithData = totals.Count(t => t > 0);
        return (new ErrorBaseline(Math.Max(weeksWithData, await FirstSeenWeeksAsync(fingerprint, now, cancellationToken).ConfigureAwait(false)), mean, Math.Sqrt(variance)), current);
    }

    private async Task<int> FirstSeenWeeksAsync(string fingerprint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT COALESCE(LEAST(@weeks, floor(EXTRACT(EPOCH FROM (@now - first_seen_at)) / 604800)), 0)::int FROM observability.error_events WHERE fingerprint = @f;");
        command.Parameters.AddWithValue("weeks", LookbackWeeks);
        command.Parameters.AddWithValue("now", now.UtcDateTime);
        command.Parameters.AddWithValue("f", fingerprint);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is int weeks ? weeks : 0;
    }

    private async Task<long> ScalarAsync(string sql, string fingerprint, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("f", fingerprint);
        command.Parameters.AddWithValue("from", from);
        command.Parameters.AddWithValue("to", to);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }
}

/// <summary>Each detector reports its last run; a detector that stopped reporting is itself an alarm.</summary>
public sealed class DetectorHeartbeats(Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, (DateTimeOffset LastBeat, TimeSpan Interval)> _beats = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Register(string detector, TimeSpan expectedInterval)
    {
        lock (_gate)
            _beats[detector] = (_clock(), expectedInterval);
    }

    public void Beat(string detector)
    {
        lock (_gate)
        {
            if (_beats.TryGetValue(detector, out var existing))
                _beats[detector] = (_clock(), existing.Interval);
        }
    }

    /// <summary>Detectors silent for more than two expected intervals.</summary>
    public IReadOnlyList<ErrorEventInput> Degraded()
    {
        var now = _clock();
        lock (_gate)
        {
            return _beats
                .Where(b => now - b.Value.LastBeat > b.Value.Interval * 2)
                .Select(b => new ErrorEventInput(
                    ErrorLayer.Background, ErrorSeverity.Error, "monitoring.detector_degraded", $"{b.Key} silent {(int)(now - b.Value.LastBeat).TotalSeconds}s"))
                .ToList();
        }
    }
}
