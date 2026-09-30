using ALKAROS.Reconciliation.OnlineOrders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.Reconciliation;

/// <summary>
/// Runs the online order reconciliation scan on a schedule, so divergences (dead status updates, failing
/// polling, orders left open) reach the Sorunlar list without anyone calling the scan endpoint. The scan
/// deduplicates open cases itself, so a repeated pass never doubles a case; a failed pass is logged and the
/// next pass tries again.
/// </summary>
public sealed class OnlineOrderReconciliationHostedService : BackgroundService
{
    public static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    private static readonly Action<ILogger, Exception?> LogPassFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5561, nameof(LogPassFault)),
            "Online order reconciliation scan failed; it will be retried on the next pass.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OnlineOrderReconciliationHostedService> _logger;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _interval;

    public OnlineOrderReconciliationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OnlineOrderReconciliationHostedService> logger,
        TimeSpan initialDelay,
        TimeSpan interval)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _initialDelay = initialDelay;
        _interval = interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_initialDelay, stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested)
            {
                await ScanOnceAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ScanOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OnlineOrderReconciliationScanner>()
                .ScanAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPassFault(_logger, ex);
        }
    }
}
