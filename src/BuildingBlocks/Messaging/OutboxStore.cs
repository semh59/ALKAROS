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
    private readonly TimeSpan _deliveryTimeout;
    private readonly Action<OutboxMessage, Exception>? _onDeliveryFailure;
    private readonly Dictionary<string, OutboxRetryProfile> _retryProfiles;

    /// <summary>Delivered messages are deleted after this long (their payload may hold personal data).</summary>
    public static readonly TimeSpan DispatchedRetention = TimeSpan.FromDays(14);

    /// <summary>Dead-lettered messages stay this long for operator triage, then are deleted.</summary>
    public static readonly TimeSpan DeadRetention = TimeSpan.FromDays(90);

    /// <param name="retryProfiles">
    /// V12-RMD-008: event types with their own retry budget; every other event type keeps the default one.
    /// </param>
    /// <param name="deliveryTimeout">Upper bound of one sink call; an expired call counts as a failed attempt.</param>
    /// <param name="onDeliveryFailure">Called with the exception of every failed delivery (the stored error holds only its type name).</param>
    public OutboxStore(
        NpgsqlDataSource dataSource,
        TimeSpan? baseDelay = null,
        TimeSpan? leaseTimeout = null,
        IEnumerable<OutboxRetryProfile>? retryProfiles = null,
        TimeSpan? deliveryTimeout = null,
        Action<OutboxMessage, Exception>? onDeliveryFailure = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
        if (_baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
        _leaseTimeout = leaseTimeout ?? TimeSpan.FromMinutes(5);
        if (_leaseTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseTimeout), "Lease timeout must be positive.");

        _deliveryTimeout = deliveryTimeout ?? TimeSpan.FromSeconds(30);
        if (_deliveryTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(deliveryTimeout), "Delivery timeout must be positive.");
        _onDeliveryFailure = onDeliveryFailure;

        var profiles = new Dictionary<string, OutboxRetryProfile>(StringComparer.Ordinal);
        foreach (var profile in retryProfiles ?? [])
        {
            ArgumentNullException.ThrowIfNull(profile, nameof(retryProfiles));
            if (!profiles.TryAdd(profile.EventType, profile))
                throw new ArgumentException($"Event type '{profile.EventType}' has more than one retry profile.", nameof(retryProfiles));
        }

        _retryProfiles = profiles;
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
                      status, attempt_count, lease_generation, created_at, next_retry_at, dispatched_at, last_error;
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
    /// Persists a domain event in the outbox on the caller's open connection
    /// and transaction, so the outbox row commits together with the domain
    /// write that produced the event (transactional outbox). The row is
    /// invisible — and never delivered — until that transaction commits; a
    /// rollback takes the row with it.
    /// </summary>
    public static async Task EnqueueAsync(
        OutboxEnvelope envelope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO outbox_messages (event_type, aggregate_type, aggregate_id, payload_envelope)
            VALUES ($1, $2, $3, $4);
            """;
        command.Parameters.AddWithValue(envelope.EventType);
        command.Parameters.AddWithValue(envelope.AggregateType);
        command.Parameters.AddWithValue(envelope.AggregateId);
        command.Parameters.AddWithValue(envelope.PayloadEnvelope);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

        for (var index = 0; index < messages.Count; index++)
        {
            try
            {
                if (await DeliverAsync(handler, messages[index], cancellationToken).ConfigureAwait(false))
                    attempted++;
            }
            catch
            {
                // Whatever stops the batch (shutdown, a lost lease, a database fault), the messages not yet
                // tried go back to pending now instead of waiting out the lease.
                await ReleaseAsync(messages.Skip(index).ToList()).ConfigureAwait(false);
                throw;
            }
        }

        return attempted;
    }

    /// <returns><c>false</c> when the lease was lost before delivery, so the message was skipped.</returns>
    private async Task<bool> DeliverAsync(IOutboxDeliverySink handler, OutboxMessage message, CancellationToken cancellationToken)
    {
        var leaseGeneration = message.LeaseGeneration + 1;
        if (!await RenewLeaseAsync(message.Id, leaseGeneration, cancellationToken).ConfigureAwait(false))
            return false;

        string? failure = null;
        var handled = false;
        using var deliveryScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deliveryScope.CancelAfter(_deliveryTimeout);
        try
        {
            handled = await handler.HandleAsync(message, deliveryScope.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout of the sink's own call (or of deliveryScope) is a failed attempt like any other.
            failure = ex.GetType().Name;
            _onDeliveryFailure?.Invoke(message, ex);
        }

        // The sink already ran: record the outcome even when shutdown has begun, or the message is delivered twice.
        await using var connection = await _dataSource.OpenConnectionAsync(CancellationToken.None).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);

        if (handled)
            await MarkDispatchedAsync(connection, transaction, message.Id, leaseGeneration, CancellationToken.None).ConfigureAwait(false);
        else
            await RecordFailureAsync(
                    connection, transaction, message.Id, message.EventType, leaseGeneration,
                    failure ?? "handler returned false", CancellationToken.None)
                .ConfigureAwait(false);

        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> RenewLeaseAsync(Guid id, long leaseGeneration, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "UPDATE outbox_messages SET claimed_at = now() WHERE id = $1 AND status = 'in_flight' AND lease_generation = $2;");
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(leaseGeneration);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private async Task ReleaseAsync(List<OutboxMessage> messages)
    {
        if (messages.Count == 0)
            return;
        try
        {
            await using var command = _dataSource.CreateCommand(
                """
                UPDATE outbox_messages m
                SET status = 'pending', claimed_at = NULL
                FROM unnest($1::uuid[], $2::bigint[]) AS held(id, lease_generation)
                WHERE m.id = held.id AND m.status = 'in_flight' AND m.lease_generation = held.lease_generation;
                """);
            command.Parameters.AddWithValue(messages.Select(message => message.Id).ToArray());
            command.Parameters.AddWithValue(messages.Select(message => message.LeaseGeneration + 1).ToArray());
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            // Best effort: the lease timeout still returns them to pending.
        }
    }

    /// <summary>
    /// Deletes delivered messages older than <see cref="DispatchedRetention"/> and dead-lettered ones older than
    /// <see cref="DeadRetention"/>, in bounded chunks.
    /// </summary>
    /// <returns>The number of deleted messages.</returns>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            DELETE FROM outbox_messages
            WHERE id IN (
                SELECT id FROM outbox_messages
                WHERE (status = 'dispatched' AND dispatched_at < now() - $1 * interval '1 second')
                   OR (status = 'dead' AND created_at < now() - $2 * interval '1 second')
                LIMIT 1000);
            """);
        command.Parameters.AddWithValue(DispatchedRetention.TotalSeconds);
        command.Parameters.AddWithValue(DeadRetention.TotalSeconds);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Leases that outlived the timeout return to pending so a crashed worker can never strand a message.
        // The abandoned attempt counts: a message that keeps crashing its worker runs out of budget and dies
        // instead of looping forever. Runs inside the claim transaction, so released records are claimable immediately.
        await ReleaseExpiredLeasesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

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
                SET status = 'in_flight', claimed_at = now(), lease_generation = lease_generation + 1
                WHERE id = ANY($1);
                """;
            leaseCommand.Parameters.AddWithValue(messages.Select(message => message.Id).ToArray());
            await leaseCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return messages;
    }

    private async Task ReleaseExpiredLeasesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var expired = new List<(Guid Id, bool Dead)>();
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                """
                SELECT id, event_type, attempt_count FROM outbox_messages
                WHERE status = 'in_flight' AND claimed_at <= now() - $1 * interval '1 second'
                FOR UPDATE SKIP LOCKED;
                """;
            select.Parameters.AddWithValue(_leaseTimeout.TotalSeconds);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var maxAttempts = _retryProfiles.TryGetValue(reader.GetString(1), out var profile)
                    ? profile.MaxAttempts
                    : RetryPolicy.MaxAttempts;
                expired.Add((reader.GetGuid(0), reader.GetInt32(2) + 1 >= maxAttempts));
            }
        }

        foreach (var dead in new[] { true, false })
        {
            var ids = expired.Where(item => item.Dead == dead).Select(item => item.Id).ToArray();
            if (ids.Length == 0)
                continue;
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE outbox_messages
                SET status = $2, claimed_at = NULL, attempt_count = attempt_count + 1, last_error = 'lease expired'
                WHERE id = ANY($1);
                """;
            update.Parameters.AddWithValue(ids);
            update.Parameters.AddWithValue(dead ? "dead" : "pending");
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
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
                   attempt_count, lease_generation, created_at, next_retry_at, dispatched_at, last_error
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
            reader.GetInt64(7),
            new DateTimeOffset(reader.GetDateTime(8)),
            reader.IsDBNull(9) ? null : new DateTimeOffset(reader.GetDateTime(9)),
            reader.IsDBNull(10) ? null : new DateTimeOffset(reader.GetDateTime(10)),
            reader.IsDBNull(11) ? null : reader.GetString(11));

    private static async Task MarkDispatchedAsync(
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
            UPDATE outbox_messages
            SET status = 'dispatched', dispatched_at = now()
            WHERE id = $1 AND status = 'in_flight' AND lease_generation = $2;
            """;
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(leaseGeneration);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected != 1)
            throw new InvalidOperationException(
                "Outbox message lease was lost before dispatch could be confirmed; the message will be re-claimed.");
    }

    private Task RecordFailureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string eventType,
        long leaseGeneration,
        string error,
        CancellationToken cancellationToken)
        => _retryProfiles.TryGetValue(eventType, out var profile)
            ? RetryPolicy.RecordFailureAsync(
                connection, "outbox_messages", id, leaseGeneration, error, profile, transaction, cancellationToken)
            : RetryPolicy.RecordFailureAsync(
                connection, "outbox_messages", id, leaseGeneration, error, _baseDelay, transaction, cancellationToken);
}

/// <summary>How long the dispatch loop waits between iterations.</summary>
public static class OutboxDispatchPacing
{
    public static readonly TimeSpan Idle = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan MaxFaultDelay = TimeSpan.FromSeconds(30);

    /// <summary>The wait after <paramref name="consecutiveFaults"/> failed iterations in a row: doubles from one second up to <see cref="MaxFaultDelay"/>.</summary>
    public static TimeSpan DelayAfter(int consecutiveFaults)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(consecutiveFaults);
        if (consecutiveFaults == 0)
            return Idle;
        var doublings = Math.Min(consecutiveFaults - 1, 16);
        return TimeSpan.FromTicks(Math.Min(Idle.Ticks << doublings, MaxFaultDelay.Ticks));
    }
}
