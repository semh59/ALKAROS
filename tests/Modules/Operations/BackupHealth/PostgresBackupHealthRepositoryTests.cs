using ALKAROS.Operations.BackupHealth.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Operations.BackupHealth.Tests;

[Collection(nameof(BackupHealthTestFixtureDefinition))]
public sealed class PostgresBackupHealthRepositoryTests : IClassFixture<BackupHealthTestDatabase>, IDisposable
{
    private readonly BackupHealthTestDatabase _db;
    private readonly string _tempDirectory;
    private readonly LocalBackupEngine _engine;
    private readonly PostgresBackupHealthRepository _repository;
    private readonly BackupHealthService _service;

    public PostgresBackupHealthRepositoryTests(BackupHealthTestDatabase db)
    {
        _db = db;
        _tempDirectory = Path.Combine(Path.GetTempPath(), "alkaros_repo_test_" + Guid.NewGuid().ToString("N"));
        _engine = new LocalBackupEngine();
        _repository = new PostgresBackupHealthRepository(_db.DataSource);
        _service = new BackupHealthService(_repository, _engine);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignored in test cleanup
            }
        }
    }

    [Fact]
    public async Task ExecuteBackupSuccessfullyAndVerifyDatabaseRecord()
    {
        var command = new StartBackupCommand(
            BackupType: BackupType.Full,
            DestinationDirectory: _tempDirectory,
            RetentionDays: 14,
            MetadataJson: "{\"triggered_by\":\"nightly_cron\"}");

        var payload = "REAL_POSTGRES_DUMP_STREAM_SIMULATION"u8.ToArray();

        var record = await _service.ExecuteBackupAsync(command, payload);

        record.Should().NotBeNull();
        record.Status.Should().Be(BackupStatus.Completed);
        record.BackupType.Should().Be(BackupType.Full);
        record.FileSizeBytes.Should().Be(payload.Length);
        record.RetentionDays.Should().Be(14);
        record.ChecksumSha256.Should().NotBeNullOrWhiteSpace();

        // Verify from repository directly
        var direct = await _service.GetBackupByIdAsync(record.BackupId);
        direct.Should().NotBeNull();
        direct!.Status.Should().Be(BackupStatus.Completed);

        // Verify integrity
        var isIntegrityValid = await _service.VerifyBackupIntegrityAsync(record.BackupId);
        isIntegrityValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteBackupFailureRecordedAndThrowsException()
    {
        var command = new StartBackupCommand(
            BackupType: BackupType.Incremental,
            DestinationDirectory: "Z:\\invalid_unreachable_disk_drive\\",
            RetentionDays: 30);

        var payload = "FAILING_PAYLOAD"u8.ToArray();

        var act = () => _service.ExecuteBackupAsync(command, payload);

        await act.Should().ThrowAsync<BackupExecutionException>();

        // Check that recent backups show the failed status
        var recent = await _service.GetRecentBackupsAsync(1);
        recent.Should().NotBeEmpty();
        recent[0].Status.Should().Be(BackupStatus.Failed);
        recent[0].ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CaptureSystemHealthSnapshotAndRetrieveLatest()
    {
        var snapshot = await _service.CaptureSystemHealthSnapshotAsync(
            databaseStatus: SystemHealthStatus.Healthy,
            diskStatus: SystemHealthStatus.Healthy,
            freeDiskBytes: 1024L * 1024L * 1024L * 50L,
            databaseSizeBytes: 1024L * 1024L * 500L,
            detailsJson: "{\"active_connections\": 8}");

        snapshot.Should().NotBeNull();
        snapshot.DatabaseStatus.Should().Be(SystemHealthStatus.Healthy);
        snapshot.DiskStatus.Should().Be(SystemHealthStatus.Healthy);

        var latest = await _service.GetLatestHealthSnapshotAsync();
        latest.Should().NotBeNull();
        latest!.SnapshotId.Should().Be(snapshot.SnapshotId);
        latest.FreeDiskBytes.Should().Be(1024L * 1024L * 1024L * 50L);
    }
}

public sealed class PostgresBackupHealthMigrationTests : IClassFixture<BackupHealthTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBackupHealthMigrationTests(BackupHealthTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "029-backup-health.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "029-backup-health.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('operations.backups')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be(DBNull.Value);
            }
        }

        // 2. Run up.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(upSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables recreated
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('operations.backups')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be("operations.backups");
            }
        }
    }
}

[CollectionDefinition(nameof(BackupHealthTestFixtureDefinition), DisableParallelization = true)]
public sealed class BackupHealthTestFixtureDefinition : ICollectionFixture<BackupHealthTestDatabase>
{
}
