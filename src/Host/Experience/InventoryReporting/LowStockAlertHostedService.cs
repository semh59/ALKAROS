using ALKAROS.Reporting.MenuInventory;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.InventoryReporting;

/// <summary>
/// V11-RPT-002: periodically scans <c>CriticalStockReport</c> and broadcasts
/// only the items that newly CROSSED INTO critical since the previous scan
/// — never a flat re-broadcast of every still-critical item, which would
/// flood connected managers with a repeat alert every 5 minutes for a stock
/// problem they already know about (Semih's design intent for the whole
/// feature: a real, actionable notification, not noise). The
/// "already-known-critical" set lives in this singleton instance's own
/// field, the same shape <see cref="ALKAROS.Host.Experience.KitchenOperations.KitchenPrintDispatchHostedService"/>
/// uses for its own poll loop — restarting the Host simply re-alerts once
/// for whatever is critical at that moment, which is acceptable (matches
/// how a human walking in fresh would also notice it).
///
/// Interval is 5 minutes, not <c>KitchenPrintDispatchHostedService</c>'s 5
/// seconds — stock levels do not change anywhere near as fast as kitchen
/// tickets do.
/// </summary>
public sealed class LowStockAlertHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static readonly Action<ILogger, Exception?> LogLoopFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5810, nameof(LogLoopFault)),
            "Low stock alert scan failed; retrying after the interval.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<LowStockAlertHub> _hub;
    private readonly ILogger<LowStockAlertHostedService> _logger;
    private readonly HashSet<(Guid StockItemId, Guid StockLocationId)> _previouslyCritical = new();

    public LowStockAlertHostedService(
        IServiceScopeFactory scopeFactory,
        IHubContext<LowStockAlertHub> hub,
        ILogger<LowStockAlertHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken).ConfigureAwait(false);
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

    /// <summary>Public so a test can invoke one scan deterministically instead of waiting on <see cref="Interval"/>.</summary>
    public async Task ScanAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var reportingService = scope.ServiceProvider.GetRequiredService<IMenuInventoryReportingService>();

        // No CriticalThreshold supplied — only items with a persisted
        // StockItem.ReorderPoint (V11-INV-009) are ever critical here
        // (the fallback defaults to 0, i.e. "never critical" for an
        // unconfigured item); this worker deliberately never invents a
        // threshold on a manager's behalf.
        var report = await reportingService.GetCriticalStockReportAsync(
            new CriticalStockReportQuery(), ct).ConfigureAwait(false);

        var currentlyCritical = report.Items.Where(i => i.IsCritical).ToList();
        var currentKeys = currentlyCritical
            .Select(i => (i.StockItemId, i.StockLocationId))
            .ToHashSet();

        var newlyCritical = currentlyCritical
            .Where(i => !_previouslyCritical.Contains((i.StockItemId, i.StockLocationId)))
            .ToList();

        _previouslyCritical.Clear();
        foreach (var key in currentKeys)
            _previouslyCritical.Add(key);

        foreach (var item in newlyCritical)
        {
            await _hub.Clients.All.SendAsync(
                LowStockAlertHub.StockBecameCritical,
                new LowStockAlertV1(
                    item.StockItemId, item.StockItemName, item.StockLocationId, item.LocationName,
                    item.AvailableQuantity, item.CriticalThreshold),
                ct).ConfigureAwait(false);
        }
    }
}

public sealed record LowStockAlertV1(
    Guid StockItemId, string StockItemName, Guid StockLocationId, string LocationName,
    decimal AvailableQuantity, decimal CriticalThreshold);
