using System.Text;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using ALKAROS.SensitiveData.Tests.Fixtures;
using Xunit;

namespace ALKAROS.SensitiveData.Tests.Encryption;

/// <summary>
/// Tests for fail-closed key and integrity failures: a missing, malformed
/// or wrong key never produces or returns plaintext, and tampered
/// ciphertext fails the integrity check with no plaintext leaked.
/// </summary>
public static class KeyFailureTests
{
    private const string Accessor = "BillingModule";
    private const string CustomerName = "Alice Customer";

    private static readonly SensitivePayload SamplePayload = new(
        new Dictionary<string, string>
        {
            ["order-id"] = "ORD-1001",
            ["customer-name"] = CustomerName,
        },
        new Dictionary<string, SensitiveCategory>
        {
            ["order-id"] = SensitiveCategory.Public,
            ["customer-name"] = SensitiveCategory.Pii,
        });

    [Fact]
    public static void MissingKeyFailsClosedOnProtect()
    {
        var protector = CreateProtector(provider: SensitiveDataFixtures.CreateKeyProvider());

        Assert.Throws<SecretNotFoundException>(() =>
            protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, Accessor));
    }

    [Fact]
    public static void MissingKeyFailsClosedOnUnprotect()
    {
        var protector = CreateProtector(SensitiveDataFixtures.ValidKeyBase64);
        var envelope = protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, Accessor);

        var missingKeyProtector = CreateProtector(provider: SensitiveDataFixtures.CreateKeyProvider());

        Assert.Throws<SecretNotFoundException>(() =>
            missingKeyProtector.Unprotect(envelope, SensitiveDataFixtures.EnvelopeKey, Accessor));
    }

    [Fact]
    public static void MalformedKeyFailsClosedOnProtect()
    {
        var protector = CreateProtector("not-base64!");

        var ex = Assert.Throws<SensitiveDataEncryptionException>(() =>
            protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, Accessor));

        Assert.DoesNotContain(CustomerName, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public static void WrongLengthKeyFailsClosed()
    {
        var shortKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("short"));
        var protector = CreateProtector(shortKeyBase64);

        var ex = Assert.Throws<SensitiveDataEncryptionException>(() =>
            protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, Accessor));

        Assert.DoesNotContain(CustomerName, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public static void TamperedCiphertextFailsIntegrityCheckWithoutPlaintext()
    {
        var protector = CreateProtector(SensitiveDataFixtures.ValidKeyBase64);
        var envelope = protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, Accessor);
        var tamperedBytes = (byte[])envelope.Ciphertext.Ciphertext.Clone();
        tamperedBytes[0] ^= 0xFF;
        var tamperedEnvelope = envelope with
        {
            Ciphertext = envelope.Ciphertext with { Ciphertext = tamperedBytes },
        };

        var ex = Assert.Throws<SensitiveDataEncryptionException>(() =>
            protector.Unprotect(tamperedEnvelope, SensitiveDataFixtures.EnvelopeKey, Accessor));

        Assert.DoesNotContain(CustomerName, ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ORD-1001", ex.ToString(), StringComparison.Ordinal);
    }

    private static SensitivePayloadProtector CreateProtector(
        string? base64Key = null,
        InMemorySecretProvider? provider = null)
    {
        var keyProvider = provider ?? SensitiveDataFixtures.CreateKeyProvider(base64Key);
        var cipher = SensitiveDataFixtures.CreateCipher(keyProvider);
        var policy = new SensitiveDataFixtures.AllowByAccessorSensitiveAccessPolicy(
            new[] { Accessor });
        return new SensitivePayloadProtector(cipher, policy);
    }
}
