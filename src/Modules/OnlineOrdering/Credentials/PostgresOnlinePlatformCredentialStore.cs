using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.OnlineOrdering.Credentials;

/// <summary>
/// V12-OUI-003. One row per platform in <c>online_ordering.platform_credentials</c>: non-secret fields as plain
/// JSON, secret fields sealed together in one AES-256-GCM envelope (same chain as the QNB and Token terminal
/// credential stores). A change is serialized per platform, merged into what is stored and audited in the same
/// transaction.
/// </summary>
public sealed class PostgresOnlinePlatformCredentialStore : IOnlinePlatformCredentialStore
{
    public const int MaxValueLength = 512;
    public const string AuditEventName = "OnlinePlatform.CredentialsChanged";

    private static readonly SecretReference MasterKey = new("envelope-master-key");

    private readonly NpgsqlDataSource _dataSource;
    private readonly SensitivePayloadProtector _protector;

    public PostgresOnlinePlatformCredentialStore(NpgsqlDataSource dataSource, ISecretProvider secretProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentNullException.ThrowIfNull(secretProvider);
        var policy = new OnlinePlatformCredentialAccessPolicy();
        _protector = new SensitivePayloadProtector(new AesGcmEnvelopeCipher(new SecretResolver(secretProvider, policy)), policy);
    }

