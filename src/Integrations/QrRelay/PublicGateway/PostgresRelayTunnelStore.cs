using ALKAROS.QrOrdering.RelayCredential;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrRelay.PublicGateway;

/// <summary>
/// V12-QRT-001. The tunnel run-token is encrypted the same way the
/// Cloudflare API token is (`ALKAROS.QrOrdering.RelayCredential.
/// PostgresRelayCredentialStore`) — same envelope master key, same accessor
/// identity, since both sit behind the identical trust boundary (only the
/// relay settings backend, never an HTTP response, ever decrypts either).
/// </summary>
public sealed class PostgresRelayTunnelStore : IRelayTunnelStore
{
    private const string TunnelKey = "cloudflare";
    private const string TokenField = "tunnel_token";
    private static readonly SecretReference MasterKey = new("envelope-master-key");

    private readonly NpgsqlDataSource _dataSource;
    private readonly SensitivePayloadProtector _protector;

    public PostgresRelayTunnelStore(NpgsqlDataSource dataSource, SensitivePayloadProtector protector)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task SaveAsync(string tunnelId, string tunnelToken, string hostname, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        var payload = new SensitivePayload(
            new Dictionary<string, string> { [TokenField] = tunnelToken },
            new Dictionary<string, SensitiveCategory> { [TokenField] = SensitiveCategory.Credential });
        var envelope = _protector.Protect(payload, MasterKey, RelayCredentialAccessPolicy.Accessor);
        var bytes = envelope.ToPersistenceBytes();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO qr_ordering.relay_tunnel (tunnel_key, tunnel_id, hostname, token_envelope, updated_at)
            VALUES (@tunnel_key, @tunnel_id, @hostname, @token_envelope, @updated_at)
            ON CONFLICT (tunnel_key) DO UPDATE
                SET tunnel_id = EXCLUDED.tunnel_id,
                    hostname = EXCLUDED.hostname,
                    token_envelope = EXCLUDED.token_envelope,
                    updated_at = EXCLUDED.updated_at;
            """);
        command.Parameters.Add("tunnel_key", NpgsqlDbType.Text).Value = TunnelKey;
        command.Parameters.Add("tunnel_id", NpgsqlDbType.Text).Value = tunnelId;
        command.Parameters.Add("hostname", NpgsqlDbType.Text).Value = hostname;
        command.Parameters.Add("token_envelope", NpgsqlDbType.Bytea).Value = bytes;
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<RelayTunnelInfo?> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT tunnel_id, hostname, updated_at FROM qr_ordering.relay_tunnel WHERE tunnel_key = @tunnel_key;");
        command.Parameters.Add("tunnel_key", NpgsqlDbType.Text).Value = TunnelKey;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new RelayTunnelInfo(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    public async Task<string?> ResolveTunnelTokenAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT token_envelope FROM qr_ordering.relay_tunnel WHERE tunnel_key = @tunnel_key;");
        command.Parameters.Add("tunnel_key", NpgsqlDbType.Text).Value = TunnelKey;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not byte[] bytes)
            return null;

        var envelope = SensitiveEnvelope.FromPersistenceBytes(bytes);
        var payload = _protector.Unprotect(envelope, MasterKey, RelayCredentialAccessPolicy.Accessor);
        return payload.Fields[TokenField];
    }
}
