using System.Text;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using ALKAROS.SensitiveData.Tests.Fixtures;
using Xunit;

namespace ALKAROS.SensitiveData.Tests.Protection;

/// <summary>
/// Tests for the protection flow: plaintext never reaches persistence and
/// an authorized accessor round-trips the payload exactly.
/// </summary>
public static class EnvelopeProtectionTests
{
    private const string Accessor = "BillingModule";

    private static readonly SensitivePayload SamplePayload = new(
        new Dictionary<string, string>
        {
            ["order-id"] = "ORD-1001",
            ["customer-name"] = "Alice Customer",
            ["card-number"] = "4111111111111111",
            ["card-holder"] = "ALICE CUSTOMER",
        },
        new Dictionary<string, SensitiveCategory>
        {
            ["order-id"] = SensitiveCategory.Public,
            ["customer-name"] = SensitiveCategory.Pii,
            ["card-number"] = SensitiveCategory.Payment,
            ["card-holder"] = SensitiveCategory.Pii,
        });

    [Fact]
    public static void ProtectProducesEnvelopeWithoutAnyPlaintextValue()
    {
        var protector = CreateProtector();

        var envelope = protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, Accessor);

        var persistenceText = Encoding.UTF8.GetString(envelope.ToPersistenceBytes());
        Assert.DoesNotContain("Alice Customer", persistenceText, StringComparison.Ordinal);
        Assert.DoesNotContain("4111111111111111", persistenceText, StringComparison.Ordinal);
        Assert.DoesNotContain("ALICE CUSTOMER", persistenceText, StringComparison.Ordinal);
    }

    [Fact]
    public static void ProtectThenUnprotectReturnsTheOriginalPayload()
    {
        var protector = CreateProtector();

        var envelope = protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, Accessor);
        var restored = protector.Unprotect(envelope, SensitiveDataFixtures.EnvelopeKey, Accessor);

        Assert.Equal(SamplePayload.Fields, restored.Fields);
        Assert.Equal(SamplePayload.Categories, restored.Categories);
    }

    [Fact]
    public static void EnvelopeSurvivesPersistenceRoundTrip()
    {
        var protector = CreateProtector();

        var envelope = protector.Protect(SamplePayload, SensitiveDataFixtures.EnvelopeKey, Accessor);
        var restoredEnvelope = SensitiveEnvelope.FromPersistenceBytes(envelope.ToPersistenceBytes());
        var restored = protector.Unprotect(restoredEnvelope, SensitiveDataFixtures.EnvelopeKey, Accessor);

        Assert.Equal(SamplePayload.Fields, restored.Fields);
        Assert.Equal(SamplePayload.Categories, restored.Categories);
    }

    [Fact]
    public static void PayloadRejectsFieldWithoutClassification()
    {
        Assert.Throws<ArgumentException>(() => new SensitivePayload(
            new Dictionary<string, string> { ["card-number"] = "4111111111111111" },
            new Dictionary<string, SensitiveCategory>
            {
                ["other-field"] = SensitiveCategory.Public,
            }));
    }

    [Fact]
    public static void FromPersistenceBytesRejectsMalformedBytesWithTypedException()
    {
        var malformed = Encoding.UTF8.GetBytes("{ not valid json");

        Assert.Throws<SensitiveDataException>(() =>
            SensitiveEnvelope.FromPersistenceBytes(malformed));
    }

    [Fact]
    public static void FromPersistenceBytesRejectsNullJsonWithTypedException()
    {
        Assert.Throws<SensitiveDataException>(() =>
            SensitiveEnvelope.FromPersistenceBytes(Encoding.UTF8.GetBytes("null")));
    }

    private static SensitivePayloadProtector CreateProtector()
    {
        var cipher = SensitiveDataFixtures.CreateCipher(SensitiveDataFixtures.CreateKeyProvider(
            SensitiveDataFixtures.ValidKeyBase64));
        var policy = new SensitiveDataFixtures.AllowByAccessorSensitiveAccessPolicy(
            new[] { Accessor });
        return new SensitivePayloadProtector(cipher, policy);
    }
}
