using ALKAROS.Operations.OffsiteBackup;

namespace ALKAROS.Operations.RestoreVerification;

/// <summary>Thrown when no off-site receipt exists for the requested data class — nothing to drill.</summary>
public sealed class RestoreArtifactNotFoundException : Exception
{
    public RestoreArtifactNotFoundException(DataClass dataClass)
        : base($"No off-site backup receipt is recorded for data class '{dataClass}'.")
    {
    }
}

/// <summary>Thrown when a restored database fails a named integrity check — the artifact restored, but its contents did not.</summary>
public sealed class RestoreDumpApplyFailedException : Exception
{
    public RestoreDumpApplyFailedException(int exitCode, string diagnostic)
        : base($"pg_restore exited with code {exitCode}: {diagnostic}")
    {
    }
}

public sealed class RestoreIntegrityCheckFailedException : Exception
{
    public RestoreIntegrityCheckFailedException(string checkName, string artifactId)
        : base($"Integrity check '{checkName}' failed after restoring artifact '{artifactId}'.")
    {
    }
}

/// <summary>Thrown when the post-restore application smoke check does not return the expected result.</summary>
public sealed class RestoreApplicationSmokeCheckFailedException : Exception
{
    public RestoreApplicationSmokeCheckFailedException(string artifactId)
        : base($"Application smoke check failed after restoring artifact '{artifactId}'.")
    {
    }
}
