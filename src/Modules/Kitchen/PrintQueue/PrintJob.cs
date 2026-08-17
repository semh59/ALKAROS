namespace ALKAROS.Kitchen.PrintQueue;

/// <summary>
/// Aggregate root representing a persistent print queue entry (kitchen.print_jobs).
/// Coordinates lease fences, exponential backoff retries, and logical deduplication.
/// </summary>
public sealed class PrintJob
{
    public PrintJob(
        Guid id,
        Guid ticketId,
        Guid printerId,
        string idempotencyKey,
        string payload,
        PrintJobStatus status = PrintJobStatus.Pending,
        int attemptCount = 0,
        int maxAttempts = 5,
        DateTimeOffset? nextAttemptAt = null,
        string? leasedBy = null,
        DateTimeOffset? leaseExpiresAt = null,
        DateTimeOffset? printedAt = null,
        DateTimeOffset? failedAt = null,
        string? lastError = null,
        long rowVersion = 1,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Job ID cannot be empty.", nameof(id));
        if (ticketId == Guid.Empty)
            throw new ArgumentException("Ticket ID cannot be empty.", nameof(ticketId));
        if (printerId == Guid.Empty)
            throw new ArgumentException("Printer ID cannot be empty.", nameof(printerId));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be empty.", nameof(payload));
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "MaxAttempts must be at least 1.");

