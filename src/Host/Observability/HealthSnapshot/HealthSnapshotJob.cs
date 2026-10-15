using System.Text.Json;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Operations.BackupHealth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ALKAROS.Host.Observability.HealthSnapshot;

public sealed record DiskUsage(long FreeBytes, long TotalBytes);

/// <summary>Collects database reachability, database size and disk usage for one health snapshot.</summary>
public sealed class HealthProbe(MonitoringDataSource monitoring, Func<DiskUsage> disk)
{
    public const double DegradedFreeRatio = 0.10;
    public const double UnhealthyFreeRatio = 0.05;

    public static DiskUsage DefaultDisk()
    {
        var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory)!);
        return new DiskUsage(drive.AvailableFreeSpace, drive.TotalSize);
    }

    public static SystemHealthStatus ClassifyDisk(DiskUsage usage)
    {
        if (usage.TotalBytes <= 0)
            return SystemHealthStatus.Unhealthy;

        var ratio = (double)usage.FreeBytes / usage.TotalBytes;
        return ratio < UnhealthyFreeRatio ? SystemHealthStatus.Unhealthy
            : ratio < DegradedFreeRatio ? SystemHealthStatus.Degraded
            : SystemHealthStatus.Healthy;
    }

    public async Task<(SystemHealthStatus Database, SystemHealthStatus Disk, DiskUsage Usage, long DatabaseSize)> CollectAsync(
        CancellationToken cancellationToken)
    {
        var usage = disk();
        try
        {
            await using var command = monitoring.Inner.CreateCommand("SELECT pg_database_size(current_database());");
            var size = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
            return (SystemHealthStatus.Healthy, ClassifyDisk(usage), usage, size);
        }
        catch (NpgsqlException)
        {
            return (SystemHealthStatus.Unhealthy, ClassifyDisk(usage), usage, 0);
        }
    }
}

/// <summary>Writes a system health snapshot on a fixed interval so the management screen always shows real values.</summary>
public sealed class HealthSnapshotJob(
    IServiceScopeFactory scopes, HealthProbe probe, TimeSpan? interval = null) : BackgroundService
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(60);

    public async Task CaptureOnceAsync(CancellationToken cancellationToken)
    {
        var (database, diskStatus, usage, size) = await probe.CollectAsync(cancellationToken);
        var details = JsonSerializer.Serialize(new { diskTotalBytes = usage.TotalBytes });
        await using var scope = scopes.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IBackupHealthService>();
        await service.CaptureSystemHealthSnapshotAsync(
            database, diskStatus, usage.FreeBytes, size, details, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? DefaultInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CaptureOnceAsync(stoppingToken);
            }
            catch (NpgsqlException)
            {
                // The next tick retries; a database outage is reported by the server detectors.
                wait = interval ?? DefaultInterval;
            }

            await Task.Delay(wait, stoppingToken);
        }
    }
}

public static class HealthSnapshotExtensions
{
    public static IServiceCollection AddHealthSnapshotJob(this IServiceCollection services)
    {
        services.AddSingleton(sp => new HealthProbe(sp.GetRequiredService<MonitoringDataSource>(), HealthProbe.DefaultDisk));
        services.AddHostedService(sp => new HealthSnapshotJob(
            sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<HealthProbe>()));
        return services;
    }
}
