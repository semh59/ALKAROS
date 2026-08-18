namespace ALKAROS.Operations.BackupHealth;

/// <summary>
/// Domain service interface for database backup operations and system health reporting (V1-OPS-002).
/// </summary>
public interface IBackupHealthService
{
    Task<BackupRecord> ExecuteBackupAsync(
        StartBackupCommand command,
        byte[] payload,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyBackupIntegrityAsync(
        Guid backupId,
        CancellationToken cancellationToken = default);

    Task<BackupRecord?> GetBackupByIdAsync(
        Guid backupId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupRecord>> GetRecentBackupsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<SystemHealthSnapshotRecord> CaptureSystemHealthSnapshotAsync(
        SystemHealthStatus databaseStatus,
        SystemHealthStatus diskStatus,
        long freeDiskBytes,
        long databaseSizeBytes,
        string? detailsJson = null,
        CancellationToken cancellationToken = default);

    Task<SystemHealthSnapshotRecord?> GetLatestHealthSnapshotAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain service implementation for Backup & Health Foundation (V1-OPS-002).
/// </summary>
public sealed class BackupHealthService : IBackupHealthService
{
    private readonly IBackupHealthRepository _repository;
    private readonly IBackupEngine _backupEngine;

    public BackupHealthService(
        IBackupHealthRepository repository,
        IBackupEngine backupEngine)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _backupEngine = backupEngine ?? throw new ArgumentNullException(nameof(backupEngine));
    }

    public async Task<BackupRecord> ExecuteBackupAsync(
        StartBackupCommand command,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(payload);

        var backupId = Guid.NewGuid();

        // 1. Initial InProgress record
        await _repository.InsertBackupRecordAsync(
            backupId,
            command.BackupType,
            string.Empty,
            0,
            string.Empty,
            BackupStatus.InProgress,
            command.RetentionDays,
            command.MetadataJson,
            cancellationToken);

        // 2. Execute local file creation and SHA-256 calculation
        var result = await _backupEngine.CreateBackupFileAsync(
            backupId,
            command.BackupType,
            command.DestinationDirectory,
            payload,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (result.IsSuccess)
        {
            // 3. Update to Completed with exact file path, size, and checksum
            await _repository.CompleteBackupAsync(
                backupId,
                result.FilePath,
                result.FileSizeBytes,
                result.ChecksumSha256,
                now,
                cancellationToken);

            return (await _repository.GetBackupByIdAsync(backupId, cancellationToken))!;
        }
        else
        {
            // 4. Update to Failed (Acceptance Evidence: source failure must be visible and never report success)
            await _repository.FailBackupAsync(
                backupId,
                result.ErrorMessage ?? "Unknown backup execution failure",
                now,
                cancellationToken);

            throw new BackupExecutionException(result.ErrorMessage ?? "Backup execution failed.");
        }
    }

    public async Task<bool> VerifyBackupIntegrityAsync(
        Guid backupId,
        CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetBackupByIdAsync(backupId, cancellationToken);
        if (record is null)
            throw new BackupNotFoundException(backupId);

        if (record.Status != BackupStatus.Completed)
            return false;

        return await _backupEngine.VerifyBackupChecksumAsync(record.FilePath, record.ChecksumSha256, cancellationToken);
    }

    public Task<BackupRecord?> GetBackupByIdAsync(Guid backupId, CancellationToken cancellationToken = default)
    {
        return _repository.GetBackupByIdAsync(backupId, cancellationToken);
    }

    public Task<IReadOnlyList<BackupRecord>> GetRecentBackupsAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        return _repository.GetRecentBackupsAsync(limit, cancellationToken);
    }

    public async Task<SystemHealthSnapshotRecord> CaptureSystemHealthSnapshotAsync(
        SystemHealthStatus databaseStatus,
        SystemHealthStatus diskStatus,
        long freeDiskBytes,
        long databaseSizeBytes,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        var recentBackups = await _repository.GetRecentBackupsAsync(1, cancellationToken);
        var lastBackupStatus = recentBackups.Count > 0 && recentBackups[0].Status == BackupStatus.Completed
            ? SystemHealthStatus.Healthy
            : (recentBackups.Count > 0 && recentBackups[0].Status == BackupStatus.Failed ? SystemHealthStatus.Unhealthy : SystemHealthStatus.Degraded);

        return await _repository.InsertHealthSnapshotAsync(
            databaseStatus,
            diskStatus,
            lastBackupStatus,
            freeDiskBytes,
            databaseSizeBytes,
            detailsJson,
            cancellationToken);
    }

    public Task<SystemHealthSnapshotRecord?> GetLatestHealthSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetLatestHealthSnapshotAsync(cancellationToken);
    }
}