    public async Task<OnlinePlatformCredentialStatus> GetStatusAsync(string provider, CancellationToken cancellationToken = default)
    {
        var platform = Require(provider);
        await using var command = _dataSource.CreateCommand(
            "SELECT plain_fields::text, secret_fields, updated_at FROM online_ordering.platform_credentials WHERE provider = $1;");
        command.Parameters.AddWithValue(platform.Provider);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return Status(platform, new Dictionary<string, string>(), [], null);

        return Status(
            platform,
            ReadPlain(reader.GetString(0)),
            reader.GetFieldValue<string[]>(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    public async Task<OnlinePlatformCredentialStatus> SaveAsync(
        SaveOnlinePlatformCredentialRequest request, Guid? actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var platform = Require(request.Provider);
        var values = Validate(platform, request);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // One change per platform at a time, including the very first one (no row to lock yet).
        await using (var gate = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext('online-ordering.platform-credentials:' || $1));", connection, transaction))
        {
            gate.Parameters.AddWithValue(platform.Provider);
            await gate.ExecuteNonQueryAsync(cancellationToken);
        }

        var plain = new Dictionary<string, string>(StringComparer.Ordinal);
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var read = new NpgsqlCommand(
            "SELECT plain_fields::text, envelope_bytes FROM online_ordering.platform_credentials WHERE provider = $1;",
            connection, transaction))
        {
            read.Parameters.AddWithValue(platform.Provider);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                plain = ReadPlain(reader.GetString(0));
                if (!reader.IsDBNull(1))
                    secrets = new Dictionary<string, string>(Open(reader.GetFieldValue<byte[]>(1)).Fields, StringComparer.Ordinal);
            }
        }

        var cleared = request.Cleared
            .Distinct(StringComparer.Ordinal)
            .Where(name => plain.Remove(name) | secrets.Remove(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
        foreach (var (name, value) in values)
        {
            if (platform.Field(name)!.IsSecret)
                secrets[name] = value;
            else
                plain[name] = value;
        }

        var set = values.Keys.Order(StringComparer.Ordinal).ToArray();
        if (set.Length == 0 && cleared.Length == 0)
            throw new InvalidOnlinePlatformCredentialException(OnlinePlatformCredentialProblem.NoChange, null);

        var now = DateTimeOffset.UtcNow;
        if (plain.Count == 0 && secrets.Count == 0)
        {
            await using var delete = new NpgsqlCommand(
                "DELETE FROM online_ordering.platform_credentials WHERE provider = $1;", connection, transaction);
            delete.Parameters.AddWithValue(platform.Provider);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            var secretNames = secrets.Keys.Order(StringComparer.Ordinal).ToArray();
            await using var upsert = new NpgsqlCommand(
                """
                INSERT INTO online_ordering.platform_credentials
                    (provider, plain_fields, secret_fields, envelope_bytes, updated_at, updated_by)
                VALUES ($1, $2::jsonb, $3, $4, $5, $6)
                ON CONFLICT (provider) DO UPDATE
                    SET plain_fields = EXCLUDED.plain_fields,
                        secret_fields = EXCLUDED.secret_fields,
                        envelope_bytes = EXCLUDED.envelope_bytes,
                        updated_at = EXCLUDED.updated_at,
                        updated_by = EXCLUDED.updated_by;
                """, connection, transaction);
            upsert.Parameters.AddWithValue(platform.Provider);
            upsert.Parameters.AddWithValue(JsonSerializer.Serialize(new SortedDictionary<string, string>(plain, StringComparer.Ordinal)));
            upsert.Parameters.AddWithValue(secretNames);
            upsert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bytea, Value = secrets.Count == 0 ? DBNull.Value : Seal(secrets) });
            upsert.Parameters.AddWithValue(now);
            upsert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)actor ?? DBNull.Value });
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var audit = new NpgsqlCommand(
            """
            INSERT INTO audit.audit_events (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json, metadata_json, occurred_at
            ) VALUES ($1, $2, 'OnlinePlatform', $3, $4, $5, NULL, $6, NULL, NULL, NULL, $7::jsonb, $8);
            """, connection, transaction))
        {
            var eventId = Guid.NewGuid();
            audit.Parameters.AddWithValue(eventId);
            audit.Parameters.AddWithValue(AuditEventName);
            audit.Parameters.AddWithValue(AggregateId(platform.Provider));
            audit.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)actor ?? DBNull.Value });
            audit.Parameters.AddWithValue(actor is null ? "System" : "User");
            audit.Parameters.AddWithValue(eventId.ToString("D"));
            // Field names only: no value, secret or not, ever reaches the audit trail.
            audit.Parameters.AddWithValue(JsonSerializer.Serialize(new { provider = platform.Provider, set, cleared }));
            audit.Parameters.AddWithValue(now);
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Status(platform, plain, secrets.Keys, now);
    }

    public string? ResolveValue(string provider, string field)
    {
        var platform = OnlinePlatformCredentialCatalog.Find(provider);
        if (platform?.Field(field) is not { } definition)
            return null;

        using var command = _dataSource.CreateCommand(
            definition.IsSecret
                ? "SELECT envelope_bytes FROM online_ordering.platform_credentials WHERE provider = $1 AND $2 = ANY(secret_fields);"
                : "SELECT plain_fields ->> $2 FROM online_ordering.platform_credentials WHERE provider = $1;");
        command.Parameters.AddWithValue(platform.Provider);
        command.Parameters.AddWithValue(definition.Name);
        var result = command.ExecuteScalar();
        return result switch
        {
            byte[] envelope => Open(envelope).Fields.GetValueOrDefault(definition.Name),
            string value => value,
            _ => null,
        };
    }

    /// <summary>One stable audit aggregate per platform (audit_events needs a UUID; a platform is named by text).</summary>
    public static Guid AggregateId(string provider)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("online-ordering.platform:" + provider));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static OnlinePlatformCredentialDefinition Require(string? provider) =>
        OnlinePlatformCredentialCatalog.Find(provider) ?? throw new UnknownOnlinePlatformException(provider ?? "");

    private static Dictionary<string, string> Validate(OnlinePlatformCredentialDefinition platform, SaveOnlinePlatformCredentialRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Values);
        ArgumentNullException.ThrowIfNull(request.Cleared);
        foreach (var name in request.Values.Keys.Concat(request.Cleared))
        {
            if (platform.Field(name) is null)
                throw new InvalidOnlinePlatformCredentialException(OnlinePlatformCredentialProblem.UnknownField, name);
        }

        if (request.Cleared.FirstOrDefault(request.Values.ContainsKey) is { } both)
            throw new InvalidOnlinePlatformCredentialException(OnlinePlatformCredentialProblem.SetAndCleared, both);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, raw) in request.Values)
        {
            // A pasted value often carries a trailing newline or space; a setting never needs one.
            var value = (raw ?? "").Trim();
            if (value.Length == 0)
                throw new InvalidOnlinePlatformCredentialException(OnlinePlatformCredentialProblem.Empty, name);
            if (value.Length > MaxValueLength)
                throw new InvalidOnlinePlatformCredentialException(OnlinePlatformCredentialProblem.TooLong, name);
            if (value.Any(char.IsControl))
                throw new InvalidOnlinePlatformCredentialException(OnlinePlatformCredentialProblem.InvalidCharacters, name);
            // Secrets travel to this address, so it must be an https URL without embedded credentials.
            if (platform.Field(name)!.IsUrl
                && !(Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0))
                throw new InvalidOnlinePlatformCredentialException(OnlinePlatformCredentialProblem.InvalidUrl, name);
            values[name] = value;
        }

        return values;
    }

    private static OnlinePlatformCredentialStatus Status(
        OnlinePlatformCredentialDefinition platform,
        Dictionary<string, string> plain,
        IEnumerable<string> secretNames,
        DateTimeOffset? updatedAt)
    {
        var secrets = secretNames.ToHashSet(StringComparer.Ordinal);
        return new OnlinePlatformCredentialStatus(
            platform.Provider,
            platform.Fields
                .Select(field => field.IsSecret
                    ? new OnlinePlatformCredentialFieldStatus(field.Name, true, secrets.Contains(field.Name), null)
                    : new OnlinePlatformCredentialFieldStatus(
                        field.Name, false, plain.ContainsKey(field.Name), plain.GetValueOrDefault(field.Name)))
                .ToList(),
            updatedAt);
    }

    private static Dictionary<string, string> ReadPlain(string json) =>
        new(JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [], StringComparer.Ordinal);

    private byte[] Seal(IReadOnlyDictionary<string, string> secrets)
    {
        var payload = new SensitivePayload(
            secrets.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            secrets.Keys.ToDictionary(name => name, _ => SensitiveCategory.Credential, StringComparer.Ordinal));
        return _protector.Protect(payload, MasterKey, OnlinePlatformCredentialAccessPolicy.Accessor).ToPersistenceBytes();
    }

    private SensitivePayload Open(byte[] envelope) =>
        _protector.Unprotect(SensitiveEnvelope.FromPersistenceBytes(envelope), MasterKey, OnlinePlatformCredentialAccessPolicy.Accessor);
}
