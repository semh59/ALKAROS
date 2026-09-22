namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// The versioned rotation history for a single logical secret name.
/// Immutable: every transition returns a new instance reflecting the
/// requested change, or throws <see cref="SecretRotationConflictException"/>
/// when the transition would violate an invariant. Carries no secret
/// values — only <see cref="SecretVersionRecord"/> metadata.
/// </summary>
public sealed class SecretRotationRecord
{
    public string SecretName { get; }
    public IReadOnlyList<SecretVersionRecord> Versions { get; }

    private SecretRotationRecord(string secretName, IReadOnlyList<SecretVersionRecord> versions)
    {
        SecretName = secretName;
        Versions = versions;
    }

    /// <summary>Starts rotation history for a brand-new secret name at version 1, Active immediately.</summary>
    public static SecretRotationRecord Initialize(string secretName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        var first = new SecretVersionRecord(1, SecretVersionStatus.Active, now, overlapExpiresAtUtc: null, revokedAtUtc: null);
        return new SecretRotationRecord(secretName, new[] { first });
    }

    /// <summary>Rebuilds a rotation record from previously persisted versions (used by <see cref="ISecretRotationStore"/> implementations).</summary>
    public static SecretRotationRecord Restore(string secretName, IReadOnlyList<SecretVersionRecord> versions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0)
            throw new ArgumentException("A rotation record must carry at least one version.", nameof(versions));
        var activeCount = versions.Count(v => v.Status == SecretVersionStatus.Active);
        if (activeCount > 1)
            throw new ArgumentException("A rotation record cannot carry more than one Active version.", nameof(versions));

        return new SecretRotationRecord(secretName, versions.ToArray());
    }

    public SecretVersionRecord? ActiveVersion => Versions.SingleOrDefault(v => v.Status == SecretVersionStatus.Active);

    public IEnumerable<SecretVersionRecord> OverlapVersions => Versions.Where(v => v.Status == SecretVersionStatus.Overlap);

    /// <summary>
    /// Activates a new version, demoting the current Active version (if
    /// any — there may be none if it was revoked outright as an incident
    /// response) to Overlap for <paramref name="overlapWindow"/> so
    /// in-flight consumers keep working until they pick up the new
    /// version.
    /// </summary>
    public SecretRotationRecord Rotate(DateTimeOffset now, TimeSpan overlapWindow)
    {
        if (overlapWindow < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(overlapWindow), overlapWindow, "Overlap window must not be negative.");

        var nextVersion = Versions.Max(v => v.Version) + 1;
        var updated = Versions
            .Select(v => v.Status == SecretVersionStatus.Active
                ? v.With(SecretVersionStatus.Overlap, overlapExpiresAtUtc: now + overlapWindow)
                : v)
            .Append(new SecretVersionRecord(nextVersion, SecretVersionStatus.Active, now, overlapExpiresAtUtc: null, revokedAtUtc: null))
            .ToArray();
        return new SecretRotationRecord(SecretName, updated);
    }

    /// <summary>
    /// Revokes a version immediately, regardless of its current status
    /// (Active or Overlap). If the revoked version was Active, the secret
    /// has no Active version until the next <see cref="Rotate"/> — this
    /// models an incident response ("this credential leaked, kill it now,
    /// rotate next"), not a routine transition.
    /// </summary>
    public SecretRotationRecord Revoke(int version, DateTimeOffset now)
    {
        var target = FindOrThrow(version);
        if (target.Status == SecretVersionStatus.Revoked)
            throw new SecretRotationConflictException($"Version {version} of '{SecretName}' is already revoked.");

        var updated = Versions
            .Select(v => v.Version == version ? v.With(SecretVersionStatus.Revoked, revokedAtUtc: now) : v)
            .ToArray();
        return new SecretRotationRecord(SecretName, updated);
    }

    /// <summary>
    /// Rolls back to a version that is still within its Overlap window:
    /// promotes it back to Active and revokes the version that was Active
    /// (it was just proven bad — rollback is an incident response, not a
    /// routine rotation).
    /// </summary>
    public SecretRotationRecord Rollback(int targetVersion, DateTimeOffset now)
    {
        var target = FindOrThrow(targetVersion);
        if (target.Status != SecretVersionStatus.Overlap)
            throw new SecretRotationConflictException(
                $"Version {targetVersion} of '{SecretName}' is not in Overlap status ({target.Status}); only an Overlap version can be rolled back to.");
        if (target.OverlapExpiresAtUtc <= now)
            throw new SecretRotationConflictException(
                $"Version {targetVersion} of '{SecretName}' has an expired overlap window; it can no longer be rolled back to.");

        var active = ActiveVersion;
        var updated = Versions
            .Select(v =>
            {
                if (v.Version == targetVersion)
                    return v.With(SecretVersionStatus.Active);
                if (active is not null && v.Version == active.Version)
                    return v.With(SecretVersionStatus.Revoked, revokedAtUtc: now);
                return v;
            })
            .ToArray();
        return new SecretRotationRecord(SecretName, updated);
    }

    /// <summary>
    /// Revokes every Overlap version whose window has expired as of
    /// <paramref name="now"/>. Idempotent.
    /// </summary>
    public SecretRotationRecord ExpireOverlapWindows(DateTimeOffset now)
    {
        var updated = Versions
            .Select(v => v.Status == SecretVersionStatus.Overlap && v.OverlapExpiresAtUtc <= now
                ? v.With(SecretVersionStatus.Revoked, revokedAtUtc: now)
                : v)
            .ToArray();
        return new SecretRotationRecord(SecretName, updated);
    }

    private SecretVersionRecord FindOrThrow(int version) =>
        Versions.SingleOrDefault(v => v.Version == version)
            ?? throw new SecretRotationConflictException($"Version {version} of '{SecretName}' does not exist.");
}
