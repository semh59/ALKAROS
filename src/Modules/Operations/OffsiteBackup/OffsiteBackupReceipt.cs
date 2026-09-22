namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// Immutable proof that an artifact was encrypted and accepted by the
/// off-site target. Carries only metadata — the checksum is over the
/// plaintext (so a later restore can verify without re-deriving it), never
/// a raw secret value or plaintext content.
/// </summary>
public sealed record OffsiteBackupReceipt
{
    public string ArtifactId { get; }
    public DataClass DataClass { get; }
    public string PlaintextChecksumSha256 { get; }
    public string EncryptionKeyName { get; }
    public int EncryptionKeyVersion { get; }
    public long PlaintextSizeBytes { get; }
    public string TargetLocation { get; }
    public DateTimeOffset UploadedAtUtc { get; }

    public OffsiteBackupReceipt(
        string artifactId,
        DataClass dataClass,
        string plaintextChecksumSha256,
        string encryptionKeyName,
        int encryptionKeyVersion,
        long plaintextSizeBytes,
        string targetLocation,
        DateTimeOffset uploadedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextChecksumSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptionKeyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLocation);
        if (encryptionKeyVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(encryptionKeyVersion), encryptionKeyVersion, "Key version must be positive.");
        if (plaintextSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(plaintextSizeBytes), plaintextSizeBytes, "Size cannot be negative.");

        ArtifactId = artifactId;
        DataClass = dataClass;
        PlaintextChecksumSha256 = plaintextChecksumSha256.Trim().ToLowerInvariant();
        EncryptionKeyName = encryptionKeyName;
        EncryptionKeyVersion = encryptionKeyVersion;
        PlaintextSizeBytes = plaintextSizeBytes;
        TargetLocation = targetLocation;
        UploadedAtUtc = uploadedAtUtc;
    }
}
