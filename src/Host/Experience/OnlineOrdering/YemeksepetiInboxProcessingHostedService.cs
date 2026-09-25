using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-ONL-002: drains stored Yemeksepeti webhook events asynchronously — the webhook itself
/// only stores and acknowledges (V12-ONL-001). With the channel unconfigured the inbox stays
/// empty and every pass finds nothing to do.
/// </summary>
public sealed class YemeksepetiInboxProcessingHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int MaxEventsPerPass = 50;

    private static readonly Action<ILogger, Exception?> LogEventFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5530, nameof(LogEventFault)),
            "Yemeksepeti inbox event processing failed; the attempt was recorded and the event will be retried.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<YemeksepetiInboxProcessingHostedService> _logger;

    public YemeksepetiInboxProcessingHostedService(
        IServiceScopeFactory scopeFactory, ILogger<YemeksepetiInboxProcessingHostedService> logger)
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
                var intake = scope.ServiceProvider.GetRequiredService<YemeksepetiOrderIntakeService>();
                for (var processed = 0; processed < MaxEventsPerPass; processed++)
                {
                    if (!await intake.ProcessNextAsync(stoppingToken).ConfigureAwait(false))
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogEventFault(_logger, ex);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
