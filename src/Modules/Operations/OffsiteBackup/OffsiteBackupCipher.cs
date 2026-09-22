using ALKAROS.Secrets;
using ALKAROS.Security.SecretRotation;
using ALKAROS.SensitiveData;

namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// Client-side envelope encryption for off-site backup artifacts, keyed
/// through V15-SEC-001's versioned secret rotation (V0-ARC-005: this
/// module builds its own private resolver/cipher chain rather than sharing
/// another module's DI registration). The ciphertext is bound to the
/// artifact id and data class via AES-GCM associated data, so a ciphertext
/// cannot be silently replayed under a different identity.
/// </summary>
public sealed class OffsiteBackupCipher
{
    private const string Accessor = "offsite-backup";

    private readonly string _secretName;
    private readonly ISecretRotationStore _rotationStore;
    private readonly AesGcmEnvelopeCipher _cipher;
    private readonly ISecretResolver _resolver;

    public OffsiteBackupCipher(string secretName, ISecretRotationStore rotationStore, ISecretProvider secretProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        _secretName = secretName;
        _rotationStore = rotationStore ?? throw new ArgumentNullException(nameof(rotationStore));
        ArgumentNullException.ThrowIfNull(secretProvider);

        _resolver = new SecretResolver(secretProvider, new SingleAccessorPolicy());
        _cipher = new AesGcmEnvelopeCipher(_resolver);
    }

    /// <summary>Encrypts <paramref name="plaintext"/> under the secret's current Active rotation version.</summary>
    public EncryptedBackupEnvelope Encrypt(string artifactId, DataClass dataClass, ReadOnlyMemory<byte> plaintext)
    {
        var version = ActiveVersionOrThrow();
        var reference = SecretVersionReferenceNaming.ReferenceFor(_secretName, version);
        var associatedData = AssociatedDataFor(artifactId, dataClass);

        var ciphertext = _cipher.Encrypt(reference, Accessor, plaintext, associatedData);
        return new EncryptedBackupEnvelope(_secretName, version, ciphertext.Nonce, ciphertext.Ciphertext, ciphertext.Tag);
    }

    /// <summary>Decrypts an envelope using the exact key version it was encrypted under.</summary>
    public byte[] Decrypt(string artifactId, DataClass dataClass, EncryptedBackupEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!string.Equals(envelope.SecretName, _secretName, StringComparison.Ordinal))
            throw new OffsiteBackupDecryptionFailedException(artifactId, new InvalidOperationException(
                $"Envelope was encrypted under secret '{envelope.SecretName}', not '{_secretName}'."));

        var reference = SecretVersionReferenceNaming.ReferenceFor(_secretName, envelope.KeyVersion);
        var associatedData = AssociatedDataFor(artifactId, dataClass);
        var ciphertext = new EnvelopeCiphertext(reference.Name, envelope.Nonce, envelope.Ciphertext, envelope.Tag);

        try
        {
            return _cipher.Decrypt(reference, Accessor, ciphertext, associatedData);
        }
        catch (SensitiveDataEncryptionException exception)
        {
            throw new OffsiteBackupDecryptionFailedException(artifactId, exception);
        }
        catch (SecretNotFoundException exception)
        {
            throw new OffsiteBackupDecryptionFailedException(artifactId, exception);
        }
    }

    private int ActiveVersionOrThrow()
    {
        var rotation = _rotationStore.Find(_secretName)
            ?? throw new SecretRotationUnavailableException(_secretName);
        return rotation.ActiveVersion?.Version
            ?? throw new SecretRotationUnavailableException(_secretName);
    }

    private static byte[] AssociatedDataFor(string artifactId, DataClass dataClass) =>
        System.Text.Encoding.UTF8.GetBytes($"offsite-backup:{dataClass}:{artifactId}");

    private sealed class SingleAccessorPolicy : ISecretAccessPolicy
    {
        public bool IsAllowed(string accessor, SecretReference reference) =>
            string.Equals(accessor, Accessor, StringComparison.Ordinal);
    }
}

/// <summary>The serializable, at-rest form of an encrypted backup artifact.</summary>
public sealed record EncryptedBackupEnvelope(
    string SecretName,
    int KeyVersion,
    byte[] Nonce,
    byte[] Ciphertext,
    byte[] Tag);
