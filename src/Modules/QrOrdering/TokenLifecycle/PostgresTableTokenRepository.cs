using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrOrdering.TokenLifecycle;

public sealed class PostgresTableTokenRepository : ITableTokenRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTableTokenRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<TableToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT token_id, table_id, token_hash, issued_at, expires_at, revoked_at, revoked_reason
            FROM qr_ordering.table_tokens
            WHERE token_hash = @token_hash;
            """);
        command.Parameters.Add("token_hash", NpgsqlDbType.Text).Value = tokenHash;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<TableToken?> GetActiveForTableAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT token_id, table_id, token_hash, issued_at, expires_at, revoked_at, revoked_reason
            FROM qr_ordering.table_tokens
            WHERE table_id = @table_id AND revoked_at IS NULL;
            """);
        command.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task AddAsync(TableToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO qr_ordering.table_tokens
                (token_id, table_id, token_hash, issued_at, expires_at, revoked_at, revoked_reason)
            VALUES (@token_id, @table_id, @token_hash, @issued_at, @expires_at, @revoked_at, @revoked_reason);
            """);
        BindNewTokenParameters(command, token);
        command.Parameters.Add("revoked_at", NpgsqlDbType.TimestampTz).Value = (object?)token.RevokedAt ?? DBNull.Value;
        command.Parameters.Add("revoked_reason", NpgsqlDbType.Text).Value = (object?)token.RevokedReason ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RevokeAsync(Guid tokenId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE qr_ordering.table_tokens
            SET revoked_at = @revoked_at, revoked_reason = @revoked_reason
            WHERE token_id = @token_id AND revoked_at IS NULL;
            """);
        command.Parameters.Add("token_id", NpgsqlDbType.Uuid).Value = tokenId;
        command.Parameters.Add("revoked_at", NpgsqlDbType.TimestampTz).Value = revokedAt;
        command.Parameters.Add("revoked_reason", NpgsqlDbType.Text).Value = reason;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new TableTokenNotFoundException($"Token {tokenId} was not found or already revoked.");
    }

    public async Task RotateAsync(Guid previousTokenId, string revokedReason, TableToken newToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using (var revokeCommand = new NpgsqlCommand(
            """
            UPDATE qr_ordering.table_tokens
            SET revoked_at = @revoked_at, revoked_reason = @revoked_reason
            WHERE token_id = @token_id AND revoked_at IS NULL;
            """, connection, transaction))
        {
            revokeCommand.Parameters.Add("token_id", NpgsqlDbType.Uuid).Value = previousTokenId;
            revokeCommand.Parameters.Add("revoked_at", NpgsqlDbType.TimestampTz).Value = newToken.IssuedAt;
            revokeCommand.Parameters.Add("revoked_reason", NpgsqlDbType.Text).Value = revokedReason;
            var affected = await revokeCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
                throw new TableTokenNotFoundException($"Token {previousTokenId} was not found or already revoked.");
        }

        await using (var insertCommand = new NpgsqlCommand(
            """
            INSERT INTO qr_ordering.table_tokens
                (token_id, table_id, token_hash, issued_at, expires_at, revoked_at, revoked_reason)
            VALUES (@token_id, @table_id, @token_hash, @issued_at, @expires_at, NULL, NULL);
            """, connection, transaction))
        {
            BindNewTokenParameters(insertCommand, newToken);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Binds the five columns every insert always sets; revoked_at/revoked_reason are each caller's own concern.</summary>
    private static void BindNewTokenParameters(NpgsqlCommand command, TableToken token)
    {
        command.Parameters.Add("token_id", NpgsqlDbType.Uuid).Value = token.TokenId;
        command.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = token.TableId;
        command.Parameters.Add("token_hash", NpgsqlDbType.Text).Value = token.TokenHash;
        command.Parameters.Add("issued_at", NpgsqlDbType.TimestampTz).Value = token.IssuedAt;
        command.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value = token.ExpiresAt;
    }

    private static TableToken Map(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetFieldValue<DateTimeOffset>(3),
        reader.GetFieldValue<DateTimeOffset>(4),
        reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
        reader.IsDBNull(6) ? null : reader.GetString(6));
}
