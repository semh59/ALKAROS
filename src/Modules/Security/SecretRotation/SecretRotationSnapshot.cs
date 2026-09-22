namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// A redacted, read-only view of a secret's rotation state for operational
/// diagnostics. Carries version numbers and timestamps only — never a
/// secret value, by construction (this type has no field capable of
/// holding one).
/// </summary>
public sealed record SecretRotationSnapshot(
    string SecretName,
    int? ActiveVersion,
    DateTimeOffset? ActiveActivatedAtUtc,
    int OverlapVersionCount,
    DateTimeOffset? EarliestOverlapExpiresAtUtc,
    int RevokedVersionCount,
    DateTimeOffset LastRotationAtUtc)
{
    public static SecretRotationSnapshot From(SecretRotationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var active = record.ActiveVersion;
        var overlaps = record.Versions.Where(v => v.Status == SecretVersionStatus.Overlap).ToArray();
        var revokedCount = record.Versions.Count(v => v.Status == SecretVersionStatus.Revoked);
        var lastRotation = record.Versions.Max(v => v.ActivatedAtUtc);

        return new SecretRotationSnapshot(
            record.SecretName,
            active?.Version,
            active?.ActivatedAtUtc,
            overlaps.Length,
            overlaps.Length > 0 ? overlaps.Min(v => v.OverlapExpiresAtUtc) : null,
            revokedCount,
            lastRotation);
    }
}
