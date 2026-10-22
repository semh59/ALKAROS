using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.WebPush;

/// <summary>
/// Sends push notifications outside the request that caused them. A kitchen hand-over or a guest order used
/// to wait for every device's push service (up to ten seconds each) before its own response was returned.
/// Callers queue the message and carry on; a single background worker delivers them in order.
/// </summary>
public sealed class WebPushDispatchService : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogDeliveryFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(5610, nameof(LogDeliveryFailed)),
            "A queued web push could not be delivered.");

    private readonly Channel<Func<CancellationToken, Task>> _jobs =
        Channel.CreateUnbounded<Func<CancellationToken, Task>>(new UnboundedChannelOptions { SingleReader = true });

    private readonly WebPushSender _sender;
    private readonly ILogger<WebPushDispatchService> _logger;

    public WebPushDispatchService(WebPushSender sender, ILogger<WebPushDispatchService> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Broadcast(WebPushMessage message)
        => Enqueue(cancellationToken => _sender.BroadcastAsync(message, cancellationToken));

    public void ToUserOrBroadcast(WebPushMessage message, Guid userId)
        => Enqueue(cancellationToken => _sender.SendToUserOrBroadcastAsync(message, userId, cancellationToken));

    /// <summary>Any exception a job lets escape is logged here: an unhandled one would stop the whole host.</summary>
    public void Enqueue(Func<CancellationToken, Task> job) => _jobs.Writer.TryWrite(job);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _jobs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await job(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogDeliveryFailed(_logger, ex);
            }
        }
    }
}
