using Npgsql;

namespace ALKAROS.Idempotency;

/// <summary>
/// Persists idempotency records in the <c>idempotency_keys</c> table and
/// enforces the V0-ARC-003 §1 semantics atomically: first use registers the
/// operation, an identical replay returns the cached response envelope, a
/// conflicting replay fails with <see cref="IdempotencyKeyConflictException"/>,
/// and an expired record is atomically replaced by a new registration
/// instead of being replayed. A replay never extends the retention window
/// and never overwrites the stored envelope.
/// </summary>
public sealed class IdempotencyKeyStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _retention;

    public IdempotencyKeyStore(NpgsqlDataSource dataSource, TimeSpan? retention = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _retention = retention ?? TimeSpan.FromHours(24);
        if (_retention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be positive.");
    }

    /// <summary>
    /// Registers the operation under <paramref name="key"/> or replays the
    /// cached response. <paramref name="responseEnvelope"/> is only written
    /// on first registration; a replay never overwrites the stored envelope.
    /// An expired record is treated as absent: the operation is registered
    /// again atomically, so a stale key can never be replayed or conflict.
    /// </summary>
    /// <exception cref="IdempotencyKeyConflictException">
    /// An active key exists with a different request hash (IDEMPOTENCY_KEY_CONFLICT).
    /// </exception>
    public async Task<IdempotencyOutcome> RegisterOrReplayAsync(
        IdempotencyKey key,
        ReadOnlyMemory<byte> requestBody,
        byte[] responseEnvelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(responseEnvelope);

        var requestHash = RequestHash.Compute(requestBody);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Fast path: a record with no prior entry registers immediately.
        // Under concurrent first use the loser of the INSERT race falls
        // through to the locked re-evaluation below, never to a duplicate.
        byte[]? insertedEnvelope = null;
        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO idempotency_keys (client_id, operation_id, request_hash, response_envelope, expires_at)
                VALUES ($1, $2, $3, $4, now() + $5 * interval '1 second')
                ON CONFLICT (client_id, operation_id) DO NOTHING
                RETURNING response_envelope;
                """;
            insertCommand.Parameters.AddWithValue(key.ClientId);
            insertCommand.Parameters.AddWithValue(key.OperationId);
            insertCommand.Parameters.AddWithValue(requestHash);
            insertCommand.Parameters.AddWithValue(responseEnvelope);
            insertCommand.Parameters.AddWithValue(_retention.TotalSeconds);

            await using var reader = await insertCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                insertedEnvelope = reader.GetFieldValue<byte[]>(0);
        }

        if (insertedEnvelope is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new IdempotencyOutcome(IdempotencyStatus.Created, insertedEnvelope);
        }

        // A record already exists; lock it so the classification is atomic
        // against concurrent callers and the expired check runs in the
        // database clock. An expired record is replaced in place (fresh
        // registration); an active record with the same hash replays without
        // touching retention or envelope; any other combination conflicts.
        string storedHash;
        byte[] storedEnvelope;
        bool expired;
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText =
                """
                SELECT request_hash, response_envelope, expires_at <= now() AS expired
                FROM idempotency_keys
                WHERE client_id = $1 AND operation_id = $2
                FOR UPDATE;
                """;
            lockCommand.Parameters.AddWithValue(key.ClientId);
            lockCommand.Parameters.AddWithValue(key.OperationId);

            await using var reader = await lockCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    "Idempotency record disappeared between the insert attempt and the lock.");
            }

            storedHash = reader.GetString(0).TrimEnd();
            storedEnvelope = reader.GetFieldValue<byte[]>(1);
            expired = reader.GetBoolean(2);
        }

        if (expired)
        {
            await using var replaceCommand = connection.CreateCommand();
            replaceCommand.Transaction = transaction;
            replaceCommand.CommandText =
                """
                UPDATE idempotency_keys
                SET request_hash = $3, response_envelope = $4, expires_at = now() + $5 * interval '1 second'
                WHERE client_id = $1 AND operation_id = $2;
                """;
            replaceCommand.Parameters.AddWithValue(key.ClientId);
            replaceCommand.Parameters.AddWithValue(key.OperationId);
            replaceCommand.Parameters.AddWithValue(requestHash);
            replaceCommand.Parameters.AddWithValue(responseEnvelope);
            replaceCommand.Parameters.AddWithValue(_retention.TotalSeconds);
            await replaceCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new IdempotencyOutcome(IdempotencyStatus.Created, responseEnvelope);
        }

        if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new IdempotencyKeyConflictException(key);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new IdempotencyOutcome(IdempotencyStatus.Replayed, storedEnvelope);
    }

    /// <summary>
    /// Executes a protected mutation and stores its terminal response in the
    /// same transaction. A concurrent caller waits for the unique-key claim
    /// and replays the committed envelope; a failed callback rolls back both
    /// the claim and the mutation.
    /// </summary>
    public async Task<IdempotencyOutcome> ExecuteAsync(
        IdempotencyKey key,
        ReadOnlyMemory<byte> requestBody,
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<byte[]>> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(mutation);

        var requestHash = RequestHash.Compute(requestBody);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        byte[]? claimed = null;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO idempotency_keys (client_id, operation_id, request_hash, response_envelope, expires_at)
                VALUES ($1, $2, $3, $4, now() + $5 * interval '1 second')
                ON CONFLICT (client_id, operation_id) DO NOTHING
                RETURNING response_envelope;
                """;
            insert.Parameters.AddWithValue(key.ClientId);
            insert.Parameters.AddWithValue(key.OperationId);
            insert.Parameters.AddWithValue(requestHash);
            insert.Parameters.AddWithValue(Array.Empty<byte>());
            insert.Parameters.AddWithValue(_retention.TotalSeconds);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                claimed = reader.GetFieldValue<byte[]>(0);
        }

        if (claimed is not null)
        {
            var response = await mutation(connection, transaction, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(response);
            await using var complete = connection.CreateCommand();
            complete.Transaction = transaction;
            complete.CommandText =
                "UPDATE idempotency_keys SET response_envelope = $3 WHERE client_id = $1 AND operation_id = $2;";
            complete.Parameters.AddWithValue(key.ClientId);
            complete.Parameters.AddWithValue(key.OperationId);
            complete.Parameters.AddWithValue(response);
            if (await complete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                throw new InvalidOperationException("Idempotency execution claim disappeared before completion.");
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new IdempotencyOutcome(IdempotencyStatus.Created, response);
        }

        await using var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText =
            "SELECT request_hash, response_envelope, expires_at <= now() FROM idempotency_keys WHERE client_id = $1 AND operation_id = $2 FOR UPDATE;";
        existing.Parameters.AddWithValue(key.ClientId);
        existing.Parameters.AddWithValue(key.OperationId);
        await using var existingReader = await existing.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await existingReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Idempotency record disappeared before replay classification.");
        var storedHash = existingReader.GetString(0).TrimEnd();
        var storedEnvelope = existingReader.GetFieldValue<byte[]>(1);
        var expired = existingReader.GetBoolean(2);
        await existingReader.DisposeAsync();
        if (expired)
            throw new InvalidOperationException("Expired idempotency execution requires a new operation key.");
        if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal))
            throw new IdempotencyKeyConflictException(key);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new IdempotencyOutcome(IdempotencyStatus.Replayed, storedEnvelope);
    }

    /// <summary>
    /// Deletes records older than the retention window (V0-ARC-003 §1:
    /// retention is 24 hours).
    /// </summary>
    public async Task<int> SweepExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "DELETE FROM idempotency_keys WHERE expires_at < now();");
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
