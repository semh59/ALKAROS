using ALKAROS.OnlineOrdering.AvailabilityPublishing;
using ALKAROS.OnlineOrdering.AvailabilityPublishing.Yemeksepeti;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-ONL-005: every <see cref="PollInterval"/>, refreshes the availability of the products each
/// enabled online channel knows and sends one batch of changes per channel. At one provider call per
/// channel per pass this stays far below the provider's published rate limit; a channel without
/// credentials is skipped, so nothing runs until an operator configures one.
/// </summary>
public sealed class OnlineAvailabilityPublishingHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private static readonly Action<ILogger, Exception?> LogPassFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5550, nameof(LogPassFault)),
            "Online availability publishing pass failed; the attempt was recorded and will be retried.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OnlineAvailabilityPublishingHostedService> _logger;

    public OnlineAvailabilityPublishingHostedService(
        IServiceScopeFactory scopeFactory, ILogger<OnlineAvailabilityPublishingHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static IServiceCollection AddOnlineAvailabilityPublishingExperience(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // TryAdd defers to OnlineOrderingModule in the real Host; the mapping service, partner
        // client and secret provider come from the other online-ordering experiences.
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAvailabilityChannelPublisher, YemeksepetiAvailabilityPublisher>());
        services.TryAddTransient<AvailabilityPublicationService>();
        services.AddHostedService<OnlineAvailabilityPublishingHostedService>();
        return services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AvailabilityPublicationService>()
                    .RunPassAsync(stoppingToken).ConfigureAwait(false);
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
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
