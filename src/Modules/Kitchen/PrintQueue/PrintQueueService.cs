namespace ALKAROS.Kitchen.PrintQueue;

using ALKAROS.Kitchen.TicketLifecycle;

/// <summary>
/// Service interface for orchestrating persistent kitchen print queue processing.
/// </summary>
public interface IPrintQueueService
{
    Task<PrintJob> EnqueueTicketPrintJobAsync(
        KitchenTicket ticket,
        Guid printerId,
        string payload,
        string? customIdempotencyKey = null,
        int maxAttempts = 5,
        CancellationToken ct = default);

    Task<int> ProcessEligibleJobsAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        Func<PrintJob, Task<bool>> printerExecutor,
        CancellationToken ct = default);

    Task<int> RecoverExpiredLeasesAsync(CancellationToken ct = default);
}

/// <summary>
/// Reference implementation of print queue service coordinating lease locks, backoff retries, and execution.
/// </summary>
public sealed class PrintQueueService : IPrintQueueService
{
    private readonly IPrintQueueRepository _repository;

    public PrintQueueService(IPrintQueueRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PrintJob> EnqueueTicketPrintJobAsync(
        KitchenTicket ticket,
        Guid printerId,
        string payload,
        string? customIdempotencyKey = null,
        int maxAttempts = 5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (printerId == Guid.Empty)
            throw new ArgumentException("PrinterId cannot be empty.", nameof(printerId));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be empty.", nameof(payload));

        var key = customIdempotencyKey ?? $"print:ticket:{ticket.Id}:printer:{printerId}";
        var job = PrintJob.Create(ticket.Id, printerId, payload, key, maxAttempts);

        return await _repository.EnqueueJobAsync(job, ct).ConfigureAwait(false);
    }

    public async Task<int> ProcessEligibleJobsAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        Func<PrintJob, Task<bool>> printerExecutor,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker ID cannot be empty.", nameof(workerId));
        ArgumentNullException.ThrowIfNull(printerExecutor);

        var now = DateTimeOffset.UtcNow;
        var claimedJobs = await _repository.ClaimEligibleJobsAsync(workerId, batchSize, leaseDuration, now, ct).ConfigureAwait(false);
        var processedCount = 0;

        foreach (var job in claimedJobs)
        {
            var inFlight = job.MarkPrinting(workerId, DateTimeOffset.UtcNow);
            await _repository.SaveAsync(inFlight, ct).ConfigureAwait(false);

            bool success;
            string? errorMessage = null;

            try
            {
                success = await printerExecutor(inFlight).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
            }

            var executionTimestamp = DateTimeOffset.UtcNow;
            if (success)
            {
                var succeeded = inFlight.MarkSucceeded(executionTimestamp);
                await _repository.SaveAsync(succeeded, ct).ConfigureAwait(false);
            }
            else
            {
                var backoff = CalculateBackoff(inFlight.AttemptCount);
                var failed = inFlight.RecordFailure(errorMessage ?? "Printer output transmission failed.", backoff, executionTimestamp);
                await _repository.SaveAsync(failed, ct).ConfigureAwait(false);
            }

            processedCount++;
        }

        return processedCount;
    }

    public Task<int> RecoverExpiredLeasesAsync(CancellationToken ct = default)
    {
        return _repository.RecoverExpiredLeasesAsync(DateTimeOffset.UtcNow, ct);
    }

    public static TimeSpan CalculateBackoff(int attemptCount)
    {
        // Exponential backoff: 2s, 4s, 8s, 16s, capped at 300s (5 minutes)
        var seconds = Math.Min(300, Math.Pow(2, Math.Max(0, attemptCount)) * 2);
        return TimeSpan.FromSeconds(seconds);
    }
}
