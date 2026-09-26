using Npgsql;

namespace ALKAROS.Messaging;

/// <summary>
/// Retry and dead-letter policy of V0-ARC-003 (max 3 attempts, exponential
/// backoff). A message that fails three times is moved to the dead-letter
/// state; each earlier failure schedules the next attempt with
/// base-delay * 2^(attempts so far).
/// </summary>
public static class RetryPolicy
{
    public const int MaxAttempts = 3;

    /// <summary>
    /// The only table identifiers accepted by <see cref="RecordFailureAsync"/>.
    /// The SQL surface is closed to these registered constants; any other
    /// value is rejected before a command is built.
    /// V1-RMD-134: found by an independent audit (2026-09-09) — this used to
    /// also register "inbox_messages" for the generic consumer-side Inbox
    /// pattern (InboxStore/IInboxHandler), which nothing in the codebase
    /// ever implemented or called; every real IIntegrationEventConsumer
    /// dedupes through its own domain-specific mechanism instead. Removed
    /// along with the rest of that dead surface — outbox_messages is this
    /// policy's only real caller (OutboxDispatcherHostedService).
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedTableNames = new HashSet<string>(
        ["outbox_messages"],
        StringComparer.Ordinal);

    /// <summary>
    /// The delay before the next attempt after <paramref name="completedAttempts"/>
    /// failed attempts. Only valid below <see cref="MaxAttempts"/>; at the
    /// threshold the message is dead, not retried.
    /// </summary>
    public static TimeSpan NextRetryDelay(int completedAttempts, TimeSpan baseDelay)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(completedAttempts);
        if (baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
        if (completedAttempts >= MaxAttempts)
            throw new ArgumentOutOfRangeException(
                nameof(completedAttempts),
                $"A message with {MaxAttempts} failed attempts is dead and is never retried.");

        var factor = Math.Pow(2, completedAttempts - 1);
        return TimeSpan.FromMilliseconds(Math.Min(
            baseDelay.TotalMilliseconds * factor,
            TimeSpan.MaxValue.TotalMilliseconds));
    }

    /// <summary>
    /// Records a delivery failure on <paramref name="tableName"/>: increments
    /// the attempt counter, saves the error, and moves the message to the
    /// dead-letter state after <see cref="MaxAttempts"/> attempts or schedules
    /// the next exponential-backoff retry. Only messages leased by the
    /// current dispatcher (status <c>in_flight</c>) are touched, so a
    /// concurrently recovered record is never overwritten. When
    /// <paramref name="transaction"/> is provided the update joins it.
    /// </summary>
    public static Task RecordFailureAsync(
        NpgsqlConnection connection,
        string tableName,
        Guid id,
        long leaseGeneration,
        string error,
        TimeSpan baseDelay,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
        return RecordFailureCoreAsync(
            connection, tableName, id, leaseGeneration, error, MaxAttempts, baseDelay, null, transaction, cancellationToken);
    }

    /// <summary>
    /// V12-RMD-008: <see cref="RecordFailureAsync(NpgsqlConnection, string, Guid, long, string, TimeSpan, NpgsqlTransaction?, CancellationToken)"/>
    /// with an event type's own budget: dead after <see cref="OutboxRetryProfile.MaxAttempts"/> attempts, each
    /// wait capped at <see cref="OutboxRetryProfile.MaxDelay"/>.
    /// </summary>
    public static Task RecordFailureAsync(
        NpgsqlConnection connection,
        string tableName,
        Guid id,
        long leaseGeneration,
        string error,
        OutboxRetryProfile profile,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return RecordFailureCoreAsync(
            connection, tableName, id, leaseGeneration, error, profile.MaxAttempts, profile.BaseDelay, profile.MaxDelay,
            transaction, cancellationToken);
    }

    private static async Task RecordFailureCoreAsync(
        NpgsqlConnection connection,
        string tableName,
        Guid id,
        long leaseGeneration,
        string error,
        int maxAttempts,
        TimeSpan baseDelay,
        TimeSpan? maxDelay,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (!AllowedTableNames.Contains(tableName))
            throw new ArgumentException(
                $"Table name '{tableName}' is not an allowed retry table.", nameof(tableName));
        ArgumentNullException.ThrowIfNull(error);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            UPDATE {tableName}
            SET attempt_count = attempt_count + 1,
                last_error = $2,
                status = CASE WHEN attempt_count + 1 >= $3 THEN 'dead' ELSE 'pending' END,
                next_retry_at = CASE WHEN attempt_count + 1 >= $3
                                     THEN NULL
                                     ELSE now() + make_interval(
                                         -- LEAST ignores a NULL cap: the default budget has none.
                                         secs => LEAST($6, $4 * power(2::double precision, attempt_count)))
                                END
            WHERE id = $1 AND status = 'in_flight' AND lease_generation = $5;
            """;
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(SanitizeError(error));
        command.Parameters.AddWithValue(maxAttempts);
        command.Parameters.AddWithValue(baseDelay.TotalSeconds);
        command.Parameters.AddWithValue(leaseGeneration);
        command.Parameters.Add(new NpgsqlParameter { Value = maxDelay is { } cap ? cap.TotalSeconds : DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Double });
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected != 1)
            throw new InvalidOperationException(
                $"Message lease was lost before failure could be recorded on '{tableName}'.");
    }

    public static string SanitizeError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return string.Empty;

        // Handler exception text is untrusted and may contain secrets, PII,
        // SQL fragments, or provider credentials. Persist only a bounded,
        // allowlisted classification; operators can correlate the failure
        // through structured telemetry without exposing the raw message.
        return "handler failure";
    }
}

/// <summary>
/// V12-RMD-008: the retry budget of one outbox event type. A message of an event type without a profile
/// keeps the default <see cref="RetryPolicy"/> budget (<see cref="RetryPolicy.MaxAttempts"/> attempts,
/// the store's base delay, no cap). A profile is for deliveries to an outside party whose outages last
/// minutes, not seconds: its message waits <see cref="BaseDelay"/> x 2^(attempts so far), at most
/// <see cref="MaxDelay"/>, and is dead after <see cref="MaxAttempts"/> failed attempts.
/// </summary>
public sealed class OutboxRetryProfile
{
    public OutboxRetryProfile(string eventType, int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        if (baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
        if (maxDelay < baseDelay)
            throw new ArgumentOutOfRangeException(nameof(maxDelay), "The longest wait cannot be shorter than the first.");

        EventType = eventType;
        MaxAttempts = maxAttempts;
        BaseDelay = baseDelay;
        MaxDelay = maxDelay;
    }

    public string EventType { get; }

    public int MaxAttempts { get; }

    public TimeSpan BaseDelay { get; }

    public TimeSpan MaxDelay { get; }
}
