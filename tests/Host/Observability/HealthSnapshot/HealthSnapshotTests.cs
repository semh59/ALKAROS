using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Operations.BackupHealth;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.HealthSnapshot.Tests;

public sealed class HealthDatabase : PgTestDatabase
{
    public HealthDatabase() : base("alkaros_rmd555_") { }

    protected override Task ApplySqlAsync() => Task.CompletedTask;
}

public sealed class HealthSnapshotTests : IClassFixture<HealthDatabase>
{
    private readonly HealthDatabase _database;

    public HealthSnapshotTests(HealthDatabase database) => _database = database;

    private sealed class RecordingService : IBackupHealthService
    {
        public List<(SystemHealthStatus Db, SystemHealthStatus Disk, long Free, long Size, string? Details)> Captures { get; } = [];

        public Task<SystemHealthSnapshotRecord> CaptureSystemHealthSnapshotAsync(
            SystemHealthStatus databaseStatus, SystemHealthStatus diskStatus, long freeDiskBytes, long databaseSizeBytes,
            string? detailsJson = null, CancellationToken cancellationToken = default)
        {
            Captures.Add((databaseStatus, diskStatus, freeDiskBytes, databaseSizeBytes, detailsJson));
            return Task.FromResult<SystemHealthSnapshotRecord>(null!);
        }

        public Task<BackupRecord> ExecuteBackupAsync(StartBackupCommand command, byte[] payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> VerifyBackupIntegrityAsync(Guid backupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BackupRecord?> GetBackupByIdAsync(Guid backupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BackupRecord>> GetRecentBackupsAsync(int limit = 10, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemHealthSnapshotRecord?> GetLatestHealthSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private HealthProbe Probe(DiskUsage usage) => new(new MonitoringDataSource(_database.DataSource), () => usage);

    [Theory]
    [InlineData(50, 100, SystemHealthStatus.Healthy)]
    [InlineData(9, 100, SystemHealthStatus.Degraded)]
    [InlineData(4, 100, SystemHealthStatus.Unhealthy)]
    [InlineData(0, 0, SystemHealthStatus.Unhealthy)]
    public void DiskIsClassifiedByFreeRatio(long free, long total, SystemHealthStatus expected) =>
        HealthProbe.ClassifyDisk(new DiskUsage(free, total)).Should().Be(expected);

    [Fact]
    public async Task CaptureWritesRealDatabaseSizeAndDiskValues()
    {
        var service = new RecordingService();
        var scopes = new ServiceCollection().AddSingleton<IBackupHealthService>(service).BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var job = new HealthSnapshotJob(scopes, Probe(new DiskUsage(30_000, 100_000)));

        await job.CaptureOnceAsync(CancellationToken.None);

        var capture = service.Captures.Should().ContainSingle().Subject;
        capture.Db.Should().Be(SystemHealthStatus.Healthy);
        capture.Disk.Should().Be(SystemHealthStatus.Healthy);
        capture.Free.Should().Be(30_000);
        capture.Size.Should().BeGreaterThan(0);
        capture.Details.Should().Contain("100000");
    }

    [Fact]
    public async Task UnreachableDatabaseIsReportedUnhealthyWithZeroSize()
    {
        await using var broken = Npgsql.NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=2;Pooling=false");
        var probe = new HealthProbe(new MonitoringDataSource(broken), () => new DiskUsage(30_000, 100_000));

        var result = await probe.CollectAsync(CancellationToken.None);

        result.Database.Should().Be(SystemHealthStatus.Unhealthy);
        result.DatabaseSize.Should().Be(0);
    }

    [Fact]
    public void DefaultDiskReportsRealNonZeroValues()
    {
        var usage = HealthProbe.DefaultDisk();

        usage.FreeBytes.Should().BeGreaterThan(0);
        usage.TotalBytes.Should().BeGreaterThan(usage.FreeBytes);
    }
}
