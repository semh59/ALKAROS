using System.Diagnostics;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ALKAROS.Host.Observability.SyntheticProbes;

/// <summary>One read-only check; it must not write, and it does not log in as any user.</summary>
public sealed record SyntheticProbe(string Name, Func<CancellationToken, Task> RunAsync);

public sealed record ProbeResult(string Name, bool Succeeded, TimeSpan Elapsed);

/// <summary>
/// Runs the probes once a minute and turns a failure or a slow answer into an error event. Probes only read, so
/// no synthetic record is ever persisted, and the database probe costs one cheap query per round.
/// </summary>
public sealed class SyntheticProbeRunner(
    IReadOnlyList<SyntheticProbe> probes, ErrorEventWriter writer, TimeSpan? slowThreshold = null)
{
    public static readonly TimeSpan DefaultSlowThreshold = TimeSpan.FromMilliseconds(1000);

    public async Task<IReadOnlyList<ProbeResult>> RunOnceAsync(CancellationToken cancellationToken)
    {
        var slow = slowThreshold ?? DefaultSlowThreshold;
        var results = new List<ProbeResult>();
        foreach (var probe in probes)
        {
            var started = Stopwatch.GetTimestamp();
            var succeeded = true;
            try
            {
                await probe.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                succeeded = false;
            }

            var elapsed = Stopwatch.GetElapsedTime(started);
            results.Add(new ProbeResult(probe.Name, succeeded, elapsed));
            if (!succeeded)
            {
                writer.TryEnqueue(new ErrorEventInput(
                    ErrorLayer.Api, ErrorSeverity.Error, $"canary.{probe.Name}_failed", $"{(int)elapsed.TotalMilliseconds} ms", Synthetic: true));
            }
            else if (elapsed > slow)
            {
                writer.TryEnqueue(new ErrorEventInput(
                    ErrorLayer.Api, ErrorSeverity.Warning, $"canary.{probe.Name}_slow", $"{(int)elapsed.TotalMilliseconds} ms", Synthetic: true));
            }
        }

        return results;
    }
}

public sealed class SyntheticProbeHostedService(SyntheticProbeRunner runner, TimeSpan? interval = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? TimeSpan.FromSeconds(60);
        while (!stoppingToken.IsCancellationRequested)
        {
            await runner.RunOnceAsync(stoppingToken);
            await Task.Delay(wait, stoppingToken);
        }
    }
}

public static class SyntheticProbesExtensions
{
    public const string BaseUrlSetting = "Monitoring:Canary:BaseUrl";

    /// <summary>The database probe is always on; the health-endpoint probe runs only when a base URL is configured.</summary>
    public static IServiceCollection AddSyntheticProbes(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var probes = new List<SyntheticProbe>
            {
                new("database", async token =>
                {
                    await using var command = sp.GetRequiredService<MonitoringDataSource>().Inner.CreateCommand("SELECT 1;");
                    await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                }),
            };
            if (sp.GetRequiredService<IConfiguration>()[BaseUrlSetting] is { Length: > 0 } baseUrl)
            {
                var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
                probes.Add(new SyntheticProbe("health", async token =>
                {
                    using var response = await client.GetAsync("/health/ready", token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                }));
            }

            return new SyntheticProbeRunner(probes, sp.GetRequiredService<ErrorEventWriter>());
        });
        services.AddHostedService(sp => new SyntheticProbeHostedService(sp.GetRequiredService<SyntheticProbeRunner>()));
        return services;
    }
}
