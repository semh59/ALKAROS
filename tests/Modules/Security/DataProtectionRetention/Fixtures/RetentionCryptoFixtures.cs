using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;

/// <summary>
/// This module's own private secret/cipher/access-policy chain —
/// ISensitiveDataAccessPolicy has no shared DI registration by design, so
/// every consumer builds its own resolver chain rather than depending on a
/// global singleton.
/// </summary>
public static class RetentionCryptoFixtures
{
    public const string Accessor = "retention-test";
    public const string OldKeyBase64 = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    public const string NewKeyBase64 = "ZmVkY2JhOTg3NjU0MzIxMGZlZGNiYTk4NzY1NDMyMTA=";

    public static readonly SecretReference OldKey = new("Test/RetentionOldKey");
    public static readonly SecretReference NewKey = new("Test/RetentionNewKey");

    public static SensitivePayloadProtector CreateProtector()
    {
        var provider = new InMemorySecretProvider();
        provider.Set(OldKey, OldKeyBase64);
        provider.Set(NewKey, NewKeyBase64);
        var resolver = new SecretResolver(provider, AllowAllSecretAccessPolicy.Instance);
        var cipher = new AesGcmEnvelopeCipher(resolver);
        return new SensitivePayloadProtector(cipher, AllowAllSensitiveAccessPolicy.Instance);
    }

    public static SensitiveEnvelope ProtectTestPayload(SensitivePayloadProtector protector, SecretReference key) =>
        protector.Protect(
            new SensitivePayload(
                new Dictionary<string, string> { ["value"] = "provider-response-body" },
                new Dictionary<string, SensitiveCategory> { ["value"] = SensitiveCategory.Payment }),
            key,
            Accessor);

    public sealed class AllowAllSecretAccessPolicy : ISecretAccessPolicy
    {
        public static readonly AllowAllSecretAccessPolicy Instance = new();

        public bool IsAllowed(string accessor, SecretReference reference) => true;
    }

    public sealed class AllowAllSensitiveAccessPolicy : ISensitiveDataAccessPolicy
    {
        public static readonly AllowAllSensitiveAccessPolicy Instance = new();

        public bool CanRead(string accessor, SensitiveEnvelope envelope) => true;
    }
}
