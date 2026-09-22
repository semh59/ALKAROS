namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>Thrown when an upload would overwrite an artifact id the target already holds.</summary>
public sealed class OffsiteBackupImmutabilityViolationException : Exception
{
    public OffsiteBackupImmutabilityViolationException(string artifactId)
        : base($"Artifact '{artifactId}' already exists at the off-site target; artifacts are immutable.")
    {
    }
}

/// <summary>Thrown when every retry attempt to reach the off-site target has failed.</summary>
public sealed class OffsiteBackupUploadFailedException : Exception
{
    public OffsiteBackupUploadFailedException(string artifactId, int attempts, Exception innerException)
        : base($"Uploading artifact '{artifactId}' failed after {attempts} attempt(s).", innerException)
    {
    }
}

/// <summary>Thrown when the local artifact's bytes no longer match the checksum its producer recorded.</summary>
public sealed class OffsiteBackupSourceIntegrityException : Exception
{
    public OffsiteBackupSourceIntegrityException(string artifactId)
        : base($"Local artifact '{artifactId}' does not match its recorded checksum; refusing to upload a corrupt source.")
    {
    }
}

/// <summary>Thrown when an artifact cannot be decrypted (wrong/missing key version, or tampered ciphertext).</summary>
public sealed class OffsiteBackupDecryptionFailedException : Exception
{
    public OffsiteBackupDecryptionFailedException(string artifactId, Exception innerException)
        : base($"Artifact '{artifactId}' could not be decrypted.", innerException)
    {
    }
}
