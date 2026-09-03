using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Identity.DeviceSessions;

public sealed class PostgresDeviceSessionRepository : IDeviceSessionRepository
{
    private const string Sessions = "identity.device_sessions";
    private const string Operations = "identity.session_operations";

    // Defensive ceiling for an unpaged read: hitting it means the operations
    // ledger outgrew the "recent, bounded" assumption and the caller needs an
    // existence check (WHERE operation_id = ANY(...)), not the whole set.
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresDeviceSessionRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task CreateAsync(DeviceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Sessions}
                (session_id, user_id, device_id, token_hash, created_at, expires_at, revoked_at, last_seen_at)
            VALUES
                (@session_id, @user_id, @device_id, @token_hash, @created_at, @expires_at, @revoked_at, @last_seen_at);
            """);
        command.Parameters.AddWithValue("session_id", session.SessionId);
        command.Parameters.AddWithValue("user_id", session.UserId);
        command.Parameters.AddWithValue("device_id", session.DeviceId);
        command.Parameters.AddWithValue("token_hash", session.TokenHash);
        command.Parameters.AddWithValue("created_at", session.CreatedAt);
        command.Parameters.AddWithValue("expires_at", session.ExpiresAt);
        command.Parameters.AddWithValue("revoked_at", (object?)session.RevokedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("last_seen_at", (object?)session.LastSeenAt ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DeviceSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT session_id, user_id, device_id, token_hash, created_at, expires_at, revoked_at, last_seen_at
            FROM {Sessions}
            WHERE token_hash = @token_hash;
            """);
        command.Parameters.AddWithValue("token_hash", tokenHash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadSession(reader);
    }

    public async Task UpdateLastSeenAsync(Guid sessionId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Sessions}
            SET last_seen_at = @last_seen_at
            WHERE session_id = @session_id AND revoked_at IS NULL;
            """);
        command.Parameters.AddWithValue("session_id", sessionId);
        command.Parameters.AddWithValue("last_seen_at", lastSeenAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(Guid sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Sessions}
            SET revoked_at = @revoked_at
            WHERE session_id = @session_id AND revoked_at IS NULL;
            """);
        command.Parameters.AddWithValue("session_id", sessionId);
        command.Parameters.AddWithValue("revoked_at", revokedAt);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<int> RevokeForDeviceAsync(Guid userId, string deviceId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Sessions}
            SET revoked_at = @revoked_at
            WHERE user_id = @user_id AND device_id = @device_id AND revoked_at IS NULL;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("device_id", deviceId);
        command.Parameters.AddWithValue("revoked_at", revokedAt);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ReconnectClaimResult> ClaimReconnectOperationsAsync(
        string tokenHash,
        Guid userId,
        string deviceId,
        IReadOnlyList<PendingOperation> operations,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenHash);
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        ArgumentNullException.ThrowIfNull(operations);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        DeviceSession? session;
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText =
                $"""
                SELECT session_id, user_id, device_id, token_hash, created_at, expires_at, revoked_at, last_seen_at
                FROM {Sessions}
                WHERE token_hash = @token_hash
                FOR UPDATE;
                """;
            lockCommand.Parameters.AddWithValue("token_hash", tokenHash);

            await using var reader = await lockCommand.ExecuteReaderAsync(cancellationToken);
            session = await reader.ReadAsync(cancellationToken) ? ReadSession(reader) : null;
        }

        if (session is null || session.UserId != userId || session.DeviceId != deviceId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ReconnectClaimResult(ReconnectClaimStatus.InvalidSession, null, []);
        }

        if (session.RevokedAt is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ReconnectClaimResult(ReconnectClaimStatus.Revoked, session, []);
        }

        if (session.ExpiresAt <= utcNow)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ReconnectClaimResult(ReconnectClaimStatus.Expired, session, []);
        }

        var inserted = new List<Guid>();
        if (operations.Count > 0)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                $"""
                INSERT INTO {Operations} (operation_id, session_id, queued_at)
                SELECT operation_id, @session_id, queued_at
                FROM unnest(@operation_ids::uuid[], @queued_at_values::timestamptz[])
                    AS pending(operation_id, queued_at)
                ON CONFLICT (operation_id) DO NOTHING
                RETURNING operation_id;
                """;
            insertCommand.Parameters.AddWithValue("session_id", session.SessionId);
            insertCommand.Parameters.AddWithValue(
                "operation_ids",
                NpgsqlDbType.Array | NpgsqlDbType.Uuid,
                operations.Select(operation => operation.OperationId).ToArray());
            insertCommand.Parameters.AddWithValue(
                "queued_at_values",
                NpgsqlDbType.Array | NpgsqlDbType.TimestampTz,
                operations.Select(operation => operation.QueuedAt).ToArray());

            await using var reader = await insertCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                inserted.Add(reader.GetGuid(0));
        }

        await transaction.CommitAsync(cancellationToken);
        return new ReconnectClaimResult(ReconnectClaimStatus.Success, session, inserted);
    }

    public async Task<IReadOnlyList<Guid>> GetProcessedOperationIdsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Guid>();

        await using var command = _dataSource.CreateCommand(
            $"SELECT operation_id FROM {Operations} LIMIT {MaxUnpagedRows + 1};");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0));

        if (result.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"{Operations} holds more than {MaxUnpagedRows} rows; GetProcessedOperationIdsAsync must become an existence check.");

        return result;
    }

    private static DeviceSession ReadSession(NpgsqlDataReader reader)
    {
        return new DeviceSession(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDateTime(4),
            reader.GetDateTime(5),
            reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            reader.IsDBNull(7) ? null : reader.GetDateTime(7));
    }
}
