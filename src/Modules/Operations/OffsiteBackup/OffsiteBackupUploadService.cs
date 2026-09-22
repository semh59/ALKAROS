using System.Security.Cryptography;
using ALKAROS.Messaging;
using ALKAROS.Observability.StructuredLogging;

namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// Orchestrates one artifact's off-site upload: verify the local source
/// against its recorded checksum, encrypt it, retry the target write with
/// <see cref="RetryPolicy"/>'s exponential backoff (max 3 attempts, same as
/// the outbox dispatcher), and emit a structured alert event on final
/// failure. Local backup creation and restore orchestration are out of
/// scope (V15-BKP-002) — this service only moves an already-produced,
/// already-checksummed artifact off-site.
/// </summary>
public sealed class OffsiteBackupUploadService
{
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(2);

    private readonly IOffsiteBackupTarget _target;
    private readonly OffsiteBackupCipher _cipher;
    private readonly IStructuredEventLogger _eventLogger;
    private readonly Func<string, CancellationToken, Task<byte[]>> _readLocalArtifact;
    private readonly Func<int, TimeSpan, Task> _delay;

    public OffsiteBackupUploadService(
        IOffsiteBackupTarget target,
        OffsiteBackupCipher cipher,
        IStructuredEventLogger eventLogger)
        : this(target, cipher, eventLogger, DefaultReadLocalArtifact, DefaultDelay)
    {
    }

    /// <summary>Test seam: replace the local-file reader and the retry delay with instrumented fakes.</summary>
    public OffsiteBackupUploadService(
        IOffsiteBackupTarget target,
        OffsiteBackupCipher cipher,
        IStructuredEventLogger eventLogger,
        Func<string, CancellationToken, Task<byte[]>> readLocalArtifact,
        Func<int, TimeSpan, Task> delay)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
        _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        _readLocalArtifact = readLocalArtifact;
        _delay = delay;
    }

    public async Task<OffsiteBackupReceipt> UploadAsync(BackupArtifactReference artifact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        if (await _target.ExistsAsync(artifact.ArtifactId, cancellationToken))
            throw new OffsiteBackupImmutabilityViolationException(artifact.ArtifactId);

        var plaintext = await _readLocalArtifact(artifact.LocalPath, cancellationToken);
        var actualChecksum = Convert.ToHexString(SHA256.HashData(plaintext)).ToLowerInvariant();
        if (!string.Equals(actualChecksum, artifact.ExpectedChecksumSha256, StringComparison.Ordinal))
            throw new OffsiteBackupSourceIntegrityException(artifact.ArtifactId);

        var envelope = _cipher.Encrypt(artifact.ArtifactId, artifact.DataClass, plaintext);
        var encoded = EncryptedBackupEnvelopeCodec.Encode(envelope);

        var location = await UploadWithRetryAsync(artifact, encoded, cancellationToken);

        return new OffsiteBackupReceipt(
            artifact.ArtifactId,
            artifact.DataClass,
            actualChecksum,
            envelope.SecretName,
            envelope.KeyVersion,
            plaintext.LongLength,
            location,
            DateTimeOffset.UtcNow);
    }

    private async Task<string> UploadWithRetryAsync(BackupArtifactReference artifact, byte[] encoded, CancellationToken cancellationToken)
    {
        Exception? lastFailure = null;
        for (var attempt = 1; attempt <= RetryPolicy.MaxAttempts; attempt++)
        {
            try
            {
                return await _target.UploadAsync(artifact.ArtifactId, encoded, cancellationToken);
            }
            catch (Exception exception) when (exception is not OffsiteBackupImmutabilityViolationException and not OperationCanceledException)
            {
                lastFailure = exception;
                if (attempt < RetryPolicy.MaxAttempts)
                    await _delay(attempt, RetryPolicy.NextRetryDelay(attempt, BaseRetryDelay));
            }
        }

        _eventLogger.Emit(
            "offsite_backup.upload_failed",
            LogSeverity.Critical,
            payload: new Dictionary<string, object?>
            {
                ["artifactId"] = artifact.ArtifactId,
                ["dataClass"] = artifact.DataClass.ToString(),
                ["attempts"] = RetryPolicy.MaxAttempts,
            });

        throw new OffsiteBackupUploadFailedException(artifact.ArtifactId, RetryPolicy.MaxAttempts, lastFailure!);
    }

    private static Task<byte[]> DefaultReadLocalArtifact(string path, CancellationToken cancellationToken) =>
        File.ReadAllBytesAsync(path, cancellationToken);

    private static async Task DefaultDelay(int attempt, TimeSpan delay) => await Task.Delay(delay);
}
