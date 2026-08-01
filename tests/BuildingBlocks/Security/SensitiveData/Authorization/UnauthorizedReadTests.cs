using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using ALKAROS.SensitiveData.Tests.Fixtures;
using Xunit;

namespace ALKAROS.SensitiveData.Tests.Authorization;

/// <summary>
/// Tests for the authorized-read boundary: a denied accessor fails before
/// decryption is ever attempted and learns nothing about the payload.
/// </summary>
public static class UnauthorizedReadTests
{
    private const string AuthorizedAccessor = "BillingModule";
    private const string UnauthorizedAccessor = "WaiterModule";
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
    public static void UnauthorizedReadThrowsAndDecryptIsNeverCalled()
    {
        var cipher = new SensitiveDataFixtures.RecordingEnvelopeCipher(
            SensitiveDataFixtures.CreateCipher(SensitiveDataFixtures.CreateKeyProvider(
                SensitiveDataFixtures.ValidKeyBase64)));
        var policy = new SensitiveDataFixtures.AllowByAccessorSensitiveAccessPolicy(
            new[] { AuthorizedAccessor });
        var protector = new SensitivePayloadProtector(cipher, policy);
        var envelope = protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, AuthorizedAccessor);

        Assert.Throws<UnauthorizedSensitiveReadException>(() =>
            protector.Unprotect(envelope, SensitiveDataFixtures.EnvelopeKey, UnauthorizedAccessor));

        Assert.Equal(0, cipher.DecryptCalls);
    }

    [Fact]
    public static void UnauthorizedReadExceptionDoesNotRevealPayloadContent()
    {
        var protector = CreateProtector();
        var envelope = protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, AuthorizedAccessor);

        var ex = Assert.Throws<UnauthorizedSensitiveReadException>(() =>
            protector.Unprotect(envelope, SensitiveDataFixtures.EnvelopeKey, UnauthorizedAccessor));

        Assert.Contains(UnauthorizedAccessor, ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(CustomerName, ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ORD-1001", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public static void AuthorizedReadDecryptsExactlyOnce()
    {
        var cipher = new SensitiveDataFixtures.RecordingEnvelopeCipher(
            SensitiveDataFixtures.CreateCipher(SensitiveDataFixtures.CreateKeyProvider(
                SensitiveDataFixtures.ValidKeyBase64)));
        var policy = new SensitiveDataFixtures.AllowByAccessorSensitiveAccessPolicy(
            new[] { AuthorizedAccessor });
        var protector = new SensitivePayloadProtector(cipher, policy);
        var envelope = protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, AuthorizedAccessor);

        var restored = protector.Unprotect(envelope, SensitiveDataFixtures.EnvelopeKey, AuthorizedAccessor);

        Assert.Equal(CustomerName, restored.Fields["customer-name"]);
        Assert.Equal(1, cipher.DecryptCalls);
    }

    private static SensitivePayloadProtector CreateProtector()
    {
        var cipher = SensitiveDataFixtures.CreateCipher(SensitiveDataFixtures.CreateKeyProvider(
            SensitiveDataFixtures.ValidKeyBase64));
        var policy = new SensitiveDataFixtures.AllowByAccessorSensitiveAccessPolicy(
            new[] { AuthorizedAccessor });
        return new SensitivePayloadProtector(cipher, policy);
    }
}
