using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Invoicing.Qnb.CredentialRegistration;

/// <summary>
/// V14-QNB-006. Stores `password` as an AES-256-GCM envelope
/// (`ALKAROS.SensitiveData`) — only ciphertext ever reaches
/// `invoicing.qnb_credentials`; mirrors
/// `ALKAROS.Payments.Token.TerminalCredential.PostgresTokenTerminalCredentialStore`'s
/// exact pattern. `userId`/`vergiTcKimlikNo` are plaintext (not secrets).
/// </summary>
public sealed class PostgresQnbCredentialStore : IQnbCredentialStore
{
    private const string CredentialKey = "qnb_efatura";
    private const string PasswordField = "password";
    private static readonly SecretReference MasterKey = new("envelope-master-key");

    private readonly NpgsqlDataSource _dataSource;
    private readonly SensitivePayloadProtector _protector;

    public PostgresQnbCredentialStore(NpgsqlDataSource dataSource, ISecretProvider secretProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentNullException.ThrowIfNull(secretProvider);

        var policy = new QnbCredentialAccessPolicy();
        var resolver = new SecretResolver(secretProvider, policy);
        var cipher = new AesGcmEnvelopeCipher(resolver);
        _protector = new SensitivePayloadProtector(cipher, policy);
    }

    public async Task SaveAsync(SaveQnbCredentialRequest request, Guid? updatedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VergiTcKimlikNo);

        var payload = new SensitivePayload(
            new Dictionary<string, string> { [PasswordField] = request.Password },
            new Dictionary<string, SensitiveCategory> { [PasswordField] = SensitiveCategory.Credential });
        var envelope = _protector.Protect(payload, MasterKey, QnbCredentialAccessPolicy.Accessor);
        var bytes = envelope.ToPersistenceBytes();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO invoicing.qnb_credentials
                (credential_key, user_id, vergi_tc_kimlik_no, envelope_bytes, updated_at, updated_by)
            VALUES (@credential_key, @user_id, @vergi_tc_kimlik_no, @envelope_bytes, @updated_at, @updated_by)
            ON CONFLICT (credential_key) DO UPDATE
                SET user_id = EXCLUDED.user_id,
                    vergi_tc_kimlik_no = EXCLUDED.vergi_tc_kimlik_no,
                    envelope_bytes = EXCLUDED.envelope_bytes,
                    updated_at = EXCLUDED.updated_at,
                    updated_by = EXCLUDED.updated_by;
            """);
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        command.Parameters.Add("user_id", NpgsqlDbType.Text).Value = request.UserId;
        command.Parameters.Add("vergi_tc_kimlik_no", NpgsqlDbType.Text).Value = request.VergiTcKimlikNo;
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = bytes;
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        command.Parameters.Add("updated_by", NpgsqlDbType.Uuid).Value = (object?)updatedBy ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<QnbCredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT user_id, vergi_tc_kimlik_no, updated_at
            FROM invoicing.qnb_credentials
            WHERE credential_key = @credential_key;
            """);
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new QnbCredentialStatus(false, null, null, null);

        return new QnbCredentialStatus(
            Configured: true,
            UpdatedAt: reader.GetFieldValue<DateTimeOffset>(2),
            UserId: reader.GetFieldValue<string>(0),
            VergiTcKimlikNo: reader.GetFieldValue<string>(1));
    }

    public async Task<string?> ResolvePasswordAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT envelope_bytes FROM invoicing.qnb_credentials WHERE credential_key = @credential_key;");
        command.Parameters.Add("credential_key", NpgsqlDbType.Text).Value = CredentialKey;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not byte[] bytes)
            return null;

        var envelope = SensitiveEnvelope.FromPersistenceBytes(bytes);
        var payload = _protector.Unprotect(envelope, MasterKey, QnbCredentialAccessPolicy.Accessor);
        return payload.Fields[PasswordField];
    }
}
