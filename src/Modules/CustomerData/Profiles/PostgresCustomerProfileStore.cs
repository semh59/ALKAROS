using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// Postgres-backed <see cref="ICustomerProfileStore"/> against
/// <c>customer_data.profiles</c> (migration 159, V14-CST-001). name/phone/
/// email/address and the tax identity (V1-RMD-453) are protected together as a single AES-256-GCM envelope
/// (<see cref="ALKAROS.SensitiveData.SensitivePayloadProtector"/>,
/// <see cref="SensitiveCategory.Pii"/>) - mirrors
/// `ALKAROS.Security.DataProtectionRetention.PostgresRetentionSubjectStore`'s
/// exact anonymize-with-a-non-decryptable-sentinel pattern.
/// </summary>
public sealed class PostgresCustomerProfileStore : ICustomerProfileStore
{
    private const string NameField = "name";
    private const string PhoneField = "phone";
    private const string EmailField = "email";
    private const string AddressField = "address";
    private const string TaxIdKindField = "tax_id_kind";
    private const string TaxIdNumberField = "tax_id_number";
    private const string TaxOfficeField = "tax_office";
    private static readonly SecretReference MasterKey = new("envelope-master-key");

    /// <summary>
    /// The sentinel envelope an Anonymize disposal overwrites a profile's
    /// real envelope with. Its key id never matches <see cref="MasterKey"/>,
    /// so <see cref="SensitivePayloadProtector.Unprotect"/> always fails
    /// closed against it - the plaintext is unrecoverable.
    /// </summary>
    private static readonly SensitiveEnvelope AnonymizedSentinel = new(
        new Dictionary<string, SensitiveCategory>(),
        new EnvelopeCiphertext("anonymized", [0], [], [0]),
        DateTimeOffset.UnixEpoch);

    private readonly NpgsqlDataSource _dataSource;
    private readonly SensitivePayloadProtector _protector;

    public PostgresCustomerProfileStore(NpgsqlDataSource dataSource, ISecretProvider secretProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentNullException.ThrowIfNull(secretProvider);

        var policy = new CustomerProfileEncryptionPolicy();
        var resolver = new SecretResolver(secretProvider, policy);
        var cipher = new AesGcmEnvelopeCipher(resolver);
        _protector = new SensitivePayloadProtector(cipher, policy);
    }

    public async Task<Guid> CreateAsync(CreateCustomerProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = Guid.NewGuid();
        var envelope = ProtectContact(request.Name, request.Phone, request.Email, request.Address, request.TaxIdentity);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at, anonymized, row_version)
            VALUES (@customer_id, @envelope_bytes, @created_at, FALSE, 1);
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = id;
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = envelope.ToPersistenceBytes();
        command.Parameters.Add("created_at", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return id;
    }

    public async Task<CustomerProfile?> GetAsync(Guid customerId, CustomerAccessRole role, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT envelope_bytes, created_at, anonymized, row_version
            FROM customer_data.profiles
            WHERE customer_id = @customer_id;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var anonymized = reader.GetFieldValue<bool>(2);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(1);
        var rowVersion = reader.GetFieldValue<int>(3);

        string? name = null, phone = null, email = null, address = null;
        CustomerTaxIdentity? taxIdentity = null;
        if (!anonymized)
        {
            var envelope = SensitiveEnvelope.FromPersistenceBytes((byte[])reader[0]);
            var payload = _protector.Unprotect(envelope, MasterKey, CustomerProfileEncryptionPolicy.Accessor);
            payload.Fields.TryGetValue(NameField, out name);
            payload.Fields.TryGetValue(PhoneField, out phone);
            payload.Fields.TryGetValue(EmailField, out email);
            payload.Fields.TryGetValue(AddressField, out address);
            taxIdentity = ReadTaxIdentity(payload.Fields);
        }

        var profile = new CustomerProfile(customerId, name, phone, email, address, createdAt, anonymized, rowVersion, taxIdentity);
        return CustomerProfileAccessPolicy.Project(profile, role);
    }

