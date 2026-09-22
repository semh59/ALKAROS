namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Persists rotation state (version metadata only — never a secret value)
/// keyed by secret name.
/// </summary>
public interface ISecretRotationStore
{
    /// <summary>Returns the rotation record for <paramref name="secretName"/>, or <c>null</c> if it has never been rotated.</summary>
    SecretRotationRecord? Find(string secretName);

    /// <summary>
    /// Persists <paramref name="record"/>, replacing any prior state for
    /// the same secret name. When state is already persisted for that
    /// secret name, enforces optimistic concurrency on
    /// <see cref="SecretRotationRecord.Version"/>: <paramref name="record"/>
    /// must carry a version exactly one past the currently persisted
    /// version (i.e. it must have been produced by a single mutation of
    /// the record most recently returned by <see cref="Find"/>), or this
    /// throws <see cref="SecretRotationConcurrencyException"/> instead of
    /// silently overwriting a concurrent writer's change — the scenario
    /// this guards is a Revoke and a Rotate racing on the same secret,
    /// where a naive last-write-wins Save could silently discard the
    /// Revoke. The very first Save for a secret name is never rejected on
    /// this basis, since there is no prior state to conflict with.
    /// </summary>
    /// <exception cref="SecretRotationConcurrencyException">
    /// Thrown when <paramref name="record"/> was not built from the
    /// currently persisted state.
    /// </exception>
    void Save(SecretRotationRecord record);
}
