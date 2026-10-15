using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorAnalysis;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ALKAROS.Host.Observability.AnalysisRunner;

/// <summary>
/// Every few minutes: errors seen recently are compared with their same-weekday-and-hour baseline and re-reported
/// one severity level higher when they got abnormally frequent; detectors that stopped reporting become events.
/// </summary>
public sealed class AnalysisRunnerHostedService(
    NpgsqlDataSource dataSource,
    ErrorEventWriter writer,
    DetectorHeartbeats heartbeats,
    TimeSpan? interval = null,
    Func<DateTimeOffset>? clock = null) : BackgroundService
{
    public const string DetectorName = "analysis-runner";
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly ErrorBaselineReader _reader = new(dataSource);

    public long Failures { get; private set; }

    /// <summary>One round; returns how many errors were escalated.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = _clock();
        var escalated = 0;
        foreach (var error in await LoadRecentAsync(now, cancellationToken).ConfigureAwait(false))
        {
            var (baseline, current) = await _reader.ReadAsync(error.Fingerprint, now, cancellationToken).ConfigureAwait(false);
            if (!SeverityAnalyzer.Evaluate(baseline, current).Escalate)
                continue;

            writer.TryEnqueue(new ErrorEventInput(
                error.Layer, SeverityAnalyzer.Escalated(error.Severity), error.Category, error.Detail));
            escalated++;
        }

        heartbeats.Beat(DetectorName);
        foreach (var degraded in heartbeats.Degraded())
            writer.TryEnqueue(degraded);
        return escalated;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? DefaultInterval;
        heartbeats.Register(DetectorName, wait);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Failures++;
            }

            await Task.Delay(wait, stoppingToken);
        }
    }

    private async Task<List<(string Fingerprint, ErrorLayer Layer, ErrorSeverity Severity, string Category, string? Detail)>> LoadRecentAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT fingerprint, layer, severity, category, detail
            FROM observability.error_events
            WHERE status = 'Open' AND severity <> 'Critical' AND last_seen_at > @since
            ORDER BY last_seen_at DESC LIMIT 200;
            """);
        command.Parameters.AddWithValue("since", now.UtcDateTime.AddMinutes(-10));
        var rows = new List<(string, ErrorLayer, ErrorSeverity, string, string?)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add((
                reader.GetString(0),
                Enum.Parse<ErrorLayer>(reader.GetString(1)),
                Enum.Parse<ErrorSeverity>(reader.GetString(2)),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return rows;
    }
}

public static class AnalysisRunnerExtensions
{
    public static IServiceCollection AddAnalysisRunner(this IServiceCollection services)
    {
        services.AddSingleton<DetectorHeartbeats>();
        services.AddHostedService(sp => new AnalysisRunnerHostedService(
            sp.GetRequiredService<MonitoringDataSource>().Inner,
            sp.GetRequiredService<ErrorEventWriter>(),
            sp.GetRequiredService<DetectorHeartbeats>()));
        return services;
    }
}
