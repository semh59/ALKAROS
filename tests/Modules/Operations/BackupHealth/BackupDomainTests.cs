using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Operations.BackupHealth.Tests;

public sealed class BackupDomainTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly LocalBackupEngine _engine;

    public BackupDomainTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "alkaros_test_backups_" + Guid.NewGuid().ToString("N"));
        _engine = new LocalBackupEngine();
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
    public async Task CreateBackupFileCalculatesAccurateSha256Checksum()
    {
        var backupId = Guid.NewGuid();
        var rawData = "PG_DUMP_SIMULATION_DATA_FOR_ALKAROS_RESTAURANT_TEST"u8.ToArray();

        var expectedHash = Convert.ToHexString(SHA256.HashData(rawData)).ToLowerInvariant();

        var result = await _engine.CreateBackupFileAsync(
            backupId,
            BackupType.Full,
            _tempDirectory,
            rawData);

        result.IsSuccess.Should().BeTrue();
        result.FileSizeBytes.Should().Be(rawData.Length);
        result.ChecksumSha256.Should().Be(expectedHash);
        File.Exists(result.FilePath).Should().BeTrue();
        Directory.GetFiles(_tempDirectory, "*.tmp").Should().BeEmpty();

        // Verify with engine
        var isVerified = await _engine.VerifyBackupChecksumAsync(result.FilePath, expectedHash);
        isVerified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyBackupChecksumReturnsFalseOnTamperedFile()
    {
        var backupId = Guid.NewGuid();
        var rawData = "ORIGINAL_BACKUP_PAYLOAD"u8.ToArray();
        var expectedHash = Convert.ToHexString(SHA256.HashData(rawData)).ToLowerInvariant();

        var result = await _engine.CreateBackupFileAsync(
            backupId,
            BackupType.Incremental,
            _tempDirectory,
            rawData);

        // Tamper with the file
        await File.WriteAllTextAsync(result.FilePath, "TAMPERED_CONTENT_AFTER_BACKUP");

        var isVerified = await _engine.VerifyBackupChecksumAsync(result.FilePath, expectedHash);
        isVerified.Should().BeFalse();
    }

    [Fact]
    public async Task CreateBackupFileWithInvalidPathReturnsFailureResultWithoutThrowing()
    {
        var backupId = Guid.NewGuid();
        var rawData = "TEST_PAYLOAD"u8.ToArray();

        // Invalid path with illegal characters on Windows
        var invalidPath = "Z:\\non_existent_drive_alkaros_test\\sub\\";

        var result = await _engine.CreateBackupFileAsync(
            backupId,
            BackupType.SchemaOnly,
            invalidPath,
            rawData);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ChecksumSha256.Should().BeEmpty();
    }
}
