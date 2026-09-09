using ALKAROS.Kitchen.PhysicalPrintRecovery;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.KitchenOperations;

/// <summary>
/// V1-RMD-130: found by an independent audit (2026-09-09) — every piece of
/// the physical print pipeline existed and was individually well-tested
/// (deterministic printer routing, a persistent lease-based print queue with
/// exponential backoff, crash-window-safe delivery tracking with
/// operator-approved reprints, ESC/POS formatting) but nothing ever actually
/// ran any of it: no kitchen ticket ever got a print job, no print job was
/// ever dispatched, and the only thing that ever "printed" was
/// <see cref="KitchenPrinterSimulator"/>, wired into tests only. A ticket
/// just sat there — restaurants had no physical output at all.
///
/// This worker closes that gap end to end, on one poll loop:
/// 1. Bridges kitchen tickets that don't have a print job yet to one per
///    active printer registered at the ticket's station (a ticket already
///    always carries a valid StationId today, whether or not per-item
///    printer routing is enabled upstream — that's a separate, narrower
///    finding this task did not need to touch).
/// 2. Recovers leases abandoned by a crashed worker.
/// 3. Dispatches eligible print jobs over a real network connection
///    (<see cref="IPrinterTransport"/>), recording every attempt's outcome
///    through <see cref="IPhysicalPrintRecoveryService"/> so an ambiguous
///    mid-transmission failure surfaces to an operator instead of silently
///    retrying into a duplicate physical print.
/// </summary>
public sealed class KitchenPrintDispatchHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private const int DispatchBatchSize = 20;
    private const int BridgeBatchSize = 50;

    private static readonly Action<ILogger, Exception?> LogLoopFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5410, nameof(LogLoopFault)),
            "Kitchen print dispatch loop iteration failed; retrying after the interval.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KitchenPrintDispatchHostedService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public KitchenPrintDispatchHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<KitchenPrintDispatchHostedService> logger)
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
                var ticketRepository = scope.ServiceProvider.GetRequiredService<IKitchenTicketRepository>();
                var printerRepository = scope.ServiceProvider.GetRequiredService<IPrinterRepository>();
                var printQueueService = scope.ServiceProvider.GetRequiredService<IPrintQueueService>();
                var recoveryService = scope.ServiceProvider.GetRequiredService<IPhysicalPrintRecoveryService>();
                var transport = scope.ServiceProvider.GetRequiredService<IPrinterTransport>();

                await BridgeUnprintedTicketsAsync(
                    dataSource, ticketRepository, printerRepository, printQueueService, stoppingToken).ConfigureAwait(false);

                await printQueueService.RecoverExpiredLeasesAsync(stoppingToken).ConfigureAwait(false);

                await printQueueService.ProcessEligibleJobsAsync(
                    _workerId,
                    DispatchBatchSize,
                    LeaseDuration,
                    job => ExecuteDeliveryAsync(job, printerRepository, recoveryService, transport, stoppingToken),
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A dispatch-loop fault (transient DB, printer subsystem
                // hiccup) must not kill the worker; jobs simply wait for the
                // next tick.
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

    /// <summary>
    /// Creates one print job per active printer registered at each
    /// not-yet-bridged ticket's station. Public and static so tests can
    /// invoke it deterministically instead of waiting on <see cref="Interval"/>.
    /// Idempotent regardless of how many times or how concurrently it runs:
    /// <see cref="IPrintQueueService.EnqueueTicketPrintJobAsync"/>'s
    /// idempotency key is derived from (ticket, printer), and the repository
    /// enqueues with <c>ON CONFLICT DO NOTHING</c>.
    /// </summary>
    public static async Task<int> BridgeUnprintedTicketsAsync(
        NpgsqlDataSource dataSource,
        IKitchenTicketRepository ticketRepository,
        IPrinterRepository printerRepository,
        IPrintQueueService printQueueService,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(ticketRepository);
        ArgumentNullException.ThrowIfNull(printerRepository);
        ArgumentNullException.ThrowIfNull(printQueueService);

        var candidates = await FindTicketsAwaitingPrintJobsAsync(dataSource, ct).ConfigureAwait(false);
        if (candidates.Count == 0)
            return 0;

        var printers = await printerRepository.GetActiveAsync(ct).ConfigureAwait(false);
        var printersByStation = printers
            .ToLookup(printer => printer.StationId, StringComparer.Ordinal);

        var bridged = 0;
        foreach (var candidate in candidates)
        {
            var ticket = await ticketRepository.GetByIdAsync(candidate.TicketId, ct).ConfigureAwait(false);
            if (ticket is null)
                continue; // Ticket vanished between the scan and here — nothing to bridge.

            var stationPrinters = printersByStation[ticket.StationId];
            var payload = EscPosTicketFormatter.FormatToPrintableText(
                ticket, candidate.TableNumber, candidate.OrderNumber);

            foreach (var printer in stationPrinters)
            {
                await printQueueService.EnqueueTicketPrintJobAsync(
                    ticket, printer.Id, payload, ct: ct).ConfigureAwait(false);
                bridged++;
            }
        }

        return bridged;
    }

    private static async Task<IReadOnlyList<TicketAwaitingPrintJob>> FindTicketsAwaitingPrintJobsAsync(
        NpgsqlDataSource dataSource, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand(
            """
            SELECT t.id, tb.table_number, o.order_number
            FROM kitchen.kitchen_tickets t
            JOIN orders.orders o ON o.order_id = t.order_id
            LEFT JOIN table_mgmt.tables tb ON tb.table_id = o.table_id
            WHERE t.status <> 'Cancelled'
              AND NOT EXISTS (SELECT 1 FROM kitchen.print_jobs pj WHERE pj.ticket_id = t.id)
            ORDER BY t.created_at
            LIMIT @batch_size;
            """);
        cmd.Parameters.Add("batch_size", NpgsqlDbType.Integer).Value = BridgeBatchSize;

        var list = new List<TicketAwaitingPrintJob>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new TicketAwaitingPrintJob(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return list;
    }

    /// <summary>
    /// Executes one delivery attempt and records its outcome through
    /// <see cref="IPhysicalPrintRecoveryService"/>. A pre-flight transport
    /// failure (<see cref="PrinterUnreachableException"/> — the printer was
    /// never reached, nothing physical could have happened) is rethrown
    /// as-is so <see cref="IPrintQueueService.ProcessEligibleJobsAsync"/>'s
    /// normal exponential backoff retries it later; no delivery record is
    /// created for it. A <see cref="PrinterTransmissionUncertainException"/>
    /// is recorded as an Unknown delivery before being rethrown, so
    /// <see cref="PrintQueueService"/> routes the job to
    /// AwaitingOperatorReview instead of auto-retrying it.
    /// </summary>
    private static async Task<bool> ExecuteDeliveryAsync(
        PrintJob job,
        IPrinterRepository printerRepository,
        IPhysicalPrintRecoveryService recoveryService,
        IPrinterTransport transport,
        CancellationToken ct)
    {
        var printer = await printerRepository.GetByIdAsync(job.PrinterId, ct).ConfigureAwait(false);
        if (printer is null)
        {
            // Configuration drift (the printer row was deleted after routing
            // resolved to it) is not a transmission attempt at all — treat it
            // the same as "never reached" so it retries later rather than
            // dead-ending immediately.
            throw new PrinterUnreachableException($"Printer '{job.PrinterId}' is no longer configured.");
        }

        try
        {
            await transport.SendAsync(
                printer.IpAddress ?? string.Empty, printer.Port ?? 0, job.Payload, ct).ConfigureAwait(false);
        }
        catch (PrinterTransmissionUncertainException ex)
        {
            var delivery = await recoveryService.StartInFlightDeliveryAsync(
                job.Id, job.TicketId, job.PrinterId, job.Payload, job.AttemptCount + 1, ct).ConfigureAwait(false);
            await recoveryService.ReportCrashWindowUncertaintyAsync(delivery.Id, ex.Message, ct).ConfigureAwait(false);
            throw;
        }

        return true;
    }

    private readonly record struct TicketAwaitingPrintJob(Guid TicketId, string? TableNumber, string? OrderNumber);
}
