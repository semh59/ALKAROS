using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Payments.Token.TerminalCredential;

/// <summary>
/// V13-HUG-005. Stores `client-secret` as an AES-256-GCM envelope
/// (`ALKAROS.SensitiveData`) — only ciphertext ever reaches
/// `payments.token_terminal_credentials`; mirrors
/// `ALKAROS.QrOrdering.RelayCredential.PostgresRelayCredentialStore`'s
/// exact pattern. `merchantId`/`branchId`/`terminalId`/`clientId` are
/// plaintext (not secrets, they identify the terminal). The envelope's
/// master key comes from the environment (`EnvironmentVariableSecretProvider`,
/// shared via DI — see `TokenTerminalCredentialAccessPolicy`'s own doc
/// comment for why the resolver/cipher/protector chain around it is
/// constructed privately here rather than reused from DI).
/// </summary>
public sealed class PostgresTokenTerminalCredentialStore : ITokenTerminalCredentialStore
{
    private const string CredentialKey = "token_terminal";
    private const string ClientSecretField = "client_secret";
    private static readonly SecretReference MasterKey = new("envelope-master-key");

    private readonly NpgsqlDataSource _dataSource;
    private readonly SensitivePayloadProtector _protector;

    public PostgresTokenTerminalCredentialStore(NpgsqlDataSource dataSource, ISecretProvider secretProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentNullException.ThrowIfNull(secretProvider);

        var policy = new TokenTerminalCredentialAccessPolicy();
        var resolver = new SecretResolver(secretProvider, policy);
        var cipher = new AesGcmEnvelopeCipher(resolver);
        _protector = new SensitivePayloadProtector(cipher, policy);
    }

    public async Task SaveAsync(SaveTokenTerminalCredentialRequest request, Guid? updatedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MerchantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BranchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TerminalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientSecret);

        var payload = new SensitivePayload(
            new Dictionary<string, string> { [ClientSecretField] = request.ClientSecret },
            new Dictionary<string, SensitiveCategory> { [ClientSecretField] = SensitiveCategory.Credential });
        var envelope = _protector.Protect(payload, MasterKey, TokenTerminalCredentialAccessPolicy.Accessor);
        var bytes = envelope.ToPersistenceBytes();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO payments.token_terminal_credentials
                (credential_key, merchant_id, branch_id, terminal_id, client_id, envelope_bytes, updated_at, updated_by)
            VALUES (@credential_key, @merchant_id, @branch_id, @terminal_id, @client_id, @envelope_bytes, @updated_at, @updated_by)
            ON CONFLICT (credential_key) DO UPDATE
                SET merchant_id = EXCLUDED.merchant_id,
                    branch_id = EXCLUDED.branch_id,
                    terminal_id = EXCLUDED.terminal_id,
                    client_id = EXCLUDED.client_id,
                    envelope_bytes = EXCLUDED.envelope_bytes,
                    updated_at = EXCLUDED.updated_at,
                    updated_by = EXCLUDED.updated_by;
            """);
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        command.Parameters.Add("merchant_id", NpgsqlDbType.Text).Value = request.MerchantId;
        command.Parameters.Add("branch_id", NpgsqlDbType.Text).Value = request.BranchId;
        command.Parameters.Add("terminal_id", NpgsqlDbType.Text).Value = request.TerminalId;
        command.Parameters.Add("client_id", NpgsqlDbType.Text).Value = request.ClientId;
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = bytes;
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        command.Parameters.Add("updated_by", NpgsqlDbType.Uuid).Value = (object?)updatedBy ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TokenTerminalCredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT merchant_id, branch_id, terminal_id, client_id, updated_at
            FROM payments.token_terminal_credentials
            WHERE credential_key = @credential_key;
            """);
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new TokenTerminalCredentialStatus(false, null, null, null, null, null);

        return new TokenTerminalCredentialStatus(
            Configured: true,
            UpdatedAt: reader.GetFieldValue<DateTimeOffset>(4),
            MerchantId: reader.GetFieldValue<string>(0),
            BranchId: reader.GetFieldValue<string>(1),
            TerminalId: reader.GetFieldValue<string>(2),
            ClientId: reader.GetFieldValue<string>(3));
    }

    public async Task<string?> ResolveClientSecretAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT envelope_bytes FROM payments.token_terminal_credentials WHERE credential_key = @credential_key;");
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not byte[] bytes)
            return null;

        var envelope = SensitiveEnvelope.FromPersistenceBytes(bytes);
        var payload = _protector.Unprotect(envelope, MasterKey, TokenTerminalCredentialAccessPolicy.Accessor);
        return payload.Fields[ClientSecretField];
    }
}