    public async Task UpdateContactAsync(
        Guid customerId,
        UpdateCustomerContactRequest request,
        int expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var envelope = ProtectContact(request.Name, request.Phone, request.Email, request.Address, request.TaxIdentity);

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE customer_data.profiles
            SET envelope_bytes = @envelope_bytes,
                row_version = row_version + 1
            WHERE customer_id = @customer_id AND row_version = @expected_row_version AND anonymized = FALSE;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = envelope.ToPersistenceBytes();
        command.Parameters.Add("expected_row_version", NpgsqlDbType.Integer).Value = expectedRowVersion;

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows > 0)
            return;

        await ThrowForFailedMutationAsync(customerId, expectedRowVersion, cancellationToken);
    }

    public async Task AnonymizeAsync(Guid customerId, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE customer_data.profiles
            SET anonymized = TRUE,
                envelope_bytes = @envelope_bytes,
                row_version = row_version + 1
            WHERE customer_id = @customer_id AND row_version = @expected_row_version AND anonymized = FALSE;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = AnonymizedSentinel.ToPersistenceBytes();
        command.Parameters.Add("expected_row_version", NpgsqlDbType.Integer).Value = expectedRowVersion;

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows > 0)
            return;

        // Idempotent: a profile that is ALREADY anonymized (by this call
        // racing itself, e.g. a retention sweep retried after a timeout)
        // must not fail - the desired end state is already true.
        await using var check = _dataSource.CreateCommand(
            "SELECT anonymized FROM customer_data.profiles WHERE customer_id = @customer_id;");
        check.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        var result = await check.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new CustomerProfileNotFoundException(customerId);
        if ((bool)result)
            return;

        throw new CustomerProfileConcurrencyException(customerId);
    }

    private async Task ThrowForFailedMutationAsync(Guid customerId, int expectedRowVersion, CancellationToken cancellationToken)
    {
        await using var check = _dataSource.CreateCommand(
            "SELECT anonymized FROM customer_data.profiles WHERE customer_id = @customer_id;");
        check.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        var result = await check.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new CustomerProfileNotFoundException(customerId);
        if ((bool)result)
            throw new CustomerProfileAnonymizedException(customerId);

        throw new CustomerProfileConcurrencyException(customerId);
    }

    /// <summary>Profiles written before V1-RMD-453 carry no tax fields and read back without a tax identity.</summary>
    private static CustomerTaxIdentity? ReadTaxIdentity(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue(TaxIdKindField, out var kind) || !fields.TryGetValue(TaxIdNumberField, out var number))
            return null;
        fields.TryGetValue(TaxOfficeField, out var office);
        return CustomerTaxIdentity.Restore(Enum.Parse<CustomerTaxIdKind>(kind), number, office);
    }

    private SensitiveEnvelope ProtectContact(
        string? name, string? phone, string? email, string? address, CustomerTaxIdentity? taxIdentity)
    {
        if (taxIdentity is { IsMasked: true })
            throw new ArgumentException("A masked tax identity cannot be stored.", nameof(taxIdentity));

        var fields = new Dictionary<string, string>();
        var categories = new Dictionary<string, SensitiveCategory>();
        AddIfPresent(fields, categories, NameField, name);
        AddIfPresent(fields, categories, PhoneField, phone);
        AddIfPresent(fields, categories, EmailField, email);
        AddIfPresent(fields, categories, AddressField, address);
        AddIfPresent(fields, categories, TaxIdKindField, taxIdentity?.Kind.ToString());
        AddIfPresent(fields, categories, TaxIdNumberField, taxIdentity?.Number);
        AddIfPresent(fields, categories, TaxOfficeField, taxIdentity?.TaxOffice);

        var payload = new SensitivePayload(fields, categories);
        return _protector.Protect(payload, MasterKey, CustomerProfileEncryptionPolicy.Accessor);
    }

    private static void AddIfPresent(
        Dictionary<string, string> fields,
        Dictionary<string, SensitiveCategory> categories,
        string key,
        string? value)
    {
        if (value is null)
            return;
        fields[key] = value;
        categories[key] = SensitiveCategory.Pii;
    }
}
