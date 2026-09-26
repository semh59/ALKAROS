using ALKAROS.OnlineOrdering.Polling;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-ONL-009: every <see cref="PassInterval"/>, polls each platform whose poll is due into the shared inbox; the
/// inbox processor then handles those events like webhook deliveries. Each platform's own interval (its rate
/// limit) decides when it is due, so a short pass interval never polls a platform more often. With no platform
/// offering an order list, every pass does nothing.
/// </summary>
public sealed class OnlineOrderPollingHostedService : BackgroundService
{
    private static readonly TimeSpan PassInterval = TimeSpan.FromSeconds(5);

    private static readonly Action<ILogger, Exception?> LogPassFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5560, nameof(LogPassFault)),
            "Online order polling pass failed; it will be retried on the next pass.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OnlineOrderPollingHostedService> _logger;

    public OnlineOrderPollingHostedService(IServiceScopeFactory scopeFactory, ILogger<OnlineOrderPollingHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static IServiceCollection AddOnlineOrderPollingExperience(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // TryAdd defers to OnlineOrderingModule in the real Host; platform adapters add their polling sources.
        services.TryAddTransient<ALKAROS.Secrets.ISecretProvider, ALKAROS.Secrets.EnvironmentVariableSecretProvider>();
        services.TryAddTransient<ProviderInbox>();
        services.TryAddTransient<OnlineOrderPoller>();
        services.AddHostedService<OnlineOrderPollingHostedService>();
        return services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OnlineOrderPoller>().PollDueAsync(stoppingToken).ConfigureAwait(false);
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
