using ALKAROS.SensitiveData;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// Postgres-backed <see cref="IRetentionSubjectStore"/> against
/// <c>security.retention_subjects</c> (migration 137, V15-SEC-003).
/// </summary>
public sealed class PostgresRetentionSubjectStore : IRetentionSubjectStore
{
    /// <summary>
    /// The sentinel envelope an Anonymize disposal overwrites a subject's
    /// real envelope with. Its key id never matches a real
    /// <c>SecretReference</c>, so <see cref="SensitivePayloadProtector.Unprotect"/>
    /// always fails closed against it — the plaintext is unrecoverable.
    /// </summary>
    private static readonly SensitiveEnvelope AnonymizedSentinel = new(
        new Dictionary<string, SensitiveCategory>(),
        new EnvelopeCiphertext("anonymized", [0], [], [0]),
        DateTimeOffset.UnixEpoch);

    private readonly NpgsqlDataSource _dataSource;

    public PostgresRetentionSubjectStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> InsertAsync(
        DataCategory category,
        SensitiveEnvelope envelope,
        bool legalHold,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var id = Guid.NewGuid();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO security.retention_subjects
                (id, data_category, envelope_bytes, created_at, legal_hold, row_version)
            VALUES (@id, @data_category, @envelope_bytes, @created_at, @legal_hold, 1);
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        command.Parameters.Add("data_category", NpgsqlDbType.Text).Value = category.ToString();
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = envelope.ToPersistenceBytes();
        command.Parameters.Add("created_at", NpgsqlDbType.TimestampTz).Value = createdAt;
        command.Parameters.Add("legal_hold", NpgsqlDbType.Boolean).Value = legalHold;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return id;
    }

    public async Task<RetentionSubjectRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, data_category, envelope_bytes, created_at, legal_hold,
                   disposed_at, disposal_action, row_version
            FROM security.retention_subjects
            WHERE id = @id;
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    public async Task<IReadOnlyList<RetentionSubjectRecord>> GetPendingAsync(CancellationToken cancellationToken, int limit = 1000)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, data_category, envelope_bytes, created_at, legal_hold,
                   disposed_at, disposal_action, row_version
            FROM security.retention_subjects
            WHERE disposed_at IS NULL
            ORDER BY created_at ASC
            LIMIT @limit;
            """);
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<RetentionSubjectRecord>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadRecord(reader));
        return results;
    }

    public async Task<IReadOnlyList<Guid>> GetDeletionQueueAsync(CancellationToken cancellationToken, int limit = 1000)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id FROM security.retention_subjects
            WHERE disposal_action = 'Delete' AND disposed_at IS NOT NULL
            ORDER BY disposed_at ASC
            LIMIT @limit;
            """);
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<Guid>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(reader.GetFieldValue<Guid>(0));
        return results;
    }

    public async Task MarkDisposedAsync(
        Guid id,
        DisposalAction action,
        int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var envelopeBytes = action == DisposalAction.Anonymize
            ? AnonymizedSentinel.ToPersistenceBytes()
            : (byte[]?)null;

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE security.retention_subjects
            SET disposed_at = @disposed_at,
                disposal_action = @disposal_action,
                envelope_bytes = COALESCE(@envelope_bytes, envelope_bytes),
                row_version = row_version + 1
            WHERE id = @id AND row_version = @expected_row_version AND disposed_at IS NULL;
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        command.Parameters.Add("disposed_at", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        command.Parameters.Add("disposal_action", NpgsqlDbType.Text).Value = action.ToString();
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = (object?)envelopeBytes ?? DBNull.Value;
        command.Parameters.Add("expected_row_version", NpgsqlDbType.Integer).Value = expectedRowVersion;

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new RetentionConcurrencyException(id);
    }

    public async Task ReplaceEnvelopeAsync(
        Guid id,
        SensitiveEnvelope envelope,
        int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE security.retention_subjects
            SET envelope_bytes = @envelope_bytes,
                row_version = row_version + 1
            WHERE id = @id AND row_version = @expected_row_version AND disposed_at IS NULL;
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        command.Parameters.Add("envelope_bytes", NpgsqlDbType.Bytea).Value = envelope.ToPersistenceBytes();
        command.Parameters.Add("expected_row_version", NpgsqlDbType.Integer).Value = expectedRowVersion;

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new RetentionConcurrencyException(id);
    }

    public async Task SetLegalHoldAsync(
        Guid id,
        bool legalHold,
        int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE security.retention_subjects
            SET legal_hold = @legal_hold,
                row_version = row_version + 1
            WHERE id = @id AND row_version = @expected_row_version;
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        command.Parameters.Add("legal_hold", NpgsqlDbType.Boolean).Value = legalHold;
        command.Parameters.Add("expected_row_version", NpgsqlDbType.Integer).Value = expectedRowVersion;

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new RetentionConcurrencyException(id);
    }

    public async Task PurgeAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "DELETE FROM security.retention_subjects WHERE id = @id;");
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static RetentionSubjectRecord ReadRecord(NpgsqlDataReader reader)
    {
        var id = reader.GetFieldValue<Guid>(0);

        var rawCategory = reader.GetString(1);
        if (!Enum.TryParse<DataCategory>(rawCategory, out var category))
            throw new RetentionSubjectCorruptDataException(id, nameof(DataCategory), rawCategory);

        var envelope = SensitiveEnvelope.FromPersistenceBytes((byte[])reader[2]);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(3);
        var legalHold = reader.GetFieldValue<bool>(4);
        var disposedAt = reader.IsDBNull(5) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(5);

        DisposalAction? action = null;
        if (!reader.IsDBNull(6))
        {
            var rawAction = reader.GetString(6);
            if (!Enum.TryParse<DisposalAction>(rawAction, out var parsedAction))
                throw new RetentionSubjectCorruptDataException(id, nameof(DisposalAction), rawAction);
            action = parsedAction;
        }

        var rowVersion = reader.GetFieldValue<int>(7);

        return new RetentionSubjectRecord(id, category, envelope, createdAt, legalHold, disposedAt, action, rowVersion);
    }
}
