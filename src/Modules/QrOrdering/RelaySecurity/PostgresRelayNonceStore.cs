using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrOrdering.RelaySecurity;

public sealed class PostgresRelayNonceStore : IRelayNonceStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresRelayNonceStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<bool> TryConsumeAsync(Guid tokenId, Guid nonce, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO qr_ordering.relay_request_nonces (token_id, nonce, used_at)
            VALUES (@token_id, @nonce, @used_at)
            ON CONFLICT (token_id, nonce) DO NOTHING;
            """);
        command.Parameters.Add("token_id", NpgsqlDbType.Uuid).Value = tokenId;
        command.Parameters.Add("nonce", NpgsqlDbType.Uuid).Value = nonce;
        command.Parameters.Add("used_at", NpgsqlDbType.TimestampTz).Value = usedAt;
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }
}
