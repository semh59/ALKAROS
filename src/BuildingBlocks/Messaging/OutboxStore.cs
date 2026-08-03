using Npgsql;

namespace ALKAROS.Messaging;

/// <summary>
/// Persists domain events in the <c>outbox_messages</c> table and dispatches
/// them at-least-once through an <see cref="IOutboxDeliverySink"/> (V0-ARC-003
/// §3). A dispatcher leases a message (<see cref="OutboxStatus.InFlight"/>) in
/// a short transaction and runs the sink strictly outside it, so no database
/// lock is held across a delivery side effect. A crashed worker leaves the
/// message in flight; the lease expires and the message returns to pending,
/// so no message is lost between restarts. Failed deliveries are retried
/// with exponential backoff and move to the dead-letter state after
/// <see cref="RetryPolicy.MaxAttempts"/> attempts.
/// </summary>
public sealed class OutboxStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _leaseTimeout;

    public OutboxStore(
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
    /// Persists a domain event in the outbox with status
    /// <see cref="OutboxStatus.Pending"/>.
    /// </summary>
    public async Task<OutboxMessage> EnqueueAsync(
        OutboxEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO outbox_messages (event_type, aggregate_type, aggregate_id, payload_envelope)
            VALUES ($1, $2, $3, $4)
            RETURNING id, event_type, aggregate_type, aggregate_id, payload_envelope,
                      status, attempt_count, created_at, next_retry_at, dispatched_at, last_error;
            """);
        command.Parameters.AddWithValue(envelope.EventType);
        command.Parameters.AddWithValue(envelope.AggregateType);
        command.Parameters.AddWithValue(envelope.AggregateId);
        command.Parameters.AddWithValue(envelope.PayloadEnvelope);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Enqueue returned no outbox record.");

        return ReadMessage(reader);
    }

    /// <summary>
    /// Claims pending messages (due now, SKIP LOCKED) into a short in-flight
    /// lease and delivers each to <paramref name="handler"/> strictly after
    /// the claim transaction committed. Successful delivery marks the message
    /// dispatched; failures increment the attempt counter, schedule the
    /// exponential backoff retry, and move the message to dead-letter after
    /// <see cref="RetryPolicy.MaxAttempts"/> attempts.
    /// </summary>
    /// <returns>The number of messages that were attempted.</returns>
    public async Task<int> DispatchAsync(
        IOutboxDeliverySink handler,
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
                await MarkDispatchedAsync(connection, transaction, message.Id, cancellationToken).ConfigureAwait(false);
            else
                await RecordFailureAsync(
                        connection, transaction, message.Id, failure ?? "handler returned false", cancellationToken)
                    .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            attempted++;
        }

        return attempted;
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
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
                UPDATE outbox_messages
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
                UPDATE outbox_messages
                SET status = 'in_flight', claimed_at = now()
                WHERE id = ANY($1);
                """;
            leaseCommand.Parameters.AddWithValue(messages.Select(message => message.Id).ToArray());
            await leaseCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return messages;
    }

    private static async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT id, event_type, aggregate_type, aggregate_id, payload_envelope, status,
                   attempt_count, created_at, next_retry_at, dispatched_at, last_error
            FROM outbox_messages
            WHERE status = 'pending' AND (next_retry_at IS NULL OR next_retry_at <= now())
            ORDER BY created_at
            LIMIT $1
            FOR UPDATE SKIP LOCKED;
            """;
        command.Parameters.AddWithValue(batchSize);

        var messages = new List<OutboxMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            messages.Add(ReadMessage(reader));

        return messages;
    }

    private static OutboxMessage ReadMessage(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetFieldValue<byte[]>(4),
            Enum.Parse<OutboxStatus>(reader.GetString(5), ignoreCase: true),
            reader.GetInt32(6),
            new DateTimeOffset(reader.GetDateTime(7)),
            reader.IsDBNull(8) ? null : new DateTimeOffset(reader.GetDateTime(8)),
            reader.IsDBNull(9) ? null : new DateTimeOffset(reader.GetDateTime(9)),
            reader.IsDBNull(10) ? null : reader.GetString(10));

    private static async Task MarkDispatchedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE outbox_messages
            SET status = 'dispatched', dispatched_at = now()
            WHERE id = $1 AND status = 'in_flight';
            """;
        command.Parameters.AddWithValue(id);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected != 1)
            throw new InvalidOperationException(
                "Outbox message lease was lost before dispatch could be confirmed; the message will be re-claimed.");
    }

    private Task RecordFailureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string error,
        CancellationToken cancellationToken)
        => RetryPolicy.RecordFailureAsync(
            connection, "outbox_messages", id, error, _baseDelay, transaction, cancellationToken);
}
