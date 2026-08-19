using Npgsql;

namespace ALKAROS.Messaging;

/// <summary>
/// Persists external callbacks in the <c>inbox_messages</c> table and drives
/// them through an <see cref="IInboxHandler"/>. Deduplication by
/// (source, externalEventId) is enforced by the unique constraint; a
/// message that fails three times is moved to the dead-letter state
/// (V0-ARC-003 §2). A dispatcher leases a message
/// (<see cref="InboxStatus.InFlight"/>) in a short transaction and runs the
/// handler strictly outside it, so no database lock is held across a
/// side effect; expired leases return to pending after a worker crash.
/// </summary>
public sealed class InboxStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _leaseTimeout;

    public InboxStore(
        NpgsqlDataSource dataSource,
        TimeSpan? baseDelay = null,
        TimeSpan? leaseTimeout = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
        if (_baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
        _leaseTimeout = leaseTimeout ?? TimeSpan.FromMinutes(5);
        if (_leaseTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseTimeout), "Lease timeout must be positive.");
    }

    /// <summary>
    /// Persists an external callback. Returns <c>false</c> when the same
    /// (source, externalEventId) was already stored; duplicates are never
    /// processed twice.
    /// </summary>
    public async Task<bool> TryEnqueueAsync(
        InboxEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO inbox_messages (source, external_event_id, payload_envelope)
            VALUES ($1, $2, $3)
            ON CONFLICT (source, external_event_id) DO NOTHING;
            """);
        command.Parameters.AddWithValue(envelope.Source);
        command.Parameters.AddWithValue(envelope.ExternalEventId);
        command.Parameters.AddWithValue(envelope.PayloadEnvelope);

        var inserted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return inserted == 1;
    }

    /// <summary>
    /// Claims pending messages (due now, SKIP LOCKED) into a short in-flight
    /// lease and hands each to <paramref name="handler"/> strictly after the
    /// claim transaction committed. Successful handling marks the message
    /// processed; failures increment the attempt counter, schedule the
    /// exponential backoff retry, and move the message to dead-letter after
    /// <see cref="RetryPolicy.MaxAttempts"/> attempts.
    /// </summary>
    /// <returns>The number of messages that were attempted.</returns>
    public async Task<int> ProcessPendingAsync(
        IInboxHandler handler,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var messages = await ClaimAsync(batchSize, cancellationToken).ConfigureAwait(false);
        var attempted = 0;

        foreach (var message in messages)
        {
            string? failure = null;
            var handled = false;
            try
            {
                handled = await handler.HandleAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failure = ex.Message;
            }

            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            if (handled)
                await MarkProcessedAsync(connection, transaction, message.Id, message.LeaseGeneration + 1, cancellationToken).ConfigureAwait(false);
            else
                await RecordFailureAsync(
                        connection, transaction, message.Id, message.LeaseGeneration + 1, failure ?? "handler returned false", cancellationToken)
                    .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            attempted++;
        }

        return attempted;
    }

    private async Task<IReadOnlyList<InboxMessage>> ClaimAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Leases that outlived the timeout return to pending so a crashed
        // worker can never strand a message. Runs inside the claim
        // transaction, so released records are claimable immediately.
        await using (var releaseCommand = connection.CreateCommand())
        {
            releaseCommand.Transaction = transaction;
            releaseCommand.CommandText =
                """
                UPDATE inbox_messages
                SET status = 'pending', claimed_at = NULL
                WHERE status = 'in_flight' AND claimed_at <= now() - $1 * interval '1 second';
                """;
            releaseCommand.Parameters.AddWithValue(_leaseTimeout.TotalSeconds);
            await releaseCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var messages = await ClaimPendingAsync(connection, transaction, batchSize, cancellationToken)
            .ConfigureAwait(false);
        if (messages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return messages;
        }

        await using (var leaseCommand = connection.CreateCommand())
        {
            leaseCommand.Transaction = transaction;
            leaseCommand.CommandText =
                """
                UPDATE inbox_messages
                SET status = 'in_flight', claimed_at = now(), lease_generation = lease_generation + 1
                WHERE id = ANY($1);
                """;
            leaseCommand.Parameters.AddWithValue(messages.Select(message => message.Id).ToArray());
            await leaseCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return messages;
    }

    private static async Task<IReadOnlyList<InboxMessage>> ClaimPendingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, source, external_event_id, payload_envelope, status, attempt_count,
                   lease_generation, received_at, processed_at, last_error
            FROM inbox_messages
            WHERE status = 'pending' AND (next_retry_at IS NULL OR next_retry_at <= now())
            ORDER BY received_at
            LIMIT $1
            FOR UPDATE SKIP LOCKED;
            """;
        command.Parameters.AddWithValue(batchSize);

        var messages = new List<InboxMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(new InboxMessage(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<byte[]>(3),
                Enum.Parse<InboxStatus>(reader.GetString(4), ignoreCase: true),
                reader.GetInt32(5),
                reader.GetInt64(6),
                new DateTimeOffset(reader.GetDateTime(7)),
                reader.IsDBNull(8) ? null : new DateTimeOffset(reader.GetDateTime(8)),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return messages;
    }

    private static async Task MarkProcessedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        long leaseGeneration,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE inbox_messages
            SET status = 'processed', processed_at = now()
            WHERE id = $1 AND status = 'in_flight' AND lease_generation = $2;
            """;
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(leaseGeneration);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected != 1)
            throw new InvalidOperationException(
                "Inbox message lease was lost before processing could be confirmed; the message will be re-claimed.");
    }

    private Task RecordFailureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        long leaseGeneration,
        string error,
        CancellationToken cancellationToken)
        => RetryPolicy.RecordFailureAsync(
            connection, "inbox_messages", id, leaseGeneration, error, _baseDelay, transaction, cancellationToken);
}
