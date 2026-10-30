using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Host.Experience.Orders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Billing;

/// <summary>
/// Closing a bill after a tender is best effort (the money is already recorded), so one failed attempt leaves a
/// fully paid bill open. This retries it: any unfinished bill that has a payment older than the grace period is
/// handed to the same closure service the tender uses; a bill that is not fully covered simply stays as it is.
/// </summary>
public sealed class BillClosureSweeper : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private const int BatchSize = 200;

    private static readonly Action<ILogger, Guid, Exception?> LogSweepCloseFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(5203, nameof(LogSweepCloseFailed)),
            "The closure sweep could not close bill {BillId}; it is retried on the next sweep.");

    private static readonly Action<ILogger, Exception?> LogSweepFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5204, nameof(LogSweepFailed)),
            "The bill closure sweep failed; it is retried on the next sweep.");

    private readonly NpgsqlDataSource _dataSource;
    private readonly IBillClosureService _closure;
    private readonly OrderSettlementService _orderSettlement;
    private readonly ILogger<BillClosureSweeper> _logger;

    public BillClosureSweeper(
        NpgsqlDataSource dataSource,
        IBillClosureService closure,
        OrderSettlementService orderSettlement,
        ILogger<BillClosureSweeper> logger)
    {
        _dataSource = dataSource;
        _closure = closure;
        _orderSettlement = orderSettlement;
        _logger = logger;
    }

    /// <summary>Closes what can be closed and returns how many bills it closed.</summary>
    public async Task<int> SweepOnceAsync(TimeSpan gracePeriod, CancellationToken cancellationToken)
    {
        // Pages through every candidate by id: a partly paid bill that stays open never changes, so any fixed
        // order with a single batch would let a full batch of them starve the bills behind it.
        var closed = 0;
        var after = Guid.Empty;
        while (true)
        {
            var candidates = new List<Guid>();
            await using (var command = _dataSource.CreateCommand(
                $"""
                SELECT b.bill_id
                FROM billing.bills b
                WHERE b.status NOT IN ('Paid', 'Cancelled')
                  AND b.bill_id > @after
                  AND EXISTS (
                      SELECT 1 FROM payments.payment_allocations a
                      WHERE a.bill_id = b.bill_id AND a.allocated_at < now() - @grace)
                ORDER BY b.bill_id
                LIMIT {BatchSize};
                """))
            {
                command.Parameters.AddWithValue("grace", gracePeriod);
                command.Parameters.AddWithValue("after", after);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    candidates.Add(reader.GetGuid(0));
            }

            if (candidates.Count == 0)
                return closed;
            after = candidates[^1];

            foreach (var billId in candidates)
            {
                try
                {
                    var result = await _closure.TryCloseAsync(billId, cancellationToken);
                    if (result.Outcome != BillClosureOutcome.Closed)
                        continue;
                    closed++;
                    await _orderSettlement.CompleteForPaidBillAsync(billId, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogSweepCloseFailed(_logger, billId, exception);
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
                await SweepOnceAsync(Interval, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSweepFailed(_logger, exception);
            }
        }
    }
}
