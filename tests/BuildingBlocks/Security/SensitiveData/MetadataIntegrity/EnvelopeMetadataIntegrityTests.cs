using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using ALKAROS.SensitiveData.Tests.Fixtures;
using Xunit;

namespace ALKAROS.SensitiveData.Tests.MetadataIntegrity;

public static class EnvelopeMetadataIntegrityTests
{
    private const string Accessor = "BillingModule";

    [Fact]
    public static void AlteredClassificationCannotBypassPolicyOrDecrypt()
    {
        var protector = CreateProtector(new CategoryPolicy(SensitiveCategory.Public));
        var envelope = CreateProtectedEnvelope(protector);
        var altered = new SensitiveEnvelope(
            new Dictionary<string, SensitiveCategory>
            {
                ["customer-name"] = SensitiveCategory.Public,
            },
            envelope.Ciphertext,
            envelope.CreatedAt);

        Assert.Throws<SensitiveDataEncryptionException>(() =>
            protector.Unprotect(altered, SensitiveDataFixtures.EnvelopeKey, Accessor));
    }

    [Fact]
    public static void AlteredCreatedAtCannotDecrypt()
    {
        var protector = CreateProtector(new CategoryPolicy(SensitiveCategory.Pii));
        var envelope = CreateProtectedEnvelope(protector);
        var altered = new SensitiveEnvelope(
            envelope.FieldCategories,
            envelope.Ciphertext,
            envelope.CreatedAt.AddTicks(1));

        Assert.Throws<SensitiveDataEncryptionException>(() =>
            protector.Unprotect(altered, SensitiveDataFixtures.EnvelopeKey, Accessor));
    }

    [Fact]
    public static void AlteredKeyIdentityIsRejectedBeforeDecrypt()
    {
        var cipher = new SensitiveDataFixtures.RecordingEnvelopeCipher(
            SensitiveDataFixtures.CreateCipher(SensitiveDataFixtures.CreateKeyProvider(
                SensitiveDataFixtures.ValidKeyBase64)));
        var protector = new SensitivePayloadProtector(cipher, new CategoryPolicy(SensitiveCategory.Pii));
        var envelope = CreateProtectedEnvelope(protector);
        var alteredCiphertext = new EnvelopeCiphertext(
            "Different/Key",
            envelope.Ciphertext.Nonce,
            envelope.Ciphertext.Ciphertext,
            envelope.Ciphertext.Tag);
        var altered = new SensitiveEnvelope(envelope.FieldCategories, alteredCiphertext, envelope.CreatedAt);

        Assert.Throws<SensitiveDataException>(() =>
            protector.Unprotect(altered, SensitiveDataFixtures.EnvelopeKey, Accessor));

        Assert.Equal(0, cipher.DecryptCalls);
    }

    private static SensitiveEnvelope CreateProtectedEnvelope(SensitivePayloadProtector protector)
        => protector.Protect(
            new SensitivePayload(
                new Dictionary<string, string> { ["customer-name"] = "Alice Customer" },
                new Dictionary<string, SensitiveCategory> { ["customer-name"] = SensitiveCategory.Pii }),
            SensitiveDataFixtures.EnvelopeKey,
            Accessor);

    private static SensitivePayloadProtector CreateProtector(ISensitiveDataAccessPolicy policy)
        => new(
            SensitiveDataFixtures.CreateCipher(SensitiveDataFixtures.CreateKeyProvider(
                SensitiveDataFixtures.ValidKeyBase64)),
            policy);

    private sealed class CategoryPolicy : ISensitiveDataAccessPolicy
    {
        private readonly SensitiveCategory _allowedCategory;

        public CategoryPolicy(SensitiveCategory allowedCategory)
        {
            _allowedCategory = allowedCategory;
        }

        public bool CanRead(string accessor, SensitiveEnvelope envelope)
            => accessor == Accessor && envelope.FieldCategories.Values.All(category => category == _allowedCategory);
    }
}
