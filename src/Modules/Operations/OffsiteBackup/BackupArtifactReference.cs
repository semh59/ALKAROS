namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// Points at a locally produced backup artifact (a <c>backup.sh</c> pg_dump,
/// a <c>basebackup.sh</c> base backup member, or an archived WAL segment)
/// that <see cref="OffsiteBackupUploadService"/> should encrypt and ship
/// off-site. Producing the local artifact is out of this task's scope —
/// this record is the hand-off point.
/// </summary>
public sealed record BackupArtifactReference
{
    /// <summary>Stable identity for the artifact (e.g. the source file name). Used as the off-site object key.</summary>
    public string ArtifactId { get; }

    public DataClass DataClass { get; }

    /// <summary>Absolute path to the plaintext artifact on local disk.</summary>
    public string LocalPath { get; }

    /// <summary>The checksum the local producer (backup.sh/basebackup.sh sidecar) recorded for this artifact.</summary>
    public string ExpectedChecksumSha256 { get; }

    public DateTimeOffset ProducedAtUtc { get; }

    public BackupArtifactReference(
        string artifactId,
        DataClass dataClass,
        string localPath,
        string expectedChecksumSha256,
        DateTimeOffset producedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedChecksumSha256);

        ArtifactId = artifactId;
        DataClass = dataClass;
        LocalPath = localPath;
        ExpectedChecksumSha256 = expectedChecksumSha256.Trim().ToLowerInvariant();
        ProducedAtUtc = producedAtUtc;
    }
}
