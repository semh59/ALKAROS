using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrOrdering.RelayCredential;

/// <summary>
/// V14-QRT-003. Stores the relay provider's API token (e.g. a Cloudflare
/// Tunnel API token) as an AES-256-GCM envelope (`ALKAROS.SensitiveData`) —
/// only ciphertext ever reaches `qr_ordering.relay_credentials`. The
/// envelope's master key comes from the environment
/// (`EnvironmentVariableSecretProvider`), never from settings or the
/// database, per `ISecretProvider`'s own contract.
/// </summary>
public sealed class PostgresRelayCredentialStore : IRelayCredentialStore
{
    private const string CredentialKey = "cloudflare_api_token";
    private const string TokenField = "cloudflare_api_token";
    private static readonly SecretReference MasterKey = new("envelope-master-key");

    private readonly NpgsqlDataSource _dataSource;
    private readonly SensitivePayloadProtector _protector;

    public PostgresRelayCredentialStore(NpgsqlDataSource dataSource, SensitivePayloadProtector protector)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task SaveCloudflareApiTokenAsync(string rawToken, Guid? updatedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var payload = new SensitivePayload(
            new Dictionary<string, string> { [TokenField] = rawToken },
            new Dictionary<string, SensitiveCategory> { [TokenField] = SensitiveCategory.Credential });
        var envelope = _protector.Protect(payload, MasterKey, RelayCredentialAccessPolicy.Accessor);
        var bytes = envelope.ToPersistenceBytes();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO qr_ordering.relay_credentials (credential_key, envelope_bytes, updated_at, updated_by)
            VALUES (@credential_key, @envelope_bytes, @updated_at, @updated_by)
            ON CONFLICT (credential_key) DO UPDATE
                SET envelope_bytes = EXCLUDED.envelope_bytes,
                    updated_at = EXCLUDED.updated_at,
                    updated_by = EXCLUDED.updated_by;
            """);
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = bytes;
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        command.Parameters.Add("updated_by", NpgsqlDbType.Uuid).Value = (object?)updatedBy ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<RelayCredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT updated_at FROM qr_ordering.relay_credentials WHERE credential_key = @credential_key;");
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new RelayCredentialStatus(false, null);

        return new RelayCredentialStatus(true, reader.GetFieldValue<DateTimeOffset>(0));
    }

    public async Task<string?> ResolveCloudflareApiTokenAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT envelope_bytes FROM qr_ordering.relay_credentials WHERE credential_key = @credential_key;");
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not byte[] bytes)
            return null;

        var envelope = SensitiveEnvelope.FromPersistenceBytes(bytes);
        var payload = _protector.Unprotect(envelope, MasterKey, RelayCredentialAccessPolicy.Accessor);
        return payload.Fields[TokenField];
    }
}
