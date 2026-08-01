using Npgsql;

namespace ALKAROS.Messaging;

/// <summary>
/// Persists domain events in the <c>outbox_messages</c> table and dispatches
/// them at-least-once through an <see cref="IOutboxDeliverySink"/> (V0-ARC-003
/// Â§3). Pending records never carry a persistent lock: a process restart
/// leaves them eligible, so no message is lost between restarts. Failed
/// deliveries are retried with exponential backoff and move to the
/// dead-letter state after <see cref="RetryPolicy.MaxAttempts"/> attempts.
/// </summary>
public sealed class OutboxStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _baseDelay;

    public OutboxStore(NpgsqlDataSource dataSource, TimeSpan? baseDelay = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
        if (_baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
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
    /// Claims pending messages (due now, SKIP LOCKED) and delivers each to
    /// <paramref name="handler"/>. Successful delivery marks the message
    /// dispatched; failures increment the attempt counter, schedule the
    /// exponential backoff retry, and move the message to dead-letter after
    /// <see cref="RetryPolicy.MaxAttempts"/> attempts.
    /// </summary>
    /// <returns>The number of messages that were delivered or failed.</returns>
    public async Task<int> DispatchAsync(
        IOutboxDeliverySink handler,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var messages = await ClaimPendingAsync(batchSize, cancellationToken).ConfigureAwait(false);
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

            if (handled)
                await MarkDispatchedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            else
                await RecordFailureAsync(message.Id, failure ?? "handler returned false", cancellationToken)
                    .ConfigureAwait(false);

            attempted++;
        }

        return attempted;
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, event_type, aggregate_type, aggregate_id, payload_envelope, status,
                   attempt_count, created_at, next_retry_at, dispatched_at, last_error
            FROM outbox_messages
            WHERE status = 'pending' AND (next_retry_at IS NULL OR next_retry_at <= now())
            ORDER BY created_at
            LIMIT $1
            FOR UPDATE SKIP LOCKED;
            """);
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

    private async Task MarkDispatchedAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE outbox_messages
            SET status = 'dispatched', dispatched_at = now()
            WHERE id = $1 AND status = 'pending';
            """);
        command.Parameters.AddWithValue(id);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordFailureAsync(
        Guid id,
        string error,
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE outbox_messages
            SET attempt_count = attempt_count + 1,
                last_error = $2,
                status = CASE WHEN attempt_count + 1 >= $3 THEN 'dead' ELSE 'pending' END,
                next_retry_at = CASE WHEN attempt_count + 1 >= $3
                                     THEN NULL ELSE now() + make_interval(secs => $4) END
            WHERE id = $1 AND status = 'pending';
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(error);
        command.Parameters.AddWithValue(RetryPolicy.MaxAttempts);
        command.Parameters.AddWithValue(_baseDelay.TotalSeconds);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
