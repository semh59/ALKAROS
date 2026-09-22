using ALKAROS.Audit.EventStore;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// Re-encrypts a live retention subject's envelope from an old key to a
/// current key (for example after <c>V15-SEC-001</c>'s own secret rotation
/// activates a new envelope-master-key version elsewhere) without resetting
/// its retention clock. Idempotent: a subject already on the target key is
/// a no-op. Fails closed on a disposed subject — an anonymized or
/// deletion-queued envelope has nothing left to re-encrypt. Authorized by
/// an explicit actor, recorded through V1-OPS-001's audit foundation.
/// </summary>
public sealed class AuthorizedReEncryptionService
{
    private readonly IRetentionSubjectStore _store;
    private readonly SensitivePayloadProtector _protector;
    private readonly IAuditEventStore _auditStore;

    /// <summary>
    /// Builds its own private secret-resolver/cipher/protector chain from
    /// <paramref name="secretProvider"/> instead of taking a shared
    /// <see cref="SensitivePayloadProtector"/> — <c>ISensitiveDataAccessPolicy</c>
    /// has no shared DI registration by design (see
    /// <see cref="RetentionAccessPolicy"/>), so this is the only component
    /// that can ever decrypt a retention subject's envelope.
    /// </summary>
    public AuthorizedReEncryptionService(
        IRetentionSubjectStore store,
        ISecretProvider secretProvider,
        IAuditEventStore auditStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(secretProvider);
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));

        var policy = new RetentionAccessPolicy();
        var resolver = new SecretResolver(secretProvider, policy);
        var cipher = new AesGcmEnvelopeCipher(resolver);
        _protector = new SensitivePayloadProtector(cipher, policy);
    }

    /// <summary>Returns <c>false</c> when the subject was already encrypted under <paramref name="newKey"/> (no-op).</summary>
    public async Task<bool> ReEncryptAsync(
        Guid subjectId,
        SecretReference oldKey,
        SecretReference newKey,
        Guid actorId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(oldKey);
        ArgumentNullException.ThrowIfNull(newKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var subject = await _store.GetAsync(subjectId, cancellationToken)
            ?? throw new RetentionSubjectNotFoundException(subjectId);
        if (subject.IsDisposed)
            throw new RetentionSubjectDisposedException(subjectId);

        if (string.Equals(subject.Envelope.Ciphertext.KeyId, newKey.Name, StringComparison.Ordinal))
            return false;

        var payload = _protector.Unprotect(subject.Envelope, oldKey, RetentionAccessPolicy.Accessor);
        var reEncrypted = _protector.Protect(payload, newKey, RetentionAccessPolicy.Accessor);
        // Protect() stamps its own fresh CreatedAt into the envelope's AAD
        // (cryptographically bound into the GCM tag) — that value cannot be
        // overwritten after the fact without breaking decryption. This is
        // harmless for retention: RetentionExecutionService decides
        // expiry from the row's own created_at column (set once at insert,
        // untouched by ReplaceEnvelopeAsync), never from envelope.CreatedAt,
        // so re-encryption never resets the retention clock.
        await _store.ReplaceEnvelopeAsync(subjectId, reEncrypted, subject.RowVersion, cancellationToken);
        await _auditStore.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(),
                "RetentionSubjectReEncrypted",
                "RetentionSubject",
                subjectId,
                actorType: "User",
                correlationId: correlationId,
                actorId: actorId),
            cancellationToken);
        return true;
    }
}
