using System.Data;
using System.Data.Common;

namespace ALKAROS.Operations.BackupHealth;

/// <summary>
/// Repository interface for backup history and system health snapshot persistence (V1-OPS-002).
/// </summary>
public interface IBackupHealthRepository
{
    Task<BackupRecord> InsertBackupRecordAsync(
        Guid backupId,
        BackupType backupType,
        string filePath,
        long fileSizeBytes,
        string checksumSha256,
        BackupStatus status,
        int retentionDays = 30,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);

    Task CompleteBackupAsync(
        Guid backupId,
        string filePath,
        long fileSizeBytes,
        string checksumSha256,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default);

    Task FailBackupAsync(
        Guid backupId,
        string errorMessage,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default);

    Task<BackupRecord?> GetBackupByIdAsync(
        Guid backupId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupRecord>> GetRecentBackupsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<SystemHealthSnapshotRecord> InsertHealthSnapshotAsync(
        SystemHealthStatus databaseStatus,
        SystemHealthStatus diskStatus,
        SystemHealthStatus lastBackupStatus,
        long freeDiskBytes,
        long databaseSizeBytes,
        string? detailsJson = null,
        CancellationToken cancellationToken = default);

    Task<SystemHealthSnapshotRecord?> GetLatestHealthSnapshotAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL implementation of <see cref="IBackupHealthRepository"/> (V1-OPS-002).
/// </summary>
public sealed class PostgresBackupHealthRepository : IBackupHealthRepository
{
    private const string BackupsTable = "operations.backups";
    private const string SnapshotsTable = "operations.system_health_snapshots";

    private readonly DbDataSource _dataSource;

    public PostgresBackupHealthRepository(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<BackupRecord> InsertBackupRecordAsync(
        Guid backupId,
        BackupType backupType,
        string filePath,
        long fileSizeBytes,
        string checksumSha256,
        BackupStatus status,
        int retentionDays = 30,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        DateTimeOffset? completedAt = status == BackupStatus.Completed ? startedAt : null;

        const string sql = $"""
            INSERT INTO {BackupsTable} (
                backup_id, backup_type, file_path, file_size_bytes, checksum_sha256, status, started_at, completed_at, retention_days, metadata
            ) VALUES (
                @id, @type, @path, @size, @checksum, @status, @started, @completed, @retention, @meta::jsonb
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", backupId);
        AddParameter(cmd, "type", backupType.ToString());
        AddParameter(cmd, "path", filePath);
        AddParameter(cmd, "size", fileSizeBytes);
        AddParameter(cmd, "checksum", checksumSha256);
        AddParameter(cmd, "status", status.ToString());
        AddParameter(cmd, "started", startedAt);
        AddParameter(cmd, "completed", (object?)completedAt ?? DBNull.Value);
        AddParameter(cmd, "retention", retentionDays);
        AddParameter(cmd, "meta", (object?)metadataJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new BackupRecord(
            backupId,
            backupType,
            filePath,
            fileSizeBytes,
            checksumSha256,
            status,
            null,
            startedAt,
            completedAt,
            retentionDays,
            metadataJson);
    }

    public async Task CompleteBackupAsync(
        Guid backupId,
        string filePath,
        long fileSizeBytes,
        string checksumSha256,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            UPDATE {BackupsTable}
            SET status = 'Completed', file_path = @path, file_size_bytes = @size, checksum_sha256 = @checksum, completed_at = @completed, error_message = NULL
            WHERE backup_id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", backupId);
        AddParameter(cmd, "path", filePath);
        AddParameter(cmd, "size", fileSizeBytes);
        AddParameter(cmd, "checksum", checksumSha256);
        AddParameter(cmd, "completed", completedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task FailBackupAsync(
        Guid backupId,
        string errorMessage,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            UPDATE {BackupsTable}
            SET status = 'Failed', error_message = @error, completed_at = @completed
            WHERE backup_id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", backupId);
        AddParameter(cmd, "error", errorMessage);
        AddParameter(cmd, "completed", completedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BackupRecord?> GetBackupByIdAsync(
        Guid backupId,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT backup_id, backup_type, file_path, file_size_bytes, checksum_sha256, status, error_message, started_at, completed_at, retention_days, metadata::text
            FROM {BackupsTable}
            WHERE backup_id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", backupId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadBackupRecord(reader);
    }

    public async Task<IReadOnlyList<BackupRecord>> GetRecentBackupsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT backup_id, backup_type, file_path, file_size_bytes, checksum_sha256, status, error_message, started_at, completed_at, retention_days, metadata::text
            FROM {BackupsTable}
            ORDER BY started_at DESC
            LIMIT @limit;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "limit", limit);

        var list = new List<BackupRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadBackupRecord(reader));
        }

        return list;
    }

    public async Task<SystemHealthSnapshotRecord> InsertHealthSnapshotAsync(
        SystemHealthStatus databaseStatus,
        SystemHealthStatus diskStatus,
        SystemHealthStatus lastBackupStatus,
        long freeDiskBytes,
        long databaseSizeBytes,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        var snapshotId = Guid.NewGuid();
        var capturedAt = DateTimeOffset.UtcNow;

        const string sql = $"""
            INSERT INTO {SnapshotsTable} (
                snapshot_id, database_status, disk_status, last_backup_status, free_disk_bytes, database_size_bytes, captured_at, details
            ) VALUES (
                @id, @db, @disk, @bkp, @free, @dbsize, @captured, @details::jsonb
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", snapshotId);
        AddParameter(cmd, "db", databaseStatus.ToString());
        AddParameter(cmd, "disk", diskStatus.ToString());
        AddParameter(cmd, "bkp", lastBackupStatus.ToString());
        AddParameter(cmd, "free", freeDiskBytes);
        AddParameter(cmd, "dbsize", databaseSizeBytes);
        AddParameter(cmd, "captured", capturedAt);
        AddParameter(cmd, "details", (object?)detailsJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new SystemHealthSnapshotRecord(
            snapshotId,
            databaseStatus,
            diskStatus,
            lastBackupStatus,
            freeDiskBytes,
            databaseSizeBytes,
            capturedAt,
            detailsJson);
    }

    public async Task<SystemHealthSnapshotRecord?> GetLatestHealthSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT snapshot_id, database_status, disk_status, last_backup_status, free_disk_bytes, database_size_bytes, captured_at, details::text
            FROM {SnapshotsTable}
            ORDER BY captured_at DESC
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new SystemHealthSnapshotRecord(
            reader.GetGuid(0),
            Enum.Parse<SystemHealthStatus>(reader.GetString(1), ignoreCase: true),
            Enum.Parse<SystemHealthStatus>(reader.GetString(2), ignoreCase: true),
            Enum.Parse<SystemHealthStatus>(reader.GetString(3), ignoreCase: true),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetString(7));
    }

    private static BackupRecord ReadBackupRecord(DbDataReader reader)
    {
        return new BackupRecord(
            reader.GetGuid(0),
            Enum.Parse<BackupType>(reader.GetString(1), ignoreCase: true),
            reader.GetString(2),
            reader.GetInt64(3),
            reader.GetString(4),
            Enum.Parse<BackupStatus>(reader.GetString(5), ignoreCase: true),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetString(10));
    }

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
