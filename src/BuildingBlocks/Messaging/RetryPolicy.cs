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
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedTableNames = new HashSet<string>(
        ["inbox_messages", "outbox_messages"],
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
    public static async Task RecordFailureAsync(
        NpgsqlConnection connection,
        string tableName,
        Guid id,
        string error,
        TimeSpan baseDelay,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (!AllowedTableNames.Contains(tableName))
            throw new ArgumentException(
                $"Table name '{tableName}' is not an allowed retry table.", nameof(tableName));
        ArgumentNullException.ThrowIfNull(error);
        if (baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");

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
                                         secs => $4 * power(2::double precision, attempt_count))
                                END
            WHERE id = $1 AND status = 'in_flight';
            """;
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(error);
        command.Parameters.AddWithValue(MaxAttempts);
        command.Parameters.AddWithValue(baseDelay.TotalSeconds);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
