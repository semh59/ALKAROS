using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Identity.DeviceSessions;

public sealed class PostgresDeviceSessionRepository : IDeviceSessionRepository
{
    private const string Sessions = "identity.device_sessions";
    private const string Operations = "identity.session_operations";

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

    public async Task AddProcessedOperationsAsync(Guid sessionId, IReadOnlyList<PendingOperation> operations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0)
            return;

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Operations} (operation_id, session_id, queued_at)
            VALUES (@operation_id, @session_id, @queued_at)
            ON CONFLICT (operation_id) DO NOTHING;
            """);
        command.Parameters.Add("operation_id", NpgsqlDbType.Uuid);
        command.Parameters.Add("session_id", NpgsqlDbType.Uuid);
        command.Parameters.Add("queued_at", NpgsqlDbType.TimestampTz);

        foreach (var operation in operations)
        {
            command.Parameters["operation_id"].Value = operation.OperationId;
            command.Parameters["session_id"].Value = sessionId;
            command.Parameters["queued_at"].Value = operation.QueuedAt;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetProcessedOperationIdsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Guid>();

        await using var command = _dataSource.CreateCommand(
            $"SELECT operation_id FROM {Operations};");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0));

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