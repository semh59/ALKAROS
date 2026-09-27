using ALKAROS.OnlineOrdering.StoreStatus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-ONL-011: every <see cref="PassInterval"/>, ends closures whose time has come and tells each platform its pending
/// open/closed request (failed deliveries wait their back-off). With no request pending, a pass does nothing.
/// </summary>
public sealed class OnlineStoreStatusHostedService : BackgroundService
{
    private static readonly TimeSpan PassInterval = TimeSpan.FromSeconds(30);

    private static readonly Action<ILogger, Exception?> LogPassFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5570, nameof(LogPassFault)),
            "Online store status pass failed; it will be retried on the next pass.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OnlineStoreStatusHostedService> _logger;

    public OnlineStoreStatusHostedService(IServiceScopeFactory scopeFactory, ILogger<OnlineStoreStatusHostedService> logger)
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
                await scope.ServiceProvider.GetRequiredService<OnlineStoreStatusService>().DeliverDueAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPassFault(_logger, ex);
            }

            try
            {
                await Task.Delay(PassInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
