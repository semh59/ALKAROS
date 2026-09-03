using ALKAROS.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Outbox;

/// <summary>
/// Drains <c>outbox_messages</c> on a poll loop and delivers each message
/// through a freshly scoped <see cref="OutboxFanoutSink"/>. This is the only
/// background worker in the host: it is what turns the transactional outbox
/// rows written by a domain transaction into integration-event side effects,
/// strictly after that transaction committed (commit-before-dispatch,
/// V0-ARC-003 §3). A crash between commit and dispatch leaves the row pending;
/// it is delivered after restart.
/// </summary>
/// <remarks>
/// A message that fails <see cref="RetryPolicy.MaxAttempts"/> times moves to
/// the dead-letter state and is never retried. That is silent in the store, so
/// this worker logs a warning whenever the dead count rises — an operator needs
/// to see a stuck integration event, not discover it from a data drift later.
/// </remarks>
public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);

    private static readonly Action<ILogger, Exception?> LogLoopFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5300, nameof(LogLoopFault)),
            "Outbox dispatch loop iteration failed; retrying after the idle delay.");

    private static readonly Action<ILogger, long, Exception?> LogDeadLetterRise =
        LoggerMessage.Define<long>(
            LogLevel.Warning,
            new EventId(5301, nameof(LogDeadLetterRise)),
            "Outbox has {DeadCount} dead-lettered message(s); an integration event exhausted its retries and needs operator triage.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxStore _outbox;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private long _lastDeadCount;

    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        OutboxStore outbox,
        NpgsqlDataSource dataSource,
        ILogger<OutboxDispatcherHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var attempted = 0;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var sink = scope.ServiceProvider.GetRequiredService<IOutboxDeliverySink>();
                attempted = await _outbox.DispatchAsync(sink, BatchSize, stoppingToken).ConfigureAwait(false);
                if (attempted > 0)
                {
                    // A message can only newly dead-letter on an iteration that
                    // delivered something, so the count query is skipped on an
                    // idle loop.
                    await ReportDeadLettersAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A dispatch-loop fault (transport, transient DB) must not kill
                // the worker; the messages stay pending and are retried.
                LogLoopFault(_logger, ex);
            }

            if (attempted < BatchSize)
            {
                try
                {
                    await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task ReportDeadLettersAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT count(*) FROM outbox_messages WHERE status = 'dead';");
        var dead = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
        if (dead > _lastDeadCount)
            LogDeadLetterRise(_logger, dead, null);
        _lastDeadCount = dead;
    }
}
