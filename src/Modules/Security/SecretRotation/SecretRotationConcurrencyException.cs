namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Thrown by an <see cref="ISecretRotationStore"/> when <c>Save</c> is
/// called with a <see cref="SecretRotationRecord"/> whose
/// <see cref="SecretRotationRecord.Version"/> is not exactly one past the
/// version currently persisted for that secret name. This is the
/// lost-update guard: it means the caller's in-memory record was built
/// from a <c>Find</c> that is now stale — another writer (e.g. a
/// concurrent Revoke racing a Rotate) has already saved a newer state.
/// The caller must re-read via <c>Find</c>, redo its mutation against the
/// current state, and retry.
/// </summary>
public sealed class SecretRotationConcurrencyException : Exception
{
    public string SecretName { get; }

    public SecretRotationConcurrencyException(string secretName)
        : base($"Concurrent modification detected while saving rotation state for '{secretName}': " +
               "the record being saved was not based on the currently persisted version. " +
               "Re-read the current state and retry the mutation.")
    {
        SecretName = secretName;
    }
}
