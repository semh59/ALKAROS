using System.Text;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using ALKAROS.SensitiveData.Tests.Fixtures;
using Xunit;

namespace ALKAROS.SensitiveData.Tests.Encryption;

public static class EnvelopeHardeningTests
{
    private const string Accessor = "BillingModule";

    private static readonly SensitivePayload Sample = new(
        new Dictionary<string, string> { ["customer-name"] = "Alice Customer" },
        new Dictionary<string, SensitiveCategory> { ["customer-name"] = SensitiveCategory.Pii });

    [Fact]
    public static void EveryEncryptionUsesAFreshNonce()
    {
        var protector = Protector(SensitiveDataFixtures.ValidKeyBase64);

        var nonces = Enumerable.Range(0, 50)
            .Select(_ => Convert.ToBase64String(protector.Protect(Sample, SensitiveDataFixtures.EnvelopeKey, Accessor).Ciphertext.Nonce))
            .ToHashSet();

        Assert.Equal(50, nonces.Count);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(31)]
    [InlineData(33)]
    public static void OnlyA32ByteKeyIsAccepted(int length)
    {
        var protector = Protector(Convert.ToBase64String(new byte[length]));

        Assert.Throws<SensitiveDataEncryptionException>(() =>
            protector.Protect(Sample, SensitiveDataFixtures.EnvelopeKey, Accessor));
    }

    [Fact]
    public static void ATamperedNonceFailsTheIntegrityCheck()
    {
        var protector = Protector(SensitiveDataFixtures.ValidKeyBase64);
        var envelope = protector.Protect(Sample, SensitiveDataFixtures.EnvelopeKey, Accessor);
        var nonce = (byte[])envelope.Ciphertext.Nonce.Clone();
        nonce[0] ^= 0xFF;

        Assert.Throws<SensitiveDataEncryptionException>(() => protector.Unprotect(
            envelope with { Ciphertext = envelope.Ciphertext with { Nonce = nonce } },
            SensitiveDataFixtures.EnvelopeKey, Accessor));
    }

    [Fact]
    public static void ATamperedTagFailsTheIntegrityCheck()
    {
        var protector = Protector(SensitiveDataFixtures.ValidKeyBase64);
        var envelope = protector.Protect(Sample, SensitiveDataFixtures.EnvelopeKey, Accessor);
        var tag = (byte[])envelope.Ciphertext.Tag.Clone();
        tag[0] ^= 0xFF;

        Assert.Throws<SensitiveDataEncryptionException>(() => protector.Unprotect(
            envelope with { Ciphertext = envelope.Ciphertext with { Tag = tag } },
            SensitiveDataFixtures.EnvelopeKey, Accessor));
    }

    [Theory]
    [InlineData(5, 16)]
    [InlineData(12, 3)]
    public static void ANonceOrTagOfTheWrongSizeIsATypedFailureNotARawArgumentException(int nonceLength, int tagLength)
    {
        var protector = Protector(SensitiveDataFixtures.ValidKeyBase64);
        var envelope = protector.Protect(Sample, SensitiveDataFixtures.EnvelopeKey, Accessor);

        Assert.Throws<SensitiveDataEncryptionException>(() => protector.Unprotect(
            envelope with
            {
                Ciphertext = envelope.Ciphertext with { Nonce = new byte[nonceLength], Tag = new byte[tagLength] },
            },
            SensitiveDataFixtures.EnvelopeKey, Accessor));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"fieldCategories\":{}}")]
    [InlineData("null")]
    [InlineData("not json")]
    public static void PersistedBytesWithMissingFieldsAreATypedFailure(string json)
        => Assert.Throws<SensitiveDataException>(() =>
            SensitiveEnvelope.FromPersistenceBytes(Encoding.UTF8.GetBytes(json)));

    private static SensitivePayloadProtector Protector(string base64Key)
        => new(
            SensitiveDataFixtures.CreateCipher(SensitiveDataFixtures.CreateKeyProvider(base64Key)),
            new SensitiveDataFixtures.AllowByAccessorSensitiveAccessPolicy(new[] { Accessor }));
}
