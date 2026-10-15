using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.Observability.PostgresInsight;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ALKAROS.Host.Observability.InsightRunner;

/// <summary>Reads the database's own statistics on the monitoring pool and reports findings as error events.</summary>
public sealed class InsightRunnerHostedService(
    PostgresInsightService insight, ErrorEventWriter writer, TimeSpan? interval = null) : BackgroundService
{
    public long Failures { get; private set; }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var snapshot = await insight.ReadAsync(cancellationToken).ConfigureAwait(false);
        var events = insight.ToEvents(snapshot);
        foreach (var input in events)
            writer.TryEnqueue(input);
        return events.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? TimeSpan.FromSeconds(60);
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
}

public static class InsightRunnerExtensions
{
    public static IServiceCollection AddInsightRunner(this IServiceCollection services)
    {
        services.AddSingleton(sp => new PostgresInsightService(sp.GetRequiredService<MonitoringDataSource>().Inner));
        services.AddHostedService(sp => new InsightRunnerHostedService(
            sp.GetRequiredService<PostgresInsightService>(), sp.GetRequiredService<ErrorEventWriter>()));
        return services;
    }
}
