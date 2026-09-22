using System.Security.Cryptography;
using ALKAROS.Secrets;
using ALKAROS.Security.SecretRotation;

namespace ALKAROS.Operations.RestoreVerification.Tests.Fixtures;

/// <summary>Builds a ready-to-use OffsiteBackupCipher backed by a fresh in-memory rotation store and secret provider.</summary>
public static class CipherFixtures
{
    public const string SecretName = "restore-verification-test-key";

    public static OffsiteBackup.OffsiteBackupCipher CreateCipher()
    {
        var rotationStore = new InMemorySecretRotationStore();
        rotationStore.Save(SecretRotationRecord.Initialize(SecretName, DateTimeOffset.UtcNow));

        var provider = new InMemorySecretProvider();
        var reference = SecretVersionReferenceNaming.ReferenceFor(SecretName, version: 1);
        provider.Set(reference, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        return new OffsiteBackup.OffsiteBackupCipher(SecretName, rotationStore, provider);
    }
}
