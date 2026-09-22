namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// A provider-agnostic off-site storage destination for encrypted backup
/// artifacts. No specific vendor (S3, Azure Blob, ...) is named by this
/// task — implementations plug in behind this boundary. Every
/// implementation must be immutable: <see cref="UploadAsync"/> rejects a
/// second write under the same <c>artifactId</c> rather than overwriting it.
/// </summary>
public interface IOffsiteBackupTarget
{
    /// <summary>
    /// Uploads <paramref name="encryptedContent"/> under <paramref name="artifactId"/>.
    /// Returns an opaque location string. Throws
    /// <see cref="OffsiteBackupImmutabilityViolationException"/> if the
    /// artifact id already exists.
    /// </summary>
    Task<string> UploadAsync(string artifactId, ReadOnlyMemory<byte> encryptedContent, CancellationToken cancellationToken = default);

    Task<byte[]> DownloadAsync(string artifactId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string artifactId, CancellationToken cancellationToken = default);

    /// <summary>Every artifact id currently held, for RPO/freshness measurement.</summary>
    Task<IReadOnlyList<string>> ListArtifactIdsAsync(CancellationToken cancellationToken = default);
}
