using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.OnlineOrdering.StoreStatus;

/// <summary>A manager's request for one platform: what the restaurant should be there.</summary>
public sealed record OnlineStoreStatusRequest(string Provider, OnlineStoreState State, int? BusyMinutes);

/// <summary>One platform as the screen shows it: what was asked, whether the platform has been told, what it says now.</summary>
public sealed record OnlineStoreStatus(
    string Provider,
    bool Configured,
    OnlineStoreState DesiredState,
    DateTimeOffset? ClosedUntil,
    OnlineStoreCloseReason? Reason,
    bool Delivered,
    bool DeliveryFailing,
    PlatformStoreState? Platform);

public sealed class UnknownStoreStatusPlatformException(string provider)
    : Exception($"'{provider}' has no store status channel.")
{
    public string Provider { get; } = provider;
}

public sealed class InvalidStoreStatusRequestException(string message) : Exception(message);

/// <summary>
/// V12-ONL-011: keeps what a manager asked each platform to show (open, closed for the day, or busy until a time),
/// delivers it with retries, and ends a closure when its time comes — the platform reopens by itself where it supports a
/// closing time (Yemeksepeti), otherwise ALKAROS opens it (Trendyol Go). A closure "for today" lasts until the next
/// service day starts, 06:00 Europe/Istanbul (the business-day boundary the reports use). Every request is audited.
/// </summary>
public sealed class OnlineStoreStatusService
{
    public static readonly IReadOnlySet<int> BusyMinuteChoices = new HashSet<int> { 15, 30, 45, 60, 90, 120 };
    public const int MaxBackoffSeconds = 900;
    public const string AuditEventName = "OnlinePlatform.StoreStatusRequested";

    private static readonly TimeZoneInfo ServiceZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private readonly NpgsqlDataSource _dataSource;
    private readonly IReadOnlyDictionary<string, IOnlineStoreStatusChannel> _channels;
    private readonly TimeProvider _time;

