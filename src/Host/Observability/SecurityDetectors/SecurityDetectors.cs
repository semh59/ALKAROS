using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Identity.Authorization;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ALKAROS.Host.Observability.SecurityDetectors;

/// <summary>Fires one error event when <c>threshold</c> hits land inside <c>window</c>; the hit list then restarts.</summary>
public sealed class BurstDetector(
    ErrorEventWriter writer,
    ErrorLayer layer,
    ErrorSeverity severity,
    string category,
    int threshold,
    TimeSpan window,
    Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Queue<DateTimeOffset> _hits = new();
    private readonly object _gate = new();

    public bool Record(string? terminalLabel = null)
    {
        int count;
        lock (_gate)
        {
            var now = _clock();
            _hits.Enqueue(now);
            while (_hits.Count > 0 && now - _hits.Peek() > window)
                _hits.Dequeue();
            if (_hits.Count < threshold)
                return false;

            count = _hits.Count;
            _hits.Clear();
        }

        writer.TryEnqueue(new ErrorEventInput(
            layer, severity, category, $"{count} hits in {(int)window.TotalSeconds}s", TerminalLabel: terminalLabel));
        return true;
    }
}

/// <summary>The detectors shared by the login endpoint and the authorization denial hook.</summary>
public sealed class SecurityBurstDetectors(ErrorEventWriter writer)
{
    public BurstDetector LoginFailures { get; } = new(
        writer, ErrorLayer.Security, ErrorSeverity.Error, "security.login_bruteforce", 10, TimeSpan.FromSeconds(60));

    public BurstDetector Denials { get; } = new(
        writer, ErrorLayer.Security, ErrorSeverity.Warning, "security.denial_burst", 20, TimeSpan.FromSeconds(60));

    public static string TerminalLabel(Guid terminalId) => $"terminal:{terminalId.ToString("N")[..8]}";
}

/// <summary>Forwards every denial to the durable sink unchanged and only counts it.</summary>
public sealed class ObservingDenialEventSink(IDenialEventSink inner, SecurityBurstDetectors detectors) : IDenialEventSink
{
    public async Task RecordAsync(DenialEvent denialEvent, CancellationToken cancellationToken = default)
    {
        await inner.RecordAsync(denialEvent, cancellationToken).ConfigureAwait(false);
        detectors.Denials.Record();
    }
}

/// <summary>Reports messages that newly became permanently failed in the outbox.</summary>
public sealed class OutboxDeadLetterWatcher(
    MonitoringDataSource monitoring, ErrorEventWriter writer, TimeSpan? interval = null) : BackgroundService
{
    private long _lastDead = -1;

    public async Task<bool> CheckOnceAsync(CancellationToken cancellationToken)
    {
        await using var command = monitoring.Inner.CreateCommand(
            "SELECT count(*) FROM outbox_messages WHERE status = 'dead';");
        var dead = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
        var rose = _lastDead >= 0 && dead > _lastDead;
        _lastDead = dead;
        if (rose)
            writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Background, ErrorSeverity.Error, "outbox.dead_letter", $"dead={dead}"));
        return rose;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? TimeSpan.FromSeconds(30);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The database being down is reported by the server detectors; the next poll retries and
                // keeps the last known count so deaths during the outage are still noticed.
                writer.TryEnqueue(new ErrorEventInput(
                    ErrorLayer.Background, ErrorSeverity.Warning, "monitoring.outbox_watch_failed", exception.GetType().Name));
            }

            await Task.Delay(wait, stoppingToken);
        }
    }
}

public static class SecurityDetectorsExtensions
{
    public static IServiceCollection AddSecurityDetection(this IServiceCollection services)
    {
        services.AddSingleton<SecurityBurstDetectors>();
        services.AddSingleton<IDenialEventSink>(sp => new ObservingDenialEventSink(
            new PostgresDenialEventSink(sp.GetRequiredService<NpgsqlDataSource>()),
            sp.GetRequiredService<SecurityBurstDetectors>()));
        services.AddHostedService(sp => new OutboxDeadLetterWatcher(
            sp.GetRequiredService<MonitoringDataSource>(), sp.GetRequiredService<ErrorEventWriter>()));
        return services;
    }
}
