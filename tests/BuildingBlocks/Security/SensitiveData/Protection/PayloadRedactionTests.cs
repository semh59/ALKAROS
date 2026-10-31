using ALKAROS.SensitiveData;
using ALKAROS.SensitiveData.Tests.Fixtures;
using Xunit;

namespace ALKAROS.SensitiveData.Tests.Protection;

/// <summary>
/// Tests for the redaction contract: log-facing representations never carry
/// non-public field values.
/// </summary>
public static class PayloadRedactionTests
{
    private static readonly SensitivePayload SamplePayload = new(
        new Dictionary<string, string>
        {
            ["order-id"] = "ORD-1001",
            ["customer-name"] = "Alice Customer",
            ["card-number"] = "4111111111111111",
        },
        new Dictionary<string, SensitiveCategory>
        {
            ["order-id"] = SensitiveCategory.Public,
            ["customer-name"] = SensitiveCategory.Pii,
            ["card-number"] = SensitiveCategory.Payment,
        });

    [Fact]
    public static void RedactMasksNonPublicFieldsAndKeepsPublicFields()
    {
        var redactor = new PayloadRedactor();

        var redacted = redactor.Redact(SamplePayload);

        Assert.Equal("ORD-1001", redacted["order-id"]);
        Assert.Equal("***", redacted["customer-name"]);
        Assert.Equal("***", redacted["card-number"]);
    }

    [Fact]
    public static void PayloadClassificationCannotBeChangedByDowncastingTheDictionaries()
    {
        Assert.False(SamplePayload.Categories is Dictionary<string, SensitiveCategory>);
        Assert.False(SamplePayload.Fields is Dictionary<string, string>);
        Assert.Equal(SensitiveCategory.Payment, SamplePayload.Categories["card-number"]);
    }

    [Fact]
    public static void EnvelopeClassificationCannotBeChangedByDowncastingTheDictionary()
    {
        var envelope = new SensitiveEnvelope(
            new Dictionary<string, SensitiveCategory> { ["card-number"] = SensitiveCategory.Payment },
            new EnvelopeCiphertext("key", [1], [2], [3]),
            DateTimeOffset.UnixEpoch);

        Assert.False(envelope.FieldCategories is Dictionary<string, SensitiveCategory>);
        Assert.Equal(SensitiveCategory.Payment, envelope.FieldCategories["card-number"]);
    }

    [Fact]
    public static void ChangingTheSourceDictionaryAfterConstructionDoesNotChangeThePayload()
    {
        var fields = new Dictionary<string, string> { ["card-number"] = "4111111111111111" };
        var categories = new Dictionary<string, SensitiveCategory> { ["card-number"] = SensitiveCategory.Payment };
        var payload = new SensitivePayload(fields, categories);

        categories["card-number"] = SensitiveCategory.Public;
        fields["card-number"] = "changed";

        Assert.Equal(SensitiveCategory.Payment, payload.Categories["card-number"]);
        Assert.Equal("4111111111111111", payload.Fields["card-number"]);
    }

    [Fact]
    public static void RedactedLogLineContainsNoSensitiveValue()
    {
        var redactor = new PayloadRedactor();

        var logLine = string.Join("; ",
            redactor.Redact(SamplePayload).Select(pair => $"{pair.Key}={pair.Value}"));

        Assert.Contains("order-id=ORD-1001", logLine, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice Customer", logLine, StringComparison.Ordinal);
        Assert.DoesNotContain("4111111111111111", logLine, StringComparison.Ordinal);
    }
}
