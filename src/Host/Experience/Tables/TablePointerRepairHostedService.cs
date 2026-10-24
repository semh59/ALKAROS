using ALKAROS.Tables.CurrentPointers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.Tables;

/// <summary>
/// Repairs a table's order and bill pointers when a missed event left them wrong. Status is left to the
/// staff and the order flow (see <see cref="ITablePointerProjector.RepairPointerDriftAsync"/>).
/// </summary>
public sealed class TablePointerRepairHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private static readonly Action<ILogger, Exception?> LogLoopFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5103, nameof(LogLoopFault)),
            "Table pointer repair failed; retrying after the interval.");

    private static readonly Action<ILogger, int, Exception?> LogRepaired =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(5104, nameof(LogRepaired)),
            "Repaired the order or bill pointers of {Count} table(s).");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TablePointerRepairHostedService> _logger;

    public TablePointerRepairHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<TablePointerRepairHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var projector = scope.ServiceProvider.GetRequiredService<ITablePointerProjector>();
                var repaired = await projector.RepairPointerDriftAsync(stoppingToken).ConfigureAwait(false);
                if (repaired > 0)
                    LogRepaired(_logger, repaired, null);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogLoopFault(_logger, ex);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