        Id = id;
        TicketId = ticketId;
        PrinterId = printerId;
        IdempotencyKey = idempotencyKey.Trim();
        Payload = payload;
        Status = status;
        AttemptCount = attemptCount;
        MaxAttempts = maxAttempts;
        NextAttemptAt = nextAttemptAt;
        LeasedBy = leasedBy;
        LeaseExpiresAt = leaseExpiresAt;
        PrintedAt = printedAt;
        FailedAt = failedAt;
        LastError = lastError;
        RowVersion = rowVersion;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public Guid TicketId { get; }
    public Guid PrinterId { get; }
    public string IdempotencyKey { get; }
    public string Payload { get; }
    public PrintJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LeasedBy { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset? PrintedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? LastError { get; private set; }
    public long RowVersion { get; internal set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public static PrintJob Create(
        Guid ticketId,
        Guid printerId,
        string payload,
        string? idempotencyKey = null,
        int maxAttempts = 5,
        DateTimeOffset? timestamp = null)
    {
        var at = timestamp ?? DateTimeOffset.UtcNow;
        var key = idempotencyKey ?? $"print:ticket:{ticketId}:printer:{printerId}";

        return new PrintJob(
            id: Guid.NewGuid(),
            ticketId: ticketId,
            printerId: printerId,
            idempotencyKey: key,
            payload: payload,
            status: PrintJobStatus.Pending,
            attemptCount: 0,
            maxAttempts: maxAttempts,
            nextAttemptAt: null,
            leasedBy: null,
            leaseExpiresAt: null,
            printedAt: null,
            failedAt: null,
            lastError: null,
            rowVersion: 1,
            createdAt: at,
            updatedAt: null);
    }

    public bool IsEligibleForClaim(DateTimeOffset now)
    {
        return Status switch
        {
            PrintJobStatus.Pending => true,
            PrintJobStatus.Failed => !NextAttemptAt.HasValue || NextAttemptAt.Value <= now,
            PrintJobStatus.Leased => LeaseExpiresAt.HasValue && LeaseExpiresAt.Value < now, // expired lease
            _ => false
        };
    }

    public PrintJob ClaimLease(string workerId, TimeSpan leaseDuration, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker ID cannot be empty.", nameof(workerId));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");

        if (!IsEligibleForClaim(now))
        {
            throw new PrintJobLeaseException(
                $"PrintJob '{Id}' is not eligible for claim. Status={Status}, NextAttemptAt={NextAttemptAt}, LeaseExpiresAt={LeaseExpiresAt}");
        }

        return new PrintJob(
            Id,
            TicketId,
            PrinterId,
            IdempotencyKey,
            Payload,
            status: PrintJobStatus.Leased,
            attemptCount: AttemptCount,
            maxAttempts: MaxAttempts,
            nextAttemptAt: null,
            leasedBy: workerId,
            leaseExpiresAt: now.Add(leaseDuration),
            printedAt: PrintedAt,
            failedAt: FailedAt,
            lastError: LastError,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: now);
    }

    public PrintJob MarkPrinting(string workerId, DateTimeOffset now)
    {
        if (Status != PrintJobStatus.Leased)
            throw new InvalidPrintJobTransitionException($"Cannot transition to Printing from {Status}. Must be Leased.");
        if (LeasedBy != workerId)
            throw new PrintJobLeaseException($"Worker '{workerId}' does not hold active lease on PrintJob '{Id}' (held by '{LeasedBy}').");
        if (LeaseExpiresAt.HasValue && LeaseExpiresAt.Value < now)
            throw new PrintJobLeaseException($"Lease for PrintJob '{Id}' expired at {LeaseExpiresAt}.");

        return new PrintJob(
            Id,
            TicketId,
            PrinterId,
            IdempotencyKey,
            Payload,
            status: PrintJobStatus.Printing,
            attemptCount: AttemptCount,
            maxAttempts: MaxAttempts,
            nextAttemptAt: null,
            leasedBy: workerId,
            leaseExpiresAt: LeaseExpiresAt,
            printedAt: PrintedAt,
            failedAt: FailedAt,
            lastError: LastError,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: now);
    }

    public PrintJob MarkSucceeded(DateTimeOffset now)
    {
        if (Status != PrintJobStatus.Printing && Status != PrintJobStatus.Leased)
        {
            throw new InvalidPrintJobTransitionException($"Cannot mark Succeeded from {Status}.");
        }

        return new PrintJob(
            Id,
            TicketId,
            PrinterId,
            IdempotencyKey,
            Payload,
            status: PrintJobStatus.Printed,
            attemptCount: AttemptCount,
            maxAttempts: MaxAttempts,
            nextAttemptAt: null,
            leasedBy: null,
            leaseExpiresAt: null,
            printedAt: now,
            failedAt: null,
            lastError: null,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: now);
    }

    public PrintJob RecordFailure(string error, TimeSpan backoff, DateTimeOffset now)
    {
        var newAttemptCount = AttemptCount + 1;
        var isDeadLetter = newAttemptCount >= MaxAttempts;
        var newStatus = isDeadLetter ? PrintJobStatus.DeadLetter : PrintJobStatus.Failed;
        var nextAttempt = isDeadLetter ? null : (DateTimeOffset?)now.Add(backoff);

        return new PrintJob(
            Id,
            TicketId,
            PrinterId,
            IdempotencyKey,
            Payload,
            status: newStatus,
            attemptCount: newAttemptCount,
            maxAttempts: MaxAttempts,
            nextAttemptAt: nextAttempt,
            leasedBy: null,
            leaseExpiresAt: null,
            printedAt: null,
            failedAt: now,
            lastError: error,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: now);
    }

    public PrintJob Cancel(string reason, DateTimeOffset now)
    {
        if (Status == PrintJobStatus.Printed)
        {
            throw new InvalidPrintJobTransitionException("Cannot cancel an already printed job.");
        }

        return new PrintJob(
            Id,
            TicketId,
            PrinterId,
            IdempotencyKey,
            Payload,
            status: PrintJobStatus.Cancelled,
            attemptCount: AttemptCount,
            maxAttempts: MaxAttempts,
            nextAttemptAt: null,
            leasedBy: null,
            leaseExpiresAt: null,
            printedAt: null,
            failedAt: FailedAt,
            lastError: reason,
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: now);
    }

    public PrintJob ResetExpiredLease(DateTimeOffset now)
    {
        if (Status != PrintJobStatus.Leased && Status != PrintJobStatus.Printing)
            return this;

        if (LeaseExpiresAt.HasValue && LeaseExpiresAt.Value >= now)
            return this; // Lease is still active

        return new PrintJob(
            Id,
            TicketId,
            PrinterId,
            IdempotencyKey,
            Payload,
            status: PrintJobStatus.Pending,
            attemptCount: AttemptCount,
            maxAttempts: MaxAttempts,
            nextAttemptAt: null,
            leasedBy: null,
            leaseExpiresAt: null,
            printedAt: PrintedAt,
            failedAt: FailedAt,
            lastError: "Lease expired and reset to Pending.",
            rowVersion: RowVersion,
            createdAt: CreatedAt,
            updatedAt: now);
    }
}
