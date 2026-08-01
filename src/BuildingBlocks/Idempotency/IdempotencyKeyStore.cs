using Npgsql;

namespace ALKAROS.Idempotency;

/// <summary>
/// Persists idempotency records in the <c>idempotency_keys</c> table and
/// enforces the V0-ARC-003 §1 semantics atomically in a single statement:
/// first use registers the operation, an identical replay returns the cached
/// response envelope, and a conflicting replay fails with
/// <see cref="IdempotencyKeyConflictException"/>.
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
    /// </summary>
    /// <exception cref="IdempotencyKeyConflictException">
    /// The key exists with a different request hash (IDEMPOTENCY_KEY_CONFLICT).
    /// </exception>
    public async Task<IdempotencyOutcome> RegisterOrReplayAsync(
        IdempotencyKey key,
        ReadOnlyMemory<byte> requestBody,
        byte[] responseEnvelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(responseEnvelope);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO idempotency_keys (client_id, operation_id, request_hash, response_envelope, expires_at)
            VALUES ($1, $2, $3, $4, now() + $5 * interval '1 second')
            ON CONFLICT (client_id, operation_id)
            DO UPDATE SET expires_at = EXCLUDED.expires_at
            WHERE idempotency_keys.request_hash = EXCLUDED.request_hash
            RETURNING (xmax = 0) AS inserted, response_envelope;
            """);
        command.Parameters.AddWithValue(key.ClientId);
        command.Parameters.AddWithValue(key.OperationId);
        command.Parameters.AddWithValue(RequestHash.Compute(requestBody));
        command.Parameters.AddWithValue(responseEnvelope);
        command.Parameters.AddWithValue(_retention.TotalSeconds);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new IdempotencyKeyConflictException(key);

        var inserted = reader.GetBoolean(0);
        var envelope = reader.GetFieldValue<byte[]>(1);
        return new IdempotencyOutcome(
            inserted ? IdempotencyStatus.Created : IdempotencyStatus.Replayed,
            envelope);
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
