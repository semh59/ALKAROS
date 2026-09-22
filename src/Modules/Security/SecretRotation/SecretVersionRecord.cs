namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Metadata for a single version of a rotated secret. Deliberately carries
/// no secret value — only the version number, status and lifecycle
/// timestamps, so this type is safe to log, persist in plain JSON and
/// return from a diagnostics endpoint.
/// </summary>
public sealed record SecretVersionRecord
{
    public int Version { get; }
    public SecretVersionStatus Status { get; }
    public DateTimeOffset ActivatedAtUtc { get; }
    public DateTimeOffset? OverlapExpiresAtUtc { get; }
    public DateTimeOffset? RevokedAtUtc { get; }

    public SecretVersionRecord(
        int version,
        SecretVersionStatus status,
        DateTimeOffset activatedAtUtc,
        DateTimeOffset? overlapExpiresAtUtc,
        DateTimeOffset? revokedAtUtc)
    {
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Version must be a positive integer.");
        if (status == SecretVersionStatus.Overlap && overlapExpiresAtUtc is null)
            throw new ArgumentException("An Overlap version must carry an overlap expiry.", nameof(overlapExpiresAtUtc));
        if (status != SecretVersionStatus.Overlap && overlapExpiresAtUtc is not null)
            throw new ArgumentException("Only an Overlap version carries an overlap expiry.", nameof(overlapExpiresAtUtc));
        if (status == SecretVersionStatus.Revoked && revokedAtUtc is null)
            throw new ArgumentException("A Revoked version must carry a revocation timestamp.", nameof(revokedAtUtc));
        if (status != SecretVersionStatus.Revoked && revokedAtUtc is not null)
            throw new ArgumentException("Only a Revoked version carries a revocation timestamp.", nameof(revokedAtUtc));

        Version = version;
        Status = status;
        ActivatedAtUtc = activatedAtUtc;
        OverlapExpiresAtUtc = overlapExpiresAtUtc;
        RevokedAtUtc = revokedAtUtc;
    }

    public SecretVersionRecord With(
        SecretVersionStatus status,
        DateTimeOffset? overlapExpiresAtUtc = null,
        DateTimeOffset? revokedAtUtc = null) =>
        new(Version, status, ActivatedAtUtc, overlapExpiresAtUtc, revokedAtUtc);
}
