using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.HelpRequests;

/// <summary>One raised help request, as stored.</summary>
public sealed record HelpRequestRecord(
    Guid HelpRequestId,
    string TableNumber,
    string RequestedByDisplayName,
    DateTimeOffset CreatedAt);

/// <summary>
/// V1-WTR-014: persistence behind a help request. The 2-minute per-table
/// cooldown (Semih's decision, 2026-09-11 — protects management from a
/// panicked double-tap or a stuck finger flooding them with duplicate
/// alerts for the same table) lives here rather than in memory so it holds
/// across a restart and across more than one Host instance.
/// </summary>
public sealed class HelpRequestStore
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    private readonly NpgsqlDataSource _dataSource;
    private readonly ISettingsService _settings;

    public HelpRequestStore(NpgsqlDataSource dataSource, ISettingsService settings)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Raises a help request. Throws <see cref="HelpRequestCooldownActiveException"/>
    /// if this table's cooldown has not elapsed yet;
    /// <see cref="KeyNotFoundException"/> if the table does not exist;
    /// <see cref="GarsonFeatureDisabledException"/> (V1-SET-004) if this
    /// deployment turned the whole feature off — unlike the note/course/seat
    /// fields, there is no "just ignore it" reading of a help call.
    /// </summary>
    public async Task<HelpRequestRecord> RaiseAsync(
        Guid tableId,
        string requestType,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await GarsonFeatureToggles.IsEnabledAsync(_settings, GarsonFeature.HelpRequest, cancellationToken))
            throw new GarsonFeatureDisabledException(GarsonFeature.HelpRequest);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Found in an independent review (2026-09-11): the cooldown check
        // below (a SELECT) and the insert further down had nothing tying
        // them together, so two concurrent raises for the same table (a
        // genuine double-tap, or two devices) could both read "no recent
        // request" and both insert - exactly the double-tap/stuck-finger
        // scenario this cooldown's own doc comment says it exists to
        // protect against, defeated by the case least protected by a bare
        // check-then-insert. A transaction-scoped advisory lock keyed by
        // the table id serializes concurrent raises for THIS table only;
        // every other table's raise proceeds fully concurrently, and the
        // lock releases automatically on commit or rollback.
        await using (var tableLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext(@table_id)::bigint);", connection, transaction))
        {
            tableLock.Parameters.Add("table_id", NpgsqlDbType.Text).Value = tableId.ToString("N");
            await tableLock.ExecuteNonQueryAsync(cancellationToken);
        }

        string? tableNumber;
        await using (var lookup = new NpgsqlCommand(
            "SELECT table_number FROM table_mgmt.tables WHERE table_id = @table_id;", connection, transaction))
        {
            lookup.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            tableNumber = (string?)await lookup.ExecuteScalarAsync(cancellationToken);
        }
        if (tableNumber is null)
            throw new KeyNotFoundException($"Table '{tableId}' was not found.");

        // Only a request nobody has answered blocks a repeat: once a manager took the earlier call, a new
        // problem at the same table is a new call, and while it is open the repeat adds nothing.
        await using (var cooldownCheck = new NpgsqlCommand(
            """
            SELECT EXTRACT(EPOCH FROM (now() - created_at))::float8 FROM notifications.help_requests
            WHERE table_id = @table_id AND acknowledged_at IS NULL
            ORDER BY created_at DESC
            LIMIT 1;
            """, connection, transaction))
        {
            cooldownCheck.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            // Both ends of the age come from the database clock, the one that stamped created_at.
            if (await cooldownCheck.ExecuteScalarAsync(cancellationToken) is double elapsedSeconds)
            {
                var elapsed = TimeSpan.FromSeconds(elapsedSeconds);
                if (elapsed < Cooldown)
                    throw new HelpRequestCooldownActiveException(Cooldown - elapsed);
            }
        }

        var helpRequestId = Guid.NewGuid();
        HelpRequestRecord record;
        await using (var insert = new NpgsqlCommand(
            """
            WITH inserted AS (
                INSERT INTO notifications.help_requests
                    (help_request_id, table_id, table_number, requested_by_user_id, request_type)
                VALUES (@help_request_id, @table_id, @table_number, @requested_by_user_id, @request_type)
                RETURNING created_at
            )
            SELECT inserted.created_at, u.display_name
            FROM inserted, identity.users u
            WHERE u.user_id = @requested_by_user_id;
            """, connection, transaction))
        {
            insert.Parameters.Add("help_request_id", NpgsqlDbType.Uuid).Value = helpRequestId;
            insert.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            insert.Parameters.Add("table_number", NpgsqlDbType.Text).Value = tableNumber;
            insert.Parameters.Add("requested_by_user_id", NpgsqlDbType.Uuid).Value = requestedByUserId;
            insert.Parameters.Add("request_type", NpgsqlDbType.Text).Value = requestType;

            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var createdAt = reader.GetFieldValue<DateTimeOffset>(0);
            var requestedByDisplayName = reader.GetString(1);
            record = new HelpRequestRecord(helpRequestId, tableNumber, requestedByDisplayName, createdAt);
        }

        await transaction.CommitAsync(cancellationToken);
        return record;
    }

    /// <summary>Requests nobody has acknowledged yet (last 24 hours, oldest first).</summary>
    public async Task<IReadOnlyList<OpenHelpRequestV1>> ListOpenAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT h.help_request_id, h.table_id, h.table_number, h.request_type, u.display_name, h.created_at
            FROM notifications.help_requests h
            JOIN identity.users u ON u.user_id = h.requested_by_user_id
            WHERE h.acknowledged_at IS NULL AND h.created_at > now() - interval '24 hours'
            ORDER BY h.created_at
            LIMIT 100;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var open = new List<OpenHelpRequestV1>();
        while (await reader.ReadAsync(cancellationToken))
        {
            open.Add(new OpenHelpRequestV1(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return open;
    }

    /// <summary>
    /// Marks every open request of the same table as seen by <paramref name="managerUserId"/> (one manager
    /// taking the call answers all of them). Returns null for an unknown request; an empty requester list
    /// means it was already acknowledged, so repeating the call is harmless.
    /// </summary>
    public async Task<HelpRequestAcknowledgement?> AcknowledgeAsync(
        Guid helpRequestId, Guid managerUserId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            WITH target AS (
                SELECT table_id, table_number FROM notifications.help_requests WHERE help_request_id = @help_request_id
            ), updated AS (
                UPDATE notifications.help_requests h
                SET acknowledged_at = now(), acknowledged_by_user_id = @manager_user_id
                FROM target
                WHERE h.table_id = target.table_id AND h.acknowledged_at IS NULL
                RETURNING h.requested_by_user_id
            )
            SELECT target.table_number,
                   (SELECT display_name FROM identity.users WHERE user_id = @manager_user_id),
                   (SELECT array_agg(DISTINCT requested_by_user_id) FROM updated)
            FROM target;
            """);
        command.Parameters.Add("help_request_id", NpgsqlDbType.Uuid).Value = helpRequestId;
        command.Parameters.Add("manager_user_id", NpgsqlDbType.Uuid).Value = managerUserId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new HelpRequestAcknowledgement(
            reader.GetString(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? [] : reader.GetFieldValue<Guid[]>(2));
    }
}

/// <summary>Who has to be told that a call was taken, and by whom.</summary>
public sealed record HelpRequestAcknowledgement(
    string TableNumber,
    string AcknowledgedByDisplayName,
    IReadOnlyList<Guid> RequesterUserIds);
