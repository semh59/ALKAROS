using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.SensitiveData.Tests.Fixtures;

/// <summary>
/// Test fixtures for the sensitive payload boundary: an open secret policy,
/// an accessor-based data access policy and a cipher wrapper that records
/// decrypt invocations.
/// </summary>
public static class SensitiveDataFixtures
{
    /// <summary>
    /// 32 ASCII bytes base64-encoded: a valid AES-256 envelope key.
    /// </summary>
    public const string ValidKeyBase64 = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    public static readonly SecretReference EnvelopeKey = new("Test/EnvelopeKey");

    public static InMemorySecretProvider CreateKeyProvider(string? base64Value = null)
    {
        var provider = new InMemorySecretProvider();
        if (base64Value is not null)
            provider.Set(EnvelopeKey, base64Value);
        return provider;
    }

    public static ISecretResolver CreateSecretResolver(InMemorySecretProvider provider) =>
        new SecretResolver(provider, AllowAllSecretAccessPolicy.Instance);

    public static IEnvelopeCipher CreateCipher(InMemorySecretProvider provider) =>
        new AesGcmEnvelopeCipher(CreateSecretResolver(provider));

    public sealed class AllowAllSecretAccessPolicy : ISecretAccessPolicy
    {
        public static readonly AllowAllSecretAccessPolicy Instance = new();

        public bool IsAllowed(string accessor, SecretReference reference) => true;
    }

    public sealed class AllowByAccessorSensitiveAccessPolicy : ISensitiveDataAccessPolicy
    {
        private readonly IReadOnlyCollection<string> _allowed;

        public AllowByAccessorSensitiveAccessPolicy(IReadOnlyCollection<string> allowed)
        {
            _allowed = allowed ?? throw new ArgumentNullException(nameof(allowed));
        }

        public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
            _allowed.Contains(accessor, StringComparer.Ordinal);
    }

    public sealed class RecordingEnvelopeCipher : IEnvelopeCipher
    {
        private readonly IEnvelopeCipher _inner;

        public RecordingEnvelopeCipher(IEnvelopeCipher inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int DecryptCalls { get; private set; }

        public EnvelopeCiphertext Encrypt(
            SecretReference key,
            string accessor,
            ReadOnlyMemory<byte> plaintext,
            ReadOnlyMemory<byte> associatedData) => _inner.Encrypt(key, accessor, plaintext, associatedData);

        public byte[] Decrypt(
            SecretReference key,
            string accessor,
            EnvelopeCiphertext ciphertext,
            ReadOnlyMemory<byte> associatedData)
        {
            DecryptCalls++;
            return _inner.Decrypt(key, accessor, ciphertext, associatedData);
        }
    }
}
