using ALKAROS.Host.Experience.OnlineOrdering;
using ALKAROS.Reconciliation.OnlineOrders;
using ALKAROS.Reconciliation.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.Reconciliation;

/// <summary>
/// Runs the online order and the payment reconciliation scans on a schedule, so divergences (dead status
/// updates, failing polling, orders left open, payments stuck at Unknown, approved payments without an
/// allocation, drawer differences) reach the Sorunlar list without anyone calling a scan endpoint. A scan
/// deduplicates open cases itself, so a repeated pass never doubles a case; each scan is tried on its own, so
/// one failing scan does not stop the other, and a failed pass is logged and the next pass tries again.
/// </summary>
public sealed class ReconciliationScanHostedService : BackgroundService
{
    public static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    private static readonly Action<ILogger, string, Exception?> LogPassFault =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(5561, nameof(LogPassFault)),
            "The {ScanName} reconciliation scan failed; it will be retried on the next pass.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReconciliationScanHostedService> _logger;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _interval;

    public ReconciliationScanHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReconciliationScanHostedService> logger,
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
        await RunAsync("online order", async scope =>
        {
            // Only hosts that run the online operations experience have the invoice drafting registered.
            if (scope.ServiceProvider.GetService<OnlineOrderInvoiceDrafting>() is { } drafting)
                await drafting.DraftMissingAsync(cancellationToken).ConfigureAwait(false);
            await scope.ServiceProvider.GetRequiredService<OnlineOrderReconciliationScanner>()
                .ScanAllAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
        await RunAsync("payment", scope => scope.ServiceProvider.GetRequiredService<PaymentReconciliationScanner>()
            .ScanAllAsync(cancellationToken), cancellationToken);
    }

    private async Task RunAsync(string scanName, Func<AsyncServiceScope, Task> scan, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scan(scope).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogPassFault(_logger, scanName, ex);
        }
    }
}
