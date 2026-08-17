namespace ALKAROS.Kitchen.PrintQueue;

using Npgsql;

public sealed class PostgresPrintQueueRepository : IPrintQueueRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPrintQueueRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, ticket_id, printer_id, idempotency_key, payload, status, attempt_count,
                   max_attempts, next_attempt_at, leased_by, lease_expires_at, printed_at,
                   failed_at, last_error, row_version, created_at, updated_at
            FROM kitchen.print_jobs
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapPrintJob(reader);
    }

    public async Task<PrintJob?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, ticket_id, printer_id, idempotency_key, payload, status, attempt_count,
                   max_attempts, next_attempt_at, leased_by, lease_expires_at, printed_at,
                   failed_at, last_error, row_version, created_at, updated_at
            FROM kitchen.print_jobs
            WHERE idempotency_key = @idempotency_key;
            """;
        cmd.Parameters.AddWithValue("idempotency_key", idempotencyKey.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapPrintJob(reader);
    }

    public async Task<IReadOnlyList<PrintJob>> GetByTicketIdAsync(Guid ticketId, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, ticket_id, printer_id, idempotency_key, payload, status, attempt_count,
                   max_attempts, next_attempt_at, leased_by, lease_expires_at, printed_at,
                   failed_at, last_error, row_version, created_at, updated_at
            FROM kitchen.print_jobs
            WHERE ticket_id = @ticket_id
            ORDER BY created_at;
            """;
        cmd.Parameters.AddWithValue("ticket_id", ticketId);

        var list = new List<PrintJob>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapPrintJob(reader));
        }

        return list;
    }

    public async Task<PrintJob> EnqueueJobAsync(PrintJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO kitchen.print_jobs (
                id, ticket_id, printer_id, idempotency_key, payload, status, attempt_count,
                max_attempts, next_attempt_at, leased_by, lease_expires_at, printed_at,
                failed_at, last_error, row_version, created_at, updated_at
            ) VALUES (
                @id, @ticket_id, @printer_id, @idempotency_key, @payload, @status, @attempt_count,
                @max_attempts, @next_attempt_at, @leased_by, @lease_expires_at, @printed_at,
                @failed_at, @last_error, @row_version, @created_at, @updated_at
            )
            ON CONFLICT (idempotency_key) DO NOTHING;
            """;

        cmd.Parameters.AddWithValue("id", job.Id);
        cmd.Parameters.AddWithValue("ticket_id", job.TicketId);
        cmd.Parameters.AddWithValue("printer_id", job.PrinterId);
        cmd.Parameters.AddWithValue("idempotency_key", job.IdempotencyKey);
        cmd.Parameters.AddWithValue("payload", job.Payload);
        cmd.Parameters.AddWithValue("status", job.Status.ToString());
        cmd.Parameters.AddWithValue("attempt_count", job.AttemptCount);
        cmd.Parameters.AddWithValue("max_attempts", job.MaxAttempts);
        cmd.Parameters.AddWithValue("next_attempt_at", (object?)job.NextAttemptAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("leased_by", (object?)job.LeasedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lease_expires_at", (object?)job.LeaseExpiresAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("printed_at", (object?)job.PrintedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("failed_at", (object?)job.FailedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("last_error", (object?)job.LastError ?? DBNull.Value);
        cmd.Parameters.AddWithValue("row_version", job.RowVersion);
        cmd.Parameters.AddWithValue("created_at", job.CreatedAt);
        cmd.Parameters.AddWithValue("updated_at", (object?)job.UpdatedAt ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Fetch the canonical record (either newly inserted or preexisting)
        var existing = await GetByIdempotencyKeyAsync(job.IdempotencyKey, ct).ConfigureAwait(false);
        return existing ?? throw new InvalidOperationException("Failed to retrieve print job after enqueue.");
    }

    public async Task<IReadOnlyList<PrintJob>> ClaimEligibleJobsAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker ID cannot be empty.", nameof(workerId));
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be at least 1.");
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Select candidate IDs with SKIP LOCKED
        await using var selectCmd = connection.CreateCommand();
        selectCmd.Transaction = transaction;
        selectCmd.CommandText =
            """
            SELECT id
            FROM kitchen.print_jobs
            WHERE status = 'Pending'
               OR (status = 'Failed' AND (next_attempt_at IS NULL OR next_attempt_at <= @now))
               OR (status = 'Leased' AND lease_expires_at < @now)
            ORDER BY created_at
            LIMIT @limit
            FOR UPDATE SKIP LOCKED;
            """;
        selectCmd.Parameters.AddWithValue("now", now);
        selectCmd.Parameters.AddWithValue("limit", batchSize);

        var candidateIds = new List<Guid>();
        await using (var reader = await selectCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                candidateIds.Add(reader.GetGuid(0));
            }
        }

        if (candidateIds.Count == 0)
        {
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return [];
        }

        var leaseExpiresAt = now.Add(leaseDuration);

        // Claim selected jobs
        await using var updateCmd = connection.CreateCommand();
        updateCmd.Transaction = transaction;
        updateCmd.CommandText =
            """
            UPDATE kitchen.print_jobs
            SET status = 'Leased',
                leased_by = @worker_id,
                lease_expires_at = @lease_expires_at,
                updated_at = @now,
                row_version = row_version + 1
            WHERE id = ANY(@ids)
            RETURNING id, ticket_id, printer_id, idempotency_key, payload, status, attempt_count,
                      max_attempts, next_attempt_at, leased_by, lease_expires_at, printed_at,
                      failed_at, last_error, row_version, created_at, updated_at;
            """;
        updateCmd.Parameters.AddWithValue("worker_id", workerId);
        updateCmd.Parameters.AddWithValue("lease_expires_at", leaseExpiresAt);
        updateCmd.Parameters.AddWithValue("now", now);
        updateCmd.Parameters.AddWithValue("ids", candidateIds.ToArray());

        var claimedJobs = new List<PrintJob>();
        await using (var reader = await updateCmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                claimedJobs.Add(MapPrintJob(reader));
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return claimedJobs;
    }

    public async Task SaveAsync(PrintJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE kitchen.print_jobs
            SET ticket_id = @ticket_id,
                printer_id = @printer_id,
                payload = @payload,
                status = @status,
                attempt_count = @attempt_count,
                max_attempts = @max_attempts,
                next_attempt_at = @next_attempt_at,
                leased_by = @leased_by,
                lease_expires_at = @lease_expires_at,
                printed_at = @printed_at,
                failed_at = @failed_at,
                last_error = @last_error,
                updated_at = @updated_at,
                row_version = row_version + 1
            WHERE id = @id AND row_version = @row_version;
            """;

        cmd.Parameters.AddWithValue("id", job.Id);
        cmd.Parameters.AddWithValue("ticket_id", job.TicketId);
        cmd.Parameters.AddWithValue("printer_id", job.PrinterId);
        cmd.Parameters.AddWithValue("payload", job.Payload);
        cmd.Parameters.AddWithValue("status", job.Status.ToString());
        cmd.Parameters.AddWithValue("attempt_count", job.AttemptCount);
        cmd.Parameters.AddWithValue("max_attempts", job.MaxAttempts);
        cmd.Parameters.AddWithValue("next_attempt_at", (object?)job.NextAttemptAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("leased_by", (object?)job.LeasedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lease_expires_at", (object?)job.LeaseExpiresAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("printed_at", (object?)job.PrintedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("failed_at", (object?)job.FailedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("last_error", (object?)job.LastError ?? DBNull.Value);
        cmd.Parameters.AddWithValue("updated_at", (object?)job.UpdatedAt ?? DateTimeOffset.UtcNow);
        cmd.Parameters.AddWithValue("row_version", job.RowVersion);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new PrintJobConcurrencyException(
                $"Optimistic concurrency check failed for PrintJob '{job.Id}'. Expected RowVersion={job.RowVersion}.");
        }

        job.RowVersion++;
    }

    public async Task<int> RecoverExpiredLeasesAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE kitchen.print_jobs
            SET status = 'Pending',
                leased_by = NULL,
                lease_expires_at = NULL,
                last_error = 'Lease expired and reset to Pending during recovery.',
                updated_at = @now,
                row_version = row_version + 1
            WHERE status IN ('Leased', 'Printing')
              AND lease_expires_at < @now;
            """;
        cmd.Parameters.AddWithValue("now", now);

        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static PrintJob MapPrintJob(NpgsqlDataReader reader)
    {
        var status = Enum.Parse<PrintJobStatus>(reader.GetString(5));

        return new PrintJob(
            id: reader.GetGuid(0),
            ticketId: reader.GetGuid(1),
            printerId: reader.GetGuid(2),
            idempotencyKey: reader.GetString(3),
            payload: reader.GetString(4),
            status: status,
            attemptCount: reader.GetInt32(6),
            maxAttempts: reader.GetInt32(7),
            nextAttemptAt: reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            leasedBy: reader.IsDBNull(9) ? null : reader.GetString(9),
            leaseExpiresAt: reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
            printedAt: reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            failedAt: reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            lastError: reader.IsDBNull(13) ? null : reader.GetString(13),
            rowVersion: reader.GetInt64(14),
            createdAt: reader.GetFieldValue<DateTimeOffset>(15),
            updatedAt: reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16));
    }
}
