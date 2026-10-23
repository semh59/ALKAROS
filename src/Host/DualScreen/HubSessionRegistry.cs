using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.DualScreen;

/// <summary>
/// SignalR authenticates only when a connection opens, so a session revoked or a user deactivated afterwards would
/// keep receiving pushes until the socket dropped by itself. Hubs register each accepted connection here with the
/// check that proved it; <see cref="HubSessionSweeper"/> repeats that check and aborts the connections that fail it.
/// </summary>
public sealed class HubSessionRegistry
{
    private static readonly Action<ILogger, string, Exception?> CheckFailedLog = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(6960, "HubSessionCheckFailed"),
        "Hub oturum denetimi yapılamadı, bağlantı korundu: {ConnectionId}");

    private readonly ConcurrentDictionary<string, (HubCallerContext Context, Func<CancellationToken, Task<bool>> IsValid)> _connections = new();
    private readonly ILogger<HubSessionRegistry> _logger;

    public HubSessionRegistry(ILogger<HubSessionRegistry> logger)
    {
        _logger = logger;
    }

    public int Count => _connections.Count;

    public void Track(HubCallerContext context, Func<CancellationToken, Task<bool>> isValid)
        => _connections[context.ConnectionId] = (context, isValid);

    public void Release(string connectionId) => _connections.TryRemove(connectionId, out _);

    /// <summary>Aborts every tracked connection whose check now fails; a check that cannot run keeps its connection.</summary>
    public async Task<int> AbortRevokedAsync(CancellationToken cancellationToken)
    {
        var aborted = 0;
        foreach (var (connectionId, entry) in _connections)
        {
            bool valid;
            try
            {
                valid = await entry.IsValid(cancellationToken);
            }
            catch (Exception exception) when (exception is Npgsql.NpgsqlException or TimeoutException or InvalidOperationException)
            {
                CheckFailedLog(_logger, connectionId, exception);
                continue;
            }

            if (valid)
                continue;
            entry.Context.Abort();
            _connections.TryRemove(connectionId, out _);
            aborted++;
        }

        return aborted;
    }
}

public static class HubSessionTracking
{
    /// <summary>Registers the registry and its sweeper; safe to call from every area that maps a hub.</summary>
    public static IServiceCollection AddHubSessionTracking(this IServiceCollection services)
    {
        services.TryAddSingleton<HubSessionRegistry>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HubSessionSweeper>());
        return services;
    }
}

public sealed class HubSessionSweeper : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private static readonly Action<ILogger, Exception?> SweepFailedLog = LoggerMessage.Define(
        LogLevel.Error, new EventId(6961, "HubSessionSweepFailed"),
        "Hub oturum taraması başarısız oldu; sonraki turda yeniden denenecek.");

    private readonly HubSessionRegistry _registry;
    private readonly ILogger<HubSessionSweeper> _logger;

    public HubSessionSweeper(HubSessionRegistry registry, ILogger<HubSessionSweeper> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _registry.AbortRevokedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // One bad tick must not stop the host: BackgroundService ends the process on an unhandled fault.
            SweepFailedLog(_logger, exception);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await SweepOnceAsync(stoppingToken);
    }
}
