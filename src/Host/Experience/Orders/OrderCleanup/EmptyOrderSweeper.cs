using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.Orders.OrderCleanup;

/// <summary>
/// An order opened without any product (a table opened and left) keeps its table occupied for ever. This cancels
/// such orders once they have been untouched for <see cref="IdleTimeout"/>; an order that got a product, a bill or
/// any other change in the meantime is left alone (the store checks again under the order lock).
/// </summary>
public sealed class EmptyOrderSweeper(IServiceProvider services, ILogger<EmptyOrderSweeper> logger) : BackgroundService
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(2);
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private const int BatchSize = 100;

    private static readonly Action<ILogger, Guid, Exception?> LogOrderFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning, new EventId(5211, nameof(LogOrderFailed)),
            "The empty order {OrderId} could not be cancelled; it is retried on the next sweep.");

    private static readonly Action<ILogger, Exception?> LogSweepFailed =
        LoggerMessage.Define(
            LogLevel.Error, new EventId(5212, nameof(LogSweepFailed)),
            "The empty order sweep failed; it is retried on the next sweep.");

    /// <returns>How many empty orders were cancelled.</returns>
    public async Task<int> SweepOnceAsync(TimeSpan idleFor, CancellationToken cancellationToken)
    {
        // Resolved per sweep so a host without the stock service still starts; the sweep then fails and is logged.
        var store = services.GetRequiredService<OrderCleanupStore>();
        var cancelled = 0;
        // A failing order stays a candidate, so ids already tried are skipped and the loop ends when only they are left.
        var tried = new HashSet<Guid>();
        while (true)
        {
            var batch = (await store.FindEmptyDraftsAsync(idleFor, BatchSize + tried.Count, cancellationToken)).Where(tried.Add).ToList();
            if (batch.Count == 0)
                return cancelled;
            foreach (var orderId in batch)
            {
                try
                {
                    if (await store.CancelEmptyDraftAsync(orderId, idleFor, cancellationToken))
                        cancelled++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogOrderFailed(logger, orderId, exception);
                }
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepOnceAsync(IdleTimeout, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSweepFailed(logger, exception);
            }
        }
    }
}
