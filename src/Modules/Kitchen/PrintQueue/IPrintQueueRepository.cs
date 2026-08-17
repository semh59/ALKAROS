namespace ALKAROS.Kitchen.PrintQueue;

/// <summary>
/// Repository interface for persistent print jobs (kitchen.print_jobs).
/// </summary>
public interface IPrintQueueRepository
{
    Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PrintJob?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<IReadOnlyList<PrintJob>> GetByTicketIdAsync(Guid ticketId, CancellationToken ct = default);
    Task<PrintJob> EnqueueJobAsync(PrintJob job, CancellationToken ct = default);
    Task<IReadOnlyList<PrintJob>> ClaimEligibleJobsAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        CancellationToken ct = default);
    Task SaveAsync(PrintJob job, CancellationToken ct = default);
    Task<int> RecoverExpiredLeasesAsync(DateTimeOffset now, CancellationToken ct = default);
}
