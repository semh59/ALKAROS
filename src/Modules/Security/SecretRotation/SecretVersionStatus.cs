namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// The lifecycle status of a single version within a secret's rotation
/// history.
/// </summary>
public enum SecretVersionStatus
{
    /// <summary>The version currently served for new resolutions.</summary>
    Active,

    /// <summary>
    /// A previously Active version kept resolvable until
    /// <see cref="SecretVersionRecord.OverlapExpiresAtUtc"/> so in-flight
    /// consumers can finish picking up the new Active version.
    /// </summary>
    Overlap,

    /// <summary>No longer resolvable, permanently.</summary>
    Revoked,
}
