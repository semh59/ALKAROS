using System.Text;
using ALKAROS.Operations.OffsiteBackup;
using ALKAROS.Operations.RestoreVerification.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Operations.RestoreVerification.Tests;

public sealed class RestoreVerificationOrchestratorTests : IAsyncLifetime
{
    private readonly RestoreVerificationTestDatabase _database = new();
    private readonly string _offsiteDirectory = Path.Combine(Path.GetTempPath(), "alkaros-restore-drill-" + Guid.NewGuid().ToString("N"));
    private OffsiteBackupCipher _cipher = null!;
    private LocalDirectoryOffsiteBackupTarget _target = null!;
    private PostgresOffsiteBackupReceiptStore _receiptStore = null!;
    private PostgresRestoreAttemptStore _attemptStore = null!;
    private RecordingStructuredEventLogger _eventLogger = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _cipher = CipherFixtures.CreateCipher();
        _target = new LocalDirectoryOffsiteBackupTarget(_offsiteDirectory);
        _receiptStore = new PostgresOffsiteBackupReceiptStore(_database.DataSource);
        _attemptStore = new PostgresRestoreAttemptStore(_database.DataSource);
        _eventLogger = new RecordingStructuredEventLogger();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_offsiteDirectory))
            Directory.Delete(_offsiteDirectory, recursive: true);
    }

    [Fact]
    public async Task RunAsyncRestoresArtifactAndRecordsSuccessWithinRto()
    {
        var receipt = await SeedRealArtifactAsync(
            "drill-success-1",
            DataClass.OrdersInventory,
            "CREATE TABLE drill_probe (id INT PRIMARY KEY); INSERT INTO drill_probe (id) VALUES (1), (2), (3);");

        var orchestrator = BuildOrchestrator(out var databaseFactory, [
            new IntegrityCheck("drill_probe row count", "SELECT count(*) FROM drill_probe;", value => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 3),
        ]);

        var result = await orchestrator.RunAsync(DataClass.OrdersInventory);

        Assert.True(result.Succeeded);
        Assert.True(result.WithinRtoTarget);
        Assert.Equal(receipt.ArtifactId, result.ArtifactId);
        Assert.Equal(1, result.IntegrityChecksPassed);
        Assert.Equal(1, result.IntegrityChecksTotal);
        Assert.Null(result.FailureReason);
        Assert.Equal(1, databaseFactory.ProvisionCallCount);

        var history = await _attemptStore.GetByDataClassAsync(DataClass.OrdersInventory);
        Assert.Single(history);
        Assert.True(history[0].Succeeded);
    }

    [Fact]
    public async Task RunAsyncThrowsAndRecordsFailureWhenChecksumIsTampered()
    {
        // Upload the real, correctly-encrypted artifact to the target, but
        // never record its true receipt — only a tampered one, whose
        // checksum no longer matches the plaintext the target actually
        // holds. Simulates a receipt row that was corrupted after upload.
        var receipt = await UploadOnlyAsync(
            "drill-bad-checksum-1",
            DataClass.Settings,
            "CREATE TABLE drill_probe (id INT PRIMARY KEY);");

        var tampered = new OffsiteBackupReceipt(
            receipt.ArtifactId,
            receipt.DataClass,
            plaintextChecksumSha256: new string('0', 64),
            receipt.EncryptionKeyName,
            receipt.EncryptionKeyVersion,
            receipt.PlaintextSizeBytes,
            receipt.TargetLocation,
            receipt.UploadedAtUtc);

        var orchestrator = BuildOrchestrator(out var databaseFactory, [], seedReceipt: tampered);

        await Assert.ThrowsAsync<OffsiteBackupSourceIntegrityException>(() => orchestrator.RunAsync(DataClass.Settings));

        Assert.Equal(0, databaseFactory.ProvisionCallCount);
        var history = await _attemptStore.GetByDataClassAsync(DataClass.Settings);
        Assert.Single(history);
        Assert.False(history[0].Succeeded);
        Assert.NotNull(history[0].FailureReason);
    }

    [Fact]
    public async Task RunAsyncThrowsAndRecordsFailureWhenCiphertextIsCorrupted()
    {
        await SeedRealArtifactAsync(
            "drill-bad-ciphertext-1",
            DataClass.Fiscal,
            "CREATE TABLE drill_probe (id INT PRIMARY KEY);");

        // Flip a byte in the stored ciphertext file directly — AES-GCM's
        // authentication tag will no longer verify.
        var path = Directory.GetFiles(_offsiteDirectory, "drill-bad-ciphertext-1.enc").Single();
        var bytes = await File.ReadAllBytesAsync(path);
        bytes[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(path, bytes);

        var orchestrator = BuildOrchestrator(out var databaseFactory, []);

        await Assert.ThrowsAsync<OffsiteBackupDecryptionFailedException>(() => orchestrator.RunAsync(DataClass.Fiscal));

        Assert.Equal(0, databaseFactory.ProvisionCallCount);
        var history = await _attemptStore.GetByDataClassAsync(DataClass.Fiscal);
        Assert.Single(history);
        Assert.False(history[0].Succeeded);
    }

    [Fact]
    public async Task RunAsyncThrowsAndRecordsFailureWhenIntegrityCheckFails()
    {
        await SeedRealArtifactAsync(
            "drill-integrity-fail-1",
            DataClass.OrdersInventory,
            "CREATE TABLE drill_probe (id INT PRIMARY KEY);"); // no rows inserted

        var orchestrator = BuildOrchestrator(out _, [
            new IntegrityCheck("drill_probe has rows", "SELECT count(*) FROM drill_probe;", value => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) > 0),
        ]);

        await Assert.ThrowsAsync<RestoreIntegrityCheckFailedException>(() => orchestrator.RunAsync(DataClass.OrdersInventory));

        var history = await _attemptStore.GetByDataClassAsync(DataClass.OrdersInventory);
        Assert.Single(history);
        Assert.False(history[0].Succeeded);
    }

    [Fact]
    public async Task RunAsyncRecordsGenuinePassedCountWhenALaterIntegrityCheckFails()
    {
        // Regression test: 4 of 5 checks genuinely pass before the 5th
        // throws. The recorded attempt must report integrityChecksPassed
        // == 4 (the real count reached before the failure), not the
        // misleading fixed 0 that a `passed` counter scoped only to the
        // try block used to produce once control jumped to the catch.
        await SeedRealArtifactAsync(
            "drill-partial-pass-1",
            DataClass.OrdersInventory,
            "CREATE TABLE drill_probe (id INT PRIMARY KEY); INSERT INTO drill_probe (id) VALUES (1), (2), (3);");

        var orchestrator = BuildOrchestrator(out _, [
            new IntegrityCheck("check 1 (passes)", "SELECT count(*) FROM drill_probe;", value => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 3),
            new IntegrityCheck("check 2 (passes)", "SELECT 1;", value => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 1),
            new IntegrityCheck("check 3 (passes)", "SELECT 2;", value => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 2),
            new IntegrityCheck("check 4 (passes)", "SELECT 3;", value => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 3),
            new IntegrityCheck("check 5 (fails)", "SELECT 4;", value => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 999),
        ]);

        await Assert.ThrowsAsync<RestoreIntegrityCheckFailedException>(() => orchestrator.RunAsync(DataClass.OrdersInventory));

        var history = await _attemptStore.GetByDataClassAsync(DataClass.OrdersInventory);
        Assert.Single(history);
        Assert.False(history[0].Succeeded);
        Assert.Equal(4, history[0].IntegrityChecksPassed);
        Assert.Equal(5, history[0].IntegrityChecksTotal);
    }

    [Fact]
    public async Task RunAsyncThrowsWhenNoArtifactIsRecorded()
    {
        var orchestrator = BuildOrchestrator(out _, []);

        await Assert.ThrowsAsync<RestoreArtifactNotFoundException>(() => orchestrator.RunAsync(DataClass.Fiscal));
    }

    [Fact]
    public async Task AttemptStoreReturnsNewestFirst()
    {
        var older = new RestoreAttemptRecord("art-1", DataClass.Settings, DateTimeOffset.UtcNow.AddMinutes(-10), TimeSpan.FromSeconds(1), true, true, 1, 1, null);
        var newer = new RestoreAttemptRecord("art-2", DataClass.Settings, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), true, true, 1, 1, null);

        await _attemptStore.RecordAsync(older);
        await _attemptStore.RecordAsync(newer);

        var history = await _attemptStore.GetByDataClassAsync(DataClass.Settings);

        Assert.Equal(2, history.Count);
        Assert.Equal("art-2", history[0].ArtifactId);
        Assert.Equal("art-1", history[1].ArtifactId);
    }

    private async Task<OffsiteBackupReceipt> SeedRealArtifactAsync(string artifactId, DataClass dataClass, string sqlScript)
    {
        var receipt = await UploadOnlyAsync(artifactId, dataClass, sqlScript);
        await _receiptStore.RecordAsync(receipt);
        return receipt;
    }

    /// <summary>Uploads a real, correctly-encrypted artifact to the target but never records a receipt for it — the caller controls what (if anything) gets recorded.</summary>
    private async Task<OffsiteBackupReceipt> UploadOnlyAsync(string artifactId, DataClass dataClass, string sqlScript)
    {
        var localPath = Path.Combine(Path.GetTempPath(), artifactId + ".sql");
        var plaintext = Encoding.UTF8.GetBytes(sqlScript);
        await File.WriteAllBytesAsync(localPath, plaintext);
        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(plaintext)).ToLowerInvariant();

        var uploadService = new OffsiteBackupUploadService(_target, _cipher, _eventLogger);
        var reference = new BackupArtifactReference(artifactId, dataClass, localPath, checksum, DateTimeOffset.UtcNow);
        var receipt = await uploadService.UploadAsync(reference);

        File.Delete(localPath);
        return receipt;
    }

    private RestoreVerificationOrchestrator BuildOrchestrator(
        out CountingIsolatedRestoreDatabaseFactory databaseFactory,
        IReadOnlyList<IntegrityCheck> integrityChecks,
        OffsiteBackupReceipt? seedReceipt = null)
    {
        if (seedReceipt is not null)
            _receiptStore.RecordAsync(seedReceipt).GetAwaiter().GetResult();

        databaseFactory = new CountingIsolatedRestoreDatabaseFactory(
            new NpgsqlIsolatedRestoreDatabaseFactory(RestoreVerificationTestDatabase.MaintenanceConnectionString()));

        var downloadVerify = new OffsiteBackupRestoreVerificationService(_target, _cipher);
        return new RestoreVerificationOrchestrator(_receiptStore, downloadVerify, databaseFactory, _attemptStore, integrityChecks);
    }
}
