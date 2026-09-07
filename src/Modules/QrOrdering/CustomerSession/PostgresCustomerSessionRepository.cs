using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrOrdering.CustomerSession;

public sealed class PostgresCustomerSessionRepository : ICustomerSessionRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCustomerSessionRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CustomerSession?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT session_id, table_id, token_hash, issued_at, last_activity_at, expires_at, revoked_at, revoked_reason
            FROM qr_ordering.customer_sessions
            WHERE token_hash = @token_hash;
            """);
        command.Parameters.Add("token_hash", NpgsqlDbType.Text).Value = tokenHash;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task AddAsync(CustomerSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO qr_ordering.customer_sessions
                (session_id, table_id, token_hash, issued_at, last_activity_at, expires_at, revoked_at, revoked_reason)
            VALUES (@session_id, @table_id, @token_hash, @issued_at, @last_activity_at, @expires_at, @revoked_at, @revoked_reason);
            """);
        command.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = session.SessionId;
        command.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = session.TableId;
        command.Parameters.Add("token_hash", NpgsqlDbType.Text).Value = session.TokenHash;
        command.Parameters.Add("issued_at", NpgsqlDbType.TimestampTz).Value = session.IssuedAt;
        command.Parameters.Add("last_activity_at", NpgsqlDbType.TimestampTz).Value = session.LastActivityAt;
        command.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value = session.ExpiresAt;
        command.Parameters.Add("revoked_at", NpgsqlDbType.TimestampTz).Value = (object?)session.RevokedAt ?? DBNull.Value;
        command.Parameters.Add("revoked_reason", NpgsqlDbType.Text).Value = (object?)session.RevokedReason ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task TouchActivityAsync(Guid sessionId, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE qr_ordering.customer_sessions
            SET last_activity_at = @at
            WHERE session_id = @session_id AND revoked_at IS NULL AND last_activity_at < @at;
            """);
        command.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = sessionId;
        command.Parameters.Add("at", NpgsqlDbType.TimestampTz).Value = at;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RevokeAsync(Guid sessionId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE qr_ordering.customer_sessions
            SET revoked_at = @revoked_at, revoked_reason = @revoked_reason
            WHERE session_id = @session_id AND revoked_at IS NULL;
            """);
        command.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = sessionId;
        command.Parameters.Add("revoked_at", NpgsqlDbType.TimestampTz).Value = revokedAt;
        command.Parameters.Add("revoked_reason", NpgsqlDbType.Text).Value = reason;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new CustomerSessionNotFoundException(sessionId);
    }

    private static CustomerSession Map(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetFieldValue<DateTimeOffset>(3),
        reader.GetFieldValue<DateTimeOffset>(4),
        reader.GetFieldValue<DateTimeOffset>(5),
        reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
        reader.IsDBNull(7) ? null : reader.GetString(7));
}
