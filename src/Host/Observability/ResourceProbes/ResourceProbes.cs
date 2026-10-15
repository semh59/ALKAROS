using System.Diagnostics;
using ALKAROS.Host.Observability.HealthSnapshot;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ALKAROS.Host.Observability.ResourceProbes;

/// <summary>Counts real-time connections that ended with an error rather than a clean close.</summary>
public sealed class ConnectionDropCounter
{
    private long _drops;

    public long Drops => Interlocked.Read(ref _drops);

    public void RecordDrop() => Interlocked.Increment(ref _drops);
}

/// <summary>Hub filter applied to every hub: it only observes, the disconnect always continues unchanged.</summary>
public sealed class ConnectionDropFilter(ConnectionDropCounter counter) : IHubFilter
{
    public async Task OnDisconnectedAsync(
        HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        if (exception is not null)
            counter.RecordDrop();
        await next(context, exception);
    }
}

/// <summary>
/// Reads resources visible from inside the container. Host machine resources (Docker Desktop or WSL2) are not
/// visible from here, so a high host load can go unnoticed by this probe.
/// </summary>
public sealed class ResourceMonitor(
    ErrorEventWriter writer,
    Func<DiskUsage> disk,
    Func<(long WorkingSetBytes, long AvailableBytes)> memory)
{
    public const double DiskWarnRatio = 0.90;
    public const double DiskCriticalRatio = 0.95;
    public const double MemoryWarnRatio = 0.90;

    public static (long WorkingSetBytes, long AvailableBytes) DefaultMemory() =>
        (Process.GetCurrentProcess().WorkingSet64, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);

    public int CheckOnce()
    {
        var raised = 0;
        var usage = disk();
        if (usage.TotalBytes > 0)
        {
            var used = 1.0 - (double)usage.FreeBytes / usage.TotalBytes;
            if (used >= DiskWarnRatio)
            {
                var severity = used >= DiskCriticalRatio ? ErrorSeverity.Critical : ErrorSeverity.Warning;
                writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Backup, severity, "resource.disk_high", $"used {(int)(used * 100)}%"));
                raised++;
            }
        }

        var (workingSet, available) = memory();
        if (available > 0 && (double)workingSet / available >= MemoryWarnRatio)
        {
            writer.TryEnqueue(new ErrorEventInput(
                ErrorLayer.Api, ErrorSeverity.Warning, "resource.memory_high", $"used {(int)((double)workingSet / available * 100)}%"));
            raised++;
        }

        return raised;
    }
}

public sealed class ResourceMonitorHostedService(ResourceMonitor monitor, TimeSpan? interval = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? TimeSpan.FromSeconds(60);
        while (!stoppingToken.IsCancellationRequested)
        {
            monitor.CheckOnce();
            await Task.Delay(wait, stoppingToken);
        }
    }
}

public static class ResourceProbesExtensions
{
    public static IServiceCollection AddResourceProbes(this IServiceCollection services)
    {
        services.AddSingleton<ConnectionDropCounter>();
        services.AddSignalR(options => options.AddFilter<ConnectionDropFilter>());
        services.AddSingleton(sp => new ResourceMonitor(
            sp.GetRequiredService<ErrorEventWriter>(), HealthProbe.DefaultDisk, ResourceMonitor.DefaultMemory));
        services.AddHostedService(sp => new ResourceMonitorHostedService(sp.GetRequiredService<ResourceMonitor>()));
        return services;
    }
}
