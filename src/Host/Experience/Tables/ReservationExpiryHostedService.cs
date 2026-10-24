using ALKAROS.Tables.Reservations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.Tables;

/// <summary>
/// Expires active reservations whose end time has passed so their tables return to the floor
/// without anyone pressing a button. Reservations with no end time are never touched.
/// </summary>
public sealed class ReservationExpiryHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private static readonly Action<ILogger, Exception?> LogLoopFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5101, nameof(LogLoopFault)),
            "Reservation expiry sweep failed; retrying after the interval.");

    private static readonly Action<ILogger, int, Exception?> LogExpired =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(5102, nameof(LogExpired)),
            "Expired {Count} overdue table reservation(s).");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpiryHostedService> _logger;

    public ReservationExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationExpiryHostedService> logger)
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
                var repository = scope.ServiceProvider.GetRequiredService<ITableReservationRepository>();
                var expired = await repository.ExpireDueAsync(DateTimeOffset.UtcNow, stoppingToken).ConfigureAwait(false);
                if (expired > 0)
                    LogExpired(_logger, expired, null);
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
