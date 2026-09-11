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

    public HelpRequestStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <summary>
    /// Raises a help request. Throws <see cref="HelpRequestCooldownActiveException"/>
    /// if this table's cooldown has not elapsed yet;
    /// <see cref="KeyNotFoundException"/> if the table does not exist.
    /// </summary>
    public async Task<HelpRequestRecord> RaiseAsync(
        Guid tableId,
        string requestType,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        string? tableNumber;
        await using (var lookup = new NpgsqlCommand(
            "SELECT table_number FROM table_mgmt.tables WHERE table_id = @table_id;", connection))
        {
            lookup.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            tableNumber = (string?)await lookup.ExecuteScalarAsync(cancellationToken);
        }
        if (tableNumber is null)
            throw new KeyNotFoundException($"Table '{tableId}' was not found.");

        await using (var cooldownCheck = new NpgsqlCommand(
            """
            SELECT created_at FROM notifications.help_requests
            WHERE table_id = @table_id
            ORDER BY created_at DESC
            LIMIT 1;
            """, connection))
        {
            cooldownCheck.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            if (await cooldownCheck.ExecuteScalarAsync(cancellationToken) is DateTime lastRaisedAtUtc)
            {
                var elapsed = DateTime.UtcNow - DateTime.SpecifyKind(lastRaisedAtUtc, DateTimeKind.Utc);
                if (elapsed < Cooldown)
                    throw new HelpRequestCooldownActiveException(Cooldown - elapsed);
            }
        }

        var helpRequestId = Guid.NewGuid();
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
            """, connection))
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
            return new HelpRequestRecord(helpRequestId, tableNumber, requestedByDisplayName, createdAt);
        }
    }
}
