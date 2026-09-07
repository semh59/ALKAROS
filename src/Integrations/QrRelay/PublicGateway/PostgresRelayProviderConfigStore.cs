using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrRelay.PublicGateway;

public sealed class PostgresRelayProviderConfigStore : IRelayProviderConfigStore
{
    private const string ConfigKey = "cloudflare";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresRelayProviderConfigStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task SaveAsync(string accountId, string zoneId, string baseDomain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDomain);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO qr_ordering.relay_provider_config (config_key, account_id, zone_id, base_domain, updated_at)
            VALUES (@config_key, @account_id, @zone_id, @base_domain, @updated_at)
            ON CONFLICT (config_key) DO UPDATE
                SET account_id = EXCLUDED.account_id,
                    zone_id = EXCLUDED.zone_id,
                    base_domain = EXCLUDED.base_domain,
                    updated_at = EXCLUDED.updated_at;
            """);
        command.Parameters.Add("config_key", NpgsqlDbType.Text).Value = ConfigKey;
        command.Parameters.Add("account_id", NpgsqlDbType.Text).Value = accountId;
        command.Parameters.Add("zone_id", NpgsqlDbType.Text).Value = zoneId;
        command.Parameters.Add("base_domain", NpgsqlDbType.Text).Value = baseDomain;
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<RelayProviderConfig?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT account_id, zone_id, base_domain, updated_at FROM qr_ordering.relay_provider_config WHERE config_key = @config_key;");
        command.Parameters.Add("config_key", NpgsqlDbType.Text).Value = ConfigKey;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new RelayProviderConfig(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }
}
