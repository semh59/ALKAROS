using System.Security.Cryptography;

namespace ALKAROS.Operations.BackupHealth;

/// <summary>
/// Engine for executing and verifying local backup files (V1-OPS-002).
/// </summary>
public interface IBackupEngine
{
    Task<BackupExecutionResult> CreateBackupFileAsync(
        Guid backupId,
        BackupType backupType,
        string destinationDirectory,
        byte[] payload,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyBackupChecksumAsync(
        string filePath,
        string expectedChecksumSha256,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Local disk backup engine implementation with atomic write and SHA-256 checksum verification (V1-OPS-002).
/// </summary>
public sealed class LocalBackupEngine : IBackupEngine
{
    public async Task<BackupExecutionResult> CreateBackupFileAsync(
        Guid backupId,
        BackupType backupType,
        string destinationDirectory,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("Destination directory cannot be null or whitespace.", nameof(destinationDirectory));

        string? temporaryPath = null;
        try
        {
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var fileName = $"alkaros_backup_{backupType.ToString().ToLowerInvariant()}_{backupId:N}.bak";
            var filePath = Path.Combine(destinationDirectory, fileName);
            temporaryPath = filePath + $".{Guid.NewGuid():N}.tmp";

            // Write and flush a sibling temp file, then replace the target on the
            // same volume. This prevents a crash from exposing a partial backup.
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(payload, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
            temporaryPath = null;

            // Compute SHA-256 checksum
            var hashBytes = SHA256.HashData(payload);
            var checksumHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

            var fileInfo = new FileInfo(filePath);

            return new BackupExecutionResult(
                backupId,
                backupType,
                filePath,
                fileInfo.Length,
                checksumHex,
                IsSuccess: true,
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BackupExecutionResult(
                backupId,
                backupType,
                string.Empty,
                0,
                string.Empty,
                IsSuccess: false,
                ErrorMessage: ex.Message);
        }
        finally
        {
            if (temporaryPath is not null)
                File.Delete(temporaryPath);
        }
    }

    public async Task<bool> VerifyBackupChecksumAsync(
        string filePath,
        string expectedChecksumSha256,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        if (string.IsNullOrWhiteSpace(expectedChecksumSha256))
            return false;

        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        var actualChecksum = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return string.Equals(actualChecksum, expectedChecksumSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }
}
