using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Polling;

/// <summary>How one platform's due poll ended.</summary>
public enum OnlineOrderPollOutcome
{
    Polled,
    RateLimited,
    NotConfigured,
    Failed
}

/// <summary>
/// V12-ONL-009: polls every platform that offers an order list, when its poll is due, into the shared inbox
/// (V12-ONL-010) that its webhook also writes to; the inbox keeps one row per event, so an order that arrives both
/// ways is processed once. Per platform, one poll runs at a time (its state row is locked for the poll) and the
/// cursor advances only after every event of the page is stored. A rate-limited poll waits at least as long as the
/// platform asked; a failing one backs off exponentially. Both keep the cursor, so no order is lost, and both count
/// as a failure streak that reconciliation shows once it reaches <see cref="FailureStreakForCase"/>.
/// </summary>
public sealed class OnlineOrderPoller
{
    public const int FailureStreakForCase = 3;
    public const int MaxBackoffSeconds = 1800;
    public const int MaxCursorLength = 512;

    private readonly NpgsqlDataSource _dataSource;
    private readonly ProviderInbox _inbox;
    private readonly IReadOnlyList<IOnlineOrderPollingSource> _sources;

    public OnlineOrderPoller(NpgsqlDataSource dataSource, ProviderInbox inbox, IEnumerable<IOnlineOrderPollingSource> sources)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources.ToList();
        var duplicate = _sources.GroupBy(source => source.Provider, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Platform '{duplicate.Key}' has more than one polling source.", nameof(sources));
    }

    /// <summary>Polls each platform whose poll is due; returns what happened per polled platform.</summary>
    public async Task<IReadOnlyDictionary<string, OnlineOrderPollOutcome>> PollDueAsync(CancellationToken cancellationToken = default)
    {
        var outcomes = new Dictionary<string, OnlineOrderPollOutcome>(StringComparer.Ordinal);
        foreach (var source in _sources)
        {
            if (await PollIfDueAsync(source, cancellationToken).ConfigureAwait(false) is { } outcome)
                outcomes[source.Provider] = outcome;
        }

        return outcomes;
    }

    private async Task<OnlineOrderPollOutcome?> PollIfDueAsync(IOnlineOrderPollingSource source, CancellationToken cancellationToken)
    {
        var interval = Math.Max(1, source.Interval.TotalSeconds);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (var ensure = new NpgsqlCommand(
            "INSERT INTO online_ordering.provider_poll_state (provider) VALUES ($1) ON CONFLICT (provider) DO NOTHING;", connection))
        {
            ensure.Parameters.AddWithValue(source.Provider);
            await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string? cursor;
        await using (var claim = new NpgsqlCommand(
            """
            SELECT poll_cursor FROM online_ordering.provider_poll_state
            WHERE provider = $1 AND next_poll_at <= now()
            FOR UPDATE SKIP LOCKED;
            """, connection, transaction))
        {
            claim.Parameters.AddWithValue(source.Provider);
            await using var reader = await claim.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await reader.DisposeAsync().ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            cursor = reader.IsDBNull(0) ? null : reader.GetString(0);
        }

        OnlineOrderPollOutcome outcome;
        try
        {
            var page = await source.PollAsync(cursor, cancellationToken).ConfigureAwait(false);
            if (page.NextCursor is { Length: > MaxCursorLength })
                throw new InvalidOperationException($"Platform '{source.Provider}' returned a cursor longer than {MaxCursorLength} characters.");
            foreach (var polled in page.Events)
            {
                await _inbox.StoreAsync(
                    new ProviderInboxEvent(source.Provider, polled.EventKey, polled.ExternalOrderId, polled.ProviderStatus,
                        polled.ProviderUpdatedAt, polled.RawBody),
                    cancellationToken).ConfigureAwait(false);
            }

            await ExecuteAsync(
                """
                UPDATE online_ordering.provider_poll_state
                SET poll_cursor = COALESCE($2, poll_cursor), next_poll_at = now() + make_interval(secs => $3),
                    consecutive_failures = 0, failing_since = NULL, last_error = NULL, last_success_at = now()
                WHERE provider = $1;
                """, connection, transaction, cancellationToken, source.Provider, Text(page.NextCursor), interval).ConfigureAwait(false);
            outcome = OnlineOrderPollOutcome.Polled;
        }
        catch (OnlineOrderPollingNotConfiguredException)
        {
            // The channel is off: not a failure, and an earlier failure streak no longer means anything.
            await ExecuteAsync(
                """
                UPDATE online_ordering.provider_poll_state
                SET next_poll_at = now() + make_interval(secs => $2), consecutive_failures = 0, failing_since = NULL,
                    last_error = NULL
                WHERE provider = $1;
                """, connection, transaction, cancellationToken, source.Provider, interval).ConfigureAwait(false);
            outcome = OnlineOrderPollOutcome.NotConfigured;
        }
        catch (OnlineOrderPollRateLimitedException limited)
        {
            var wait = Math.Min(MaxBackoffSeconds, Math.Max(interval, limited.RetryAfter?.TotalSeconds ?? 0));
            await RecordFailureAsync(source.Provider, "RateLimited", wait, connection, transaction, cancellationToken).ConfigureAwait(false);
            outcome = OnlineOrderPollOutcome.RateLimited;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Anything stored before the failure stays (the inbox is idempotent); the cursor does not move, so the
            // next poll reads the same events again.
            await using var failures = new NpgsqlCommand(
                "SELECT consecutive_failures FROM online_ordering.provider_poll_state WHERE provider = $1;", connection, transaction);
            failures.Parameters.AddWithValue(source.Provider);
            var streak = (int)(await failures.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            var wait = Math.Min(MaxBackoffSeconds, interval * Math.Pow(2, streak + 1));
            await RecordFailureAsync(source.Provider, ex.GetType().Name, wait, connection, transaction, cancellationToken).ConfigureAwait(false);
            outcome = OnlineOrderPollOutcome.Failed;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    private static Task RecordFailureAsync(
        string provider, string error, double waitSeconds, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            """
            UPDATE online_ordering.provider_poll_state
            SET next_poll_at = now() + make_interval(secs => $3), consecutive_failures = consecutive_failures + 1,
                failing_since = COALESCE(failing_since, now()), last_error = left($2, 200)
            WHERE provider = $1;
            """, connection, transaction, cancellationToken, provider, error, waitSeconds);

    /// <summary>A text parameter that may be null (a bare null has no type to infer).</summary>
    private static NpgsqlParameter Text(string? value) =>
        new() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text, Value = (object?)value ?? DBNull.Value };

    private static async Task ExecuteAsync(
        string sql, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken,
        params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values)
        {
            if (value is NpgsqlParameter parameter)
                command.Parameters.Add(parameter);
            else
                command.Parameters.AddWithValue(value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
