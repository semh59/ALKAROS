using Npgsql;

namespace ALKAROS.Messaging;

/// <summary>
/// Persists external callbacks in the <c>inbox_messages</c> table and drives
/// them through an <see cref="IInboxHandler"/>. Deduplication by
/// (source, externalEventId) is enforced by the unique constraint; a
/// message that fails three times is moved to the dead-letter state
/// (V0-ARC-003 Â§2).
/// </summary>
public sealed class InboxStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _baseDelay;

    public InboxStore(NpgsqlDataSource dataSource, TimeSpan? baseDelay = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
        if (_baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
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
    /// Claims pending messages with SKIP LOCKED so concurrent dispatchers
    /// never race, then hands each to <paramref name="handler"/>. Successful
    /// handling marks the message processed; failures increment the attempt
    /// counter, schedule the exponential backoff retry, and move the message
    /// to dead-letter after <see cref="RetryPolicy.MaxAttempts"/> attempts.
    /// </summary>
    /// <returns>The number of messages that were attempted.</returns>
    public async Task<int> ProcessPendingAsync(
        IInboxHandler handler,
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
                await MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            else
                await RecordFailureAsync(message.Id, failure ?? "handler returned false", cancellationToken)
                    .ConfigureAwait(false);

            attempted++;
        }

        return attempted;
    }

    private async Task<IReadOnlyList<InboxMessage>> ClaimPendingAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, source, external_event_id, payload_envelope, status, attempt_count,
                   received_at, processed_at, last_error
            FROM inbox_messages
            WHERE status = 'pending' AND (next_retry_at IS NULL OR next_retry_at <= now())
            ORDER BY received_at
            LIMIT $1
            FOR UPDATE SKIP LOCKED;
            """);
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
                new DateTimeOffset(reader.GetDateTime(6)),
                reader.IsDBNull(7) ? null : new DateTimeOffset(reader.GetDateTime(7)),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return messages;
    }

    private async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE inbox_messages
            SET status = 'processed', processed_at = now()
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
            UPDATE inbox_messages
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
