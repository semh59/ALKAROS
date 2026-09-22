using System.Data;
using System.Data.Common;

namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// PostgreSQL-backed <see cref="IOffsiteBackupReceiptStore"/>
/// (<c>operations.offsite_backup_receipts</c>, migration 138). The
/// artifact id is the primary key, so a second <see cref="RecordAsync"/>
/// call for the same artifact raises a unique-violation rather than
/// silently overwriting an existing receipt.
/// </summary>
public sealed class PostgresOffsiteBackupReceiptStore : IOffsiteBackupReceiptStore
{
    private const string Table = "operations.offsite_backup_receipts";

    private readonly DbDataSource _dataSource;

    public PostgresOffsiteBackupReceiptStore(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task RecordAsync(OffsiteBackupReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        const string sql = $"""
            INSERT INTO {Table} (
                artifact_id, data_class, plaintext_checksum_sha256, encryption_key_name,
                encryption_key_version, plaintext_size_bytes, target_location, uploaded_at
            ) VALUES (
                @artifactId, @dataClass, @checksum, @keyName, @keyVersion, @size, @location, @uploadedAt
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "artifactId", receipt.ArtifactId);
        AddParameter(cmd, "dataClass", receipt.DataClass.ToString());
        AddParameter(cmd, "checksum", receipt.PlaintextChecksumSha256);
        AddParameter(cmd, "keyName", receipt.EncryptionKeyName);
        AddParameter(cmd, "keyVersion", receipt.EncryptionKeyVersion);
        AddParameter(cmd, "size", receipt.PlaintextSizeBytes);
        AddParameter(cmd, "location", receipt.TargetLocation);
        AddParameter(cmd, "uploadedAt", receipt.UploadedAtUtc);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OffsiteBackupReceipt>> GetAllAsync(int limit = 500, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT artifact_id, data_class, plaintext_checksum_sha256, encryption_key_name,
                   encryption_key_version, plaintext_size_bytes, target_location, uploaded_at
            FROM {Table}
            ORDER BY uploaded_at DESC
            LIMIT @limit;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "limit", limit);

        var results = new List<OffsiteBackupReceipt>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadReceipt(reader));

        return results;
    }

    public async Task<IReadOnlyList<OffsiteBackupReceipt>> GetByDataClassAsync(DataClass dataClass, int limit = 500, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT artifact_id, data_class, plaintext_checksum_sha256, encryption_key_name,
                   encryption_key_version, plaintext_size_bytes, target_location, uploaded_at
            FROM {Table}
            WHERE data_class = @dataClass
            ORDER BY uploaded_at DESC
            LIMIT @limit;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "dataClass", dataClass.ToString());
        AddParameter(cmd, "limit", limit);

        var results = new List<OffsiteBackupReceipt>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadReceipt(reader));

        return results;
    }

    private static OffsiteBackupReceipt ReadReceipt(DbDataReader reader) => new(
        reader.GetString(0),
        Enum.Parse<DataClass>(reader.GetString(1), ignoreCase: true),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4),
        reader.GetInt64(5),
        reader.GetString(6),
        reader.GetFieldValue<DateTimeOffset>(7));

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