    public OnlineStoreStatusService(NpgsqlDataSource dataSource, IEnumerable<IOnlineStoreStatusChannel> channels, TimeProvider time)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentNullException.ThrowIfNull(channels);
        _channels = channels.ToDictionary(c => c.Provider, StringComparer.Ordinal);
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public IReadOnlyCollection<string> Providers => _channels.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>The end of the current service day: the next 06:00 in Europe/Istanbul after <paramref name="now"/>.</summary>
    public static DateTimeOffset EndOfServiceDay(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, ServiceZone);
        var sixToday = new DateTime(local.Year, local.Month, local.Day, 6, 0, 0, DateTimeKind.Unspecified);
        var next = local.DateTime < sixToday ? sixToday : sixToday.AddDays(1);
        // Stored and compared in UTC (Npgsql writes timestamptz from UTC offsets only).
        return new DateTimeOffset(next, ServiceZone.GetUtcOffset(next)).ToUniversalTime();
    }

    /// <summary>Records the request (replacing an earlier one) for delivery and audits it.</summary>
    public async Task<OnlineStoreStatus> RequestAsync(OnlineStoreStatusRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_channels.TryGetValue(request.Provider, out var channel))
            throw new UnknownStoreStatusPlatformException(request.Provider);
        if (actorId == Guid.Empty)
            throw new InvalidStoreStatusRequestException("An actor is required.");
        if (!channel.IsConfigured)
            throw new InvalidStoreStatusRequestException("NotConfigured");

        var now = _time.GetUtcNow();
        (DateTimeOffset? until, OnlineStoreCloseReason? reason) = request.State switch
        {
            OnlineStoreState.Open when request.BusyMinutes is null => ((DateTimeOffset?)null, (OnlineStoreCloseReason?)null),
            OnlineStoreState.ClosedToday when request.BusyMinutes is null => (EndOfServiceDay(now), OnlineStoreCloseReason.Closed),
            OnlineStoreState.ClosedUntil when request.BusyMinutes is { } minutes && BusyMinuteChoices.Contains(minutes) =>
                (now.AddMinutes(minutes), OnlineStoreCloseReason.Busy),
            _ => throw new InvalidStoreStatusRequestException("InvalidDuration"),
        };

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await WriteRequestAsync(connection, transaction, request.Provider, request.State, until, reason, actorId, now, cancellationToken)
            .ConfigureAwait(false);
        await AuditAsync(connection, transaction, request.Provider, request.State, until, actorId, now, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new OnlineStoreStatus(request.Provider, true, request.State, until, reason, false, false, null);
    }

    /// <summary>
    /// One pass: ends closures whose time has come, then tells each platform its pending request. Returns how many
    /// requests were delivered.
    /// </summary>
    public async Task<int> DeliverDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        await ReopenExpiredAsync(now, cancellationToken).ConfigureAwait(false);

        var delivered = 0;
        foreach (var (provider, channel) in _channels)
        {
            if (!channel.IsConfigured)
                continue;
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var claim = new NpgsqlCommand(
                """
                SELECT desired_state, closed_until, close_reason, requested_at, delivery_attempts
                FROM online_ordering.platform_store_status
                WHERE provider = $1 AND delivered_at IS NULL AND (next_attempt_at IS NULL OR next_attempt_at <= $2)
                FOR UPDATE SKIP LOCKED;
                """, connection, transaction);
            claim.Parameters.AddWithValue(provider);
            claim.Parameters.AddWithValue(now);
            OnlineStoreState state;
            DateTimeOffset? until;
            OnlineStoreCloseReason? reason;
            DateTimeOffset requestedAt;
            int attempts;
            await using (var reader = await claim.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    continue;
                state = Enum.Parse<OnlineStoreState>(reader.GetString(0));
                until = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1);
                reason = reader.IsDBNull(2) ? null : Enum.Parse<OnlineStoreCloseReason>(reader.GetString(2));
                requestedAt = reader.GetFieldValue<DateTimeOffset>(3);
                attempts = reader.GetInt32(4);
            }

            try
            {
                await channel.ApplyAsync(state, until, reason, cancellationToken).ConfigureAwait(false);
                await ExecuteAsync(connection, transaction,
                    """
                    UPDATE online_ordering.platform_store_status
                    SET delivered_at = $2, delivery_attempts = delivery_attempts + 1, last_error = NULL, next_attempt_at = NULL
                    WHERE provider = $1 AND requested_at = $3;
                    """, cancellationToken, provider, now, requestedAt).ConfigureAwait(false);
                delivered++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var wait = Math.Min(MaxBackoffSeconds, 15 * Math.Pow(2, attempts));
                await ExecuteAsync(connection, transaction,
                    """
                    UPDATE online_ordering.platform_store_status
                    SET delivery_attempts = delivery_attempts + 1, last_error = left($4, 200),
                        next_attempt_at = $2 + make_interval(secs => $5)
                    WHERE provider = $1 AND requested_at = $3;
                    """, cancellationToken, provider, now, requestedAt, ex.GetType().Name, wait).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return delivered;
    }

    /// <summary>Every platform with its request and, where it can be read, what the platform reports now.</summary>
    public async Task<IReadOnlyList<OnlineStoreStatus>> StatusAsync(CancellationToken cancellationToken = default)
    {
        var rows = new Dictionary<string, (OnlineStoreState, DateTimeOffset?, OnlineStoreCloseReason?, bool, bool)>(StringComparer.Ordinal);
        await using (var command = _dataSource.CreateCommand(
            "SELECT provider, desired_state, closed_until, close_reason, delivered_at IS NOT NULL, last_error IS NOT NULL FROM online_ordering.platform_store_status;"))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows[reader.GetString(0)] = (Enum.Parse<OnlineStoreState>(reader.GetString(1)),
                    reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                    reader.IsDBNull(3) ? null : Enum.Parse<OnlineStoreCloseReason>(reader.GetString(3)),
                    reader.GetBoolean(4), reader.GetBoolean(5));
            }
        }

        var result = new List<OnlineStoreStatus>();
        foreach (var provider in Providers)
        {
            var channel = _channels[provider];
            PlatformStoreState? platform = null;
            if (channel.IsConfigured)
            {
                try
                {
                    platform = await channel.ReadAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    // Unreadable right now: the screen says so rather than guessing.
                }
            }

            var (state, until, reason, delivered, failing) = rows.TryGetValue(provider, out var row)
                ? row
                : (OnlineStoreState.Open, null, null, true, false);
            result.Add(new OnlineStoreStatus(provider, channel.IsConfigured, state, until, reason, delivered, failing, platform));
        }

        return result;
    }

    private async Task ReopenExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT provider FROM online_ordering.platform_store_status WHERE desired_state <> 'Open' AND closed_until <= $1;");
        command.Parameters.AddWithValue(now);
        var expired = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                expired.Add(reader.GetString(0));
        }

        foreach (var provider in expired)
        {
            // A platform that ends a timed closure itself needs no call; the others are told to open.
            var reopensItself = _channels.TryGetValue(provider, out var channel) && channel.ReopensByItself;
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (var reopen = new NpgsqlCommand(
                """
                UPDATE online_ordering.platform_store_status
                SET desired_state = 'Open', closed_until = NULL, close_reason = NULL, requested_by = NULL, requested_at = $2,
                    delivered_at = CASE WHEN $3 THEN $2 END, delivery_attempts = 0, next_attempt_at = NULL, last_error = NULL
                WHERE provider = $1 AND desired_state <> 'Open' AND closed_until <= $2;
                """, connection, transaction))
            {
                reopen.Parameters.AddWithValue(provider);
                reopen.Parameters.AddWithValue(now);
                reopen.Parameters.AddWithValue(reopensItself);
                if (await reopen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1)
                    await AuditAsync(connection, transaction, provider, OnlineStoreState.Open, null, null, now, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task WriteRequestAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string provider, OnlineStoreState state, DateTimeOffset? until,
        OnlineStoreCloseReason? reason, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction,
            """
            INSERT INTO online_ordering.platform_store_status
                (provider, desired_state, closed_until, close_reason, requested_by, requested_at, delivered_at, delivery_attempts, next_attempt_at, last_error)
            VALUES ($1, $2, $3, $4, $5, $6, NULL, 0, NULL, NULL)
            ON CONFLICT (provider) DO UPDATE
                SET desired_state = EXCLUDED.desired_state, closed_until = EXCLUDED.closed_until, close_reason = EXCLUDED.close_reason,
                    requested_by = EXCLUDED.requested_by, requested_at = EXCLUDED.requested_at, delivered_at = NULL,
                    delivery_attempts = 0, next_attempt_at = NULL, last_error = NULL;
            """, cancellationToken, provider, state.ToString(), Nullable(until, NpgsqlDbType.TimestampTz),
            Nullable(reason?.ToString(), NpgsqlDbType.Text), actorId, now);

    private static Task AuditAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string provider, OnlineStoreState state, DateTimeOffset? until,
        Guid? actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var eventId = Guid.NewGuid();
        return ExecuteAsync(connection, transaction,
            """
            INSERT INTO audit.audit_events (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json, metadata_json, occurred_at
            ) VALUES ($1, $2, 'OnlinePlatform', $3, $4, $5, NULL, $6, NULL, NULL, NULL, $7::jsonb, $8);
            """, cancellationToken, eventId, AuditEventName, AggregateId(provider), Nullable(actorId, NpgsqlDbType.Uuid),
            actorId is null ? "System" : "User", eventId.ToString("D"),
            JsonSerializer.Serialize(new { provider, state = state.ToString(), closedUntil = until }), now);
    }

    /// <summary>The same stable platform aggregate the settings store audits under.</summary>
    private static Guid AggregateId(string provider) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("online-ordering.platform:" + provider)).AsSpan(0, 16));

    private static NpgsqlParameter Nullable(object? value, NpgsqlDbType type) => new() { NpgsqlDbType = type, Value = value ?? DBNull.Value };

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, params object[] values)
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
