namespace ALKAROS.Operations.BackupHealth;

/// <summary>
/// Domain record representing a local backup metadata entry (V1-OPS-002, PDF:III.25).
/// </summary>
public sealed record BackupRecord(
    Guid BackupId,
    BackupType BackupType,
    string FilePath,
    long FileSizeBytes,
    string ChecksumSha256,
    BackupStatus Status,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int RetentionDays,
    string? MetadataJson);

/// <summary>
/// Domain record representing a periodic system health snapshot (V1-OPS-002, PDF:II.2.23).
/// </summary>
public sealed record SystemHealthSnapshotRecord(
    Guid SnapshotId,
    SystemHealthStatus DatabaseStatus,
    SystemHealthStatus DiskStatus,
    SystemHealthStatus LastBackupStatus,
    long FreeDiskBytes,
    long DatabaseSizeBytes,
    DateTimeOffset CapturedAt,
    string? DetailsJson);

/// <summary>
/// Command to initiate a new backup (V1-OPS-002).
/// </summary>
public sealed record StartBackupCommand(
    BackupType BackupType,
    string DestinationDirectory,
    int RetentionDays = 30,
    string? MetadataJson = null);

/// <summary>
/// Result of an executed backup operation with checksum verification (V1-OPS-002).
/// </summary>
public sealed record BackupExecutionResult(
    Guid BackupId,
    BackupType BackupType,
    string FilePath,
    long FileSizeBytes,
    string ChecksumSha256,
    bool IsSuccess,
    string? ErrorMessage);
