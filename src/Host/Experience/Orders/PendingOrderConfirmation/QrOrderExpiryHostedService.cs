using ALKAROS.Orders.ItemExceptions;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Settings.QrOrderExpiry;
using ALKAROS.Settings.TypedSettings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;

/// <summary>
/// V12-QRO-002: docs/design/modules/qr-nfc-ordering.md §5 — an unconfirmed
/// QR order must not hold a table Reserved forever (an old or duplicate QR
/// scan, or a customer who never got staff attention). This is the
/// background half of the "no remote QR service denial" goal:
/// <see cref="ALKAROS.QrOrdering.TablePolicy.QrTableReservationPolicy"/> is
/// the submission-time gate, this is the standing-order cleanup. Every
/// PendingConfirmation order sourced from QR that has sat past the
/// configured timeout (<see cref="QrOrderExpirySetting"/>, default 5
/// minutes) is rejected through the exact same
/// <see cref="PendingOrderConfirmationStore.RejectAsync"/> a staff member's
/// own reject action uses (V1-RMD-137) — cancels any already-dispatched
/// kitchen ticket items and releases the table Reserved -&gt; Available, one
/// atomic table-reservation-policy.md-compliant path, not a second one
/// invented for the timer case.
/// </summary>
public sealed class QrOrderExpiryHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 50;
    private const string ExpiryReason = "Sipariş zaman aşımına uğradı, personel onayı gelmedi.";

    // A stable, non-empty actor id standing in for "the system" in the
    // audit trail — never a real user, and intentionally distinct from
    // Guid.Empty (several call paths already treat that as "no actor").
    internal static readonly Guid SystemActorId = new("00000000-0000-0000-0000-0000000005e5");

    private static readonly Action<ILogger, Exception?> LogLoopFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5520, nameof(LogLoopFault)),
            "QR order expiry loop iteration failed; retrying after the interval.");

    private static readonly Action<ILogger, Guid, Exception?> LogExpired =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(5521, nameof(LogExpired)),
            "QR order '{OrderId}' auto-rejected after exceeding its confirmation timeout.");

    private static readonly Action<ILogger, Guid, Exception?> LogOrderFault =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(5522, nameof(LogOrderFault)),
            "QR order '{OrderId}' could not be auto-rejected; the rest of the pass continues and it is retried next pass.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QrOrderExpiryHostedService> _logger;

    public QrOrderExpiryHostedService(IServiceScopeFactory scopeFactory, ILogger<QrOrderExpiryHostedService> logger)
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
                var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var store = scope.ServiceProvider.GetRequiredService<PendingOrderConfirmationStore>();

                var timeout = await QrOrderExpirySetting.GetTimeoutAsync(settings, stoppingToken).ConfigureAwait(false);
                await ExpireOverdueOrdersAsync(dataSource, store, timeout, _logger, stoppingToken).ConfigureAwait(false);
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
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Public and static so tests can invoke one pass deterministically
    /// instead of waiting on <see cref="PollInterval"/>.
    /// </summary>
    public static async Task<int> ExpireOverdueOrdersAsync(
        NpgsqlDataSource dataSource,
        PendingOrderConfirmationStore store,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var candidates = await FindOverdueQrOrdersAsync(dataSource, timeout, cancellationToken).ConfigureAwait(false);
        var expiredCount = 0;

        foreach (var (orderId, rowVersion) in candidates)
        {
            try
            {
                await store.RejectAsync(orderId, rowVersion, SystemActorId, ExpiryReason, cancellationToken)
                    .ConfigureAwait(false);
                LogExpired(logger, orderId, null);
                expiredCount++;
            }
            catch (Exception ex) when (
                ex is OrderNotAwaitingConfirmationException
                    or StaleOrderRowVersionException
                    or OrderAlreadyBilledException)
            {
                // Staff already acted on it (or another tick already
                // expired it) since the scan above — the order moved on,
                // nothing left for this pass to do. Not an error.
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // One order that keeps failing must not stop every order behind it from expiring.
                LogOrderFault(logger, orderId, ex);
            }
        }

        return expiredCount;
    }

    private static async Task<IReadOnlyList<(Guid OrderId, long RowVersion)>> FindOverdueQrOrdersAsync(
        NpgsqlDataSource dataSource, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT o.order_id, o.row_version
            FROM orders.orders o
            WHERE o.status = 'PendingConfirmation' AND o.updated_at < now() - @timeout
              AND (o.source = 'Qr'
                   -- An age-restricted NFC cart is held for a waiter although its ticket was already sent; it expires
                   -- like a QR order only while the kitchen has not started any of it.
                   OR (o.source = 'Nfc' AND NOT EXISTS (
                       SELECT 1
                       FROM kitchen.kitchen_tickets t
                       JOIN kitchen.kitchen_ticket_items ti ON ti.ticket_id = t.id
                       WHERE t.order_id = o.order_id AND ti.status NOT IN ('Queued', 'Cancelled'))))
            ORDER BY o.updated_at
            LIMIT @batch_size;
            """);
        command.Parameters.Add("timeout", NpgsqlDbType.Interval).Value = timeout;
        command.Parameters.Add("batch_size", NpgsqlDbType.Integer).Value = BatchSize;

        var result = new List<(Guid, long)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add((reader.GetGuid(0), reader.GetInt64(1)));

        return result;
    }
}
