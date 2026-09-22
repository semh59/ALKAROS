using System.Security.Cryptography;
using ALKAROS.Observability.StructuredLogging;
using ALKAROS.Operations.OffsiteBackup.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Operations.OffsiteBackup.Tests;

public sealed class OffsiteBackupUploadServiceTests : IDisposable
{
    private readonly string _sourceDirectory;
    private readonly string _targetDirectory;

    public OffsiteBackupUploadServiceTests()
    {
        _sourceDirectory = Path.Combine(Path.GetTempPath(), "alkaros-bkp001-src-" + Guid.NewGuid().ToString("N")[..8]);
        _targetDirectory = Path.Combine(Path.GetTempPath(), "alkaros-bkp001-tgt-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_sourceDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sourceDirectory))
            Directory.Delete(_sourceDirectory, recursive: true);
        if (Directory.Exists(_targetDirectory))
            Directory.Delete(_targetDirectory, recursive: true);
    }

    [Fact]
    public async Task UploadAsyncRoundTripsChecksumMatchesAndDecryptsWithAuthorizedKey()
    {
        var cipher = CipherFixtures.CreateCipher(out _, out _);
        var target = new LocalDirectoryOffsiteBackupTarget(_targetDirectory);
        var logger = new RecordingStructuredEventLogger();
        var service = new OffsiteBackupUploadService(target, cipher, logger);

        var artifact = SeedArtifact("alkaros_alkaros_20260922T010000Z.dump", "hello fiscal backup"u8.ToArray());

        var receipt = await service.UploadAsync(artifact);

        Assert.Equal(artifact.ArtifactId, receipt.ArtifactId);
        Assert.Equal(1, receipt.EncryptionKeyVersion);
        Assert.Empty(logger.Events);

        var verifier = new OffsiteBackupRestoreVerificationService(target, cipher);
        var plaintext = await verifier.DownloadAndVerifyAsync(receipt);
        Assert.Equal("hello fiscal backup", System.Text.Encoding.UTF8.GetString(plaintext));
    }

    [Fact]
    public async Task UploadAsyncSecondUploadOfSameArtifactIdThrowsImmutabilityViolation()
    {
        var cipher = CipherFixtures.CreateCipher(out _, out _);
        var target = new LocalDirectoryOffsiteBackupTarget(_targetDirectory);
        var service = new OffsiteBackupUploadService(target, cipher, new RecordingStructuredEventLogger());

        var artifact = SeedArtifact("alkaros_base_20260922T020000Z", "base backup bytes"u8.ToArray());
        await service.UploadAsync(artifact);

        await Assert.ThrowsAsync<OffsiteBackupImmutabilityViolationException>(() => service.UploadAsync(artifact));
    }

    [Fact]
    public async Task UploadAsyncCorruptLocalArtifactThrowsSourceIntegrityException()
    {
        var cipher = CipherFixtures.CreateCipher(out _, out _);
        var target = new LocalDirectoryOffsiteBackupTarget(_targetDirectory);
        var service = new OffsiteBackupUploadService(target, cipher, new RecordingStructuredEventLogger());

        var artifact = SeedArtifact("alkaros_settings_20260922T030000Z.dump", "settings dump"u8.ToArray());
        // Tamper with the recorded checksum without touching the file (simulates bit-rot detected against the sidecar).
        var tampered = new BackupArtifactReference(
            artifact.ArtifactId, artifact.DataClass, artifact.LocalPath,
            "0000000000000000000000000000000000000000000000000000000000000000".Substring(0, 64),
            artifact.ProducedAtUtc);

        await Assert.ThrowsAsync<OffsiteBackupSourceIntegrityException>(() => service.UploadAsync(tampered));
    }

    [Fact]
    public async Task UploadAsyncTransientFailuresBelowMaxAttemptsRetriesAndSucceeds()
    {
        var cipher = CipherFixtures.CreateCipher(out _, out _);
        var realTarget = new LocalDirectoryOffsiteBackupTarget(_targetDirectory);
        var flaky = new FlakyTarget(realTarget, failuresBeforeSuccess: 2);
        var logger = new RecordingStructuredEventLogger();
        var delays = new List<TimeSpan>();
        var service = CreateServiceWithSeams(flaky, cipher, logger, delays);

        var artifact = SeedArtifact("alkaros_base_20260922T040000Z", "retry me"u8.ToArray());
        var receipt = await service.UploadAsync(artifact);

        Assert.Equal(3, flaky.Attempts);
        Assert.Equal(2, delays.Count);
        Assert.Empty(logger.Events);
        Assert.NotNull(receipt);
    }

    [Fact]
    public async Task UploadAsyncFailuresExhaustMaxAttemptsEmitsCriticalAlertAndThrows()
    {
        var cipher = CipherFixtures.CreateCipher(out _, out _);
        var realTarget = new LocalDirectoryOffsiteBackupTarget(_targetDirectory);
        var flaky = new FlakyTarget(realTarget, failuresBeforeSuccess: int.MaxValue);
        var logger = new RecordingStructuredEventLogger();
        var service = CreateServiceWithSeams(flaky, cipher, logger, []);

        var artifact = SeedArtifact("alkaros_base_20260922T050000Z", "never lands"u8.ToArray());

        await Assert.ThrowsAsync<OffsiteBackupUploadFailedException>(() => service.UploadAsync(artifact));

        var alert = Assert.Single(logger.Events);
        Assert.Equal("offsitebackup.upload.failed", alert.EventName);
        Assert.Equal(LogSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void DecryptWithoutAuthorizedKeyVersionThrowsDecryptionFailed()
    {
        var cipher = CipherFixtures.CreateCipher(out var rotationStore, out var provider);
        var envelope = cipher.Encrypt("artifact-x", DataClass.Fiscal, "secret payload"u8.ToArray());

        // A second cipher instance over the same rotation store but a provider
        // that never received the key material - models a restore attempted
        // without the authorized key.
        var unauthorizedProvider = new ALKAROS.Secrets.InMemorySecretProvider();
        var unauthorizedCipher = new OffsiteBackupCipher(CipherFixtures.SecretName, rotationStore, unauthorizedProvider);

        Assert.Throws<OffsiteBackupDecryptionFailedException>(
            () => unauthorizedCipher.Decrypt("artifact-x", DataClass.Fiscal, envelope));
    }

    private BackupArtifactReference SeedArtifact(string artifactId, byte[] plaintext)
    {
        var path = Path.Combine(_sourceDirectory, artifactId);
        File.WriteAllBytes(path, plaintext);
        var checksum = Convert.ToHexString(SHA256.HashData(plaintext)).ToLowerInvariant();
        return new BackupArtifactReference(artifactId, DataClass.Fiscal, path, checksum, DateTimeOffset.UtcNow);
    }

    private static OffsiteBackupUploadService CreateServiceWithSeams(
        IOffsiteBackupTarget target,
        OffsiteBackupCipher cipher,
        RecordingStructuredEventLogger logger,
        List<TimeSpan> recordedDelays)
    {
        Func<string, CancellationToken, Task<byte[]>> reader = (path, ct) => File.ReadAllBytesAsync(path, ct);
        Func<int, TimeSpan, Task> delay = (attempt, span) =>
        {
            recordedDelays.Add(span);
            return Task.CompletedTask;
        };

        return new OffsiteBackupUploadService(target, cipher, logger, reader, delay);
    }

    /// <summary>Fails the first N upload attempts with a transient IOException, then delegates for real.</summary>
    private sealed class FlakyTarget : IOffsiteBackupTarget
    {
        private readonly IOffsiteBackupTarget _inner;
        private readonly int _failuresBeforeSuccess;
        public int Attempts { get; private set; }

        public FlakyTarget(IOffsiteBackupTarget inner, int failuresBeforeSuccess)
        {
            _inner = inner;
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public Task<string> UploadAsync(string artifactId, ReadOnlyMemory<byte> encryptedContent, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts <= _failuresBeforeSuccess)
                throw new IOException("simulated transient network failure");
            return _inner.UploadAsync(artifactId, encryptedContent, cancellationToken);
        }

        public Task<byte[]> DownloadAsync(string artifactId, CancellationToken cancellationToken = default) =>
            _inner.DownloadAsync(artifactId, cancellationToken);

        public Task<bool> ExistsAsync(string artifactId, CancellationToken cancellationToken = default) =>
            _inner.ExistsAsync(artifactId, cancellationToken);

        public Task<IReadOnlyList<string>> ListArtifactIdsAsync(CancellationToken cancellationToken = default) =>
            _inner.ListArtifactIdsAsync(cancellationToken);
    }
}
