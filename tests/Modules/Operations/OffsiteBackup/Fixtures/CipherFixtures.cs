using System.Security.Cryptography;
using ALKAROS.Secrets;
using ALKAROS.Security.SecretRotation;

namespace ALKAROS.Operations.OffsiteBackup.Tests.Fixtures;

/// <summary>Builds a ready-to-use OffsiteBackupCipher backed by a fresh in-memory rotation store and secret provider.</summary>
public static class CipherFixtures
{
    public const string SecretName = "offsite-backup-test-key";

    public static OffsiteBackupCipher CreateCipher(out ISecretRotationStore rotationStore, out InMemorySecretProvider provider)
    {
        rotationStore = new InMemorySecretRotationStore();
        rotationStore.Save(SecretRotationRecord.Initialize(SecretName, DateTimeOffset.UtcNow));

        provider = new InMemorySecretProvider();
        SeedKeyVersion(provider, version: 1);

        return new OffsiteBackupCipher(SecretName, rotationStore, provider);
    }

    public static void SeedKeyVersion(InMemorySecretProvider provider, int version)
    {
        var reference = SecretVersionReferenceNaming.ReferenceFor(SecretName, version);
        provider.Set(reference, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }
}
