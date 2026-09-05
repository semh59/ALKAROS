using System.Text.Json;
using System.Text.Json.Nodes;
using ALKAROS.Audit.EventStore;
using Xunit;

namespace ALKAROS.Audit.Tests.EventStore;

public sealed class AuditSanitizerTests
{
    private readonly AuditSanitizer _sanitizer = new();

    [Fact]
    public void SanitizeJsonValidJsonWithSensitiveKeysRedactsSensitiveValues()
    {
        var input = """
        {
            "username": "admin",
            "password": "SuperSecretPassword123!",
            "apiKey": "ak_live_abcdef123456",
            "metadata": {
                "creditCardPan": "1234567890123456",
                "normalField": "public_data"
            }
        }
        """;

        var sanitized = _sanitizer.SanitizeJson(input);

        Assert.NotNull(sanitized);
        var node = JsonNode.Parse(sanitized);
        Assert.NotNull(node);
        Assert.Equal("admin", node["username"]?.GetValue<string>());
        Assert.Equal("[REDACTED]", node["password"]?.GetValue<string>());
        Assert.Equal("[REDACTED]", node["apiKey"]?.GetValue<string>());
        Assert.Equal("[REDACTED]", node["metadata"]?["creditCardPan"]?.GetValue<string>());
        Assert.Equal("public_data", node["metadata"]?["normalField"]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeJsonDoesNotFalselyRedactWordsThatSubstringMatchASensitivePattern()
    {
        // Regression test for an independent audit finding (2026-09-05, H3):
        // IsSensitiveKey used to do a raw substring match, so "pin" matched
        // inside "shipping" and "pan" matched inside "company"/"expansion",
        // wrongly redacting unrelated fields and destroying audit-trail
        // usefulness.
        var input = """
        {
            "shipping_address": "123 Main St",
            "company_name": "Acme Corp",
            "expansion_plan": "grow to Izmir",
            "mapping_table": "sku-to-category",
            "spinner_color": "blue"
        }
        """;

        var sanitized = _sanitizer.SanitizeJson(input);

        Assert.NotNull(sanitized);
        var node = JsonNode.Parse(sanitized);
        Assert.NotNull(node);
        Assert.Equal("123 Main St", node["shipping_address"]?.GetValue<string>());
        Assert.Equal("Acme Corp", node["company_name"]?.GetValue<string>());
        Assert.Equal("grow to Izmir", node["expansion_plan"]?.GetValue<string>());
        Assert.Equal("sku-to-category", node["mapping_table"]?.GetValue<string>());
        Assert.Equal("blue", node["spinner_color"]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeJsonRedactsASecretEmbeddedInAStringValueUnderANonSensitiveKey()
    {
        // Regression test for an independent audit finding (2026-09-05, H2):
        // SanitizeNode used to redact by property name only, so a secret
        // embedded in a string VALUE under an innocuous key (free text like
        // an exception message) passed into the audit store verbatim on
        // well-formed JSON.
        var input = """
        {
            "detail": "auth failed for token=eyJhbGciOiJIUzI1NiJ9.secret",
            "note": "pin: 4821 was rejected"
        }
        """;

        var sanitized = _sanitizer.SanitizeJson(input);

        Assert.NotNull(sanitized);
        var node = JsonNode.Parse(sanitized);
        Assert.NotNull(node);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", node["detail"]?.GetValue<string>());
        Assert.DoesNotContain("4821", node["note"]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeJsonMalformedJsonUsesFallbackSanitizationAndReturnsValidJson()
    {
        var malformed = """
        NOT_VALID_JSON password: secret123, token=my_secret_token, user=john
        """;

        var sanitized = _sanitizer.SanitizeJson(malformed);

        Assert.NotNull(sanitized);
        // Must be parseable valid JSON
        var node = JsonNode.Parse(sanitized);
        Assert.NotNull(node);
        Assert.True(node["unparsed"]?.GetValue<bool>());
        var rawPayload = node["raw_payload"]?.GetValue<string>();
        Assert.NotNull(rawPayload);
        Assert.DoesNotContain("secret123", rawPayload);
        Assert.DoesNotContain("my_secret_token", rawPayload);
        Assert.Contains("[REDACTED]", rawPayload);
    }

    [Fact]
    public void SanitizeJsonMalformedQuotedJsonRedactsQuotedSecretValues()
    {
        var malformed = """{"password": "secret123" broken""";

        var sanitized = _sanitizer.SanitizeJson(malformed);

        Assert.NotNull(sanitized);
        var node = JsonNode.Parse(sanitized);
        Assert.NotNull(node);
        Assert.True(node["unparsed"]?.GetValue<bool>());
        var rawPayload = node["raw_payload"]?.GetValue<string>();
        Assert.NotNull(rawPayload);
        Assert.DoesNotContain("secret123", rawPayload);
        Assert.Contains("[REDACTED]", rawPayload);
    }

    [Fact]
    public void SerializeAndSanitizeComplexObjectProducesSanitizedJson()
    {
        var payload = new
        {
            SessionId = Guid.NewGuid(),
            AuthToken = "jwt.token.here",
            User = new { Name = "Zeynep", PasswordHash = "hash1234" }
        };

        var result = _sanitizer.SerializeAndSanitize(payload);

        Assert.NotNull(result);
        var node = JsonNode.Parse(result);
        Assert.NotNull(node);
        Assert.Equal("[REDACTED]", node["AuthToken"]?.GetValue<string>());
        Assert.Equal("[REDACTED]", node["User"]?["PasswordHash"]?.GetValue<string>());
        Assert.Equal("Zeynep", node["User"]?["Name"]?.GetValue<string>());
    }

    [Fact]
    public void SanitizeJsonNullOrWhitespaceReturnsNullOrEmpty()
    {
        Assert.Null(_sanitizer.SanitizeJson(null));
        Assert.Null(_sanitizer.SerializeAndSanitize<string>(null));
    }

    [Fact]
    public void SanitizeJsonMalformedUnclosedQuoteRedactsSecret()
    {
        // Closing quote is missing — the secret must still be redacted
        var malformed = """{"password": "secret123 broken""";

        var sanitized = _sanitizer.SanitizeJson(malformed);

        Assert.NotNull(sanitized);
        var node = JsonNode.Parse(sanitized);
        Assert.NotNull(node);
        Assert.True(node["unparsed"]?.GetValue<bool>());
        var rawPayload = node["raw_payload"]?.GetValue<string>();
        Assert.NotNull(rawPayload);
        Assert.DoesNotContain("secret123", rawPayload);
        Assert.Contains("[REDACTED]", rawPayload);
    }

    [Fact]
    public void SanitizeJsonMalformedUnclosedTokenRedactsSecret()
    {
        var malformed = """{"token": "jwt.eyJhbGciOi""";

        var sanitized = _sanitizer.SanitizeJson(malformed);

        Assert.NotNull(sanitized);
        var node = JsonNode.Parse(sanitized);
        Assert.NotNull(node);
        var rawPayload = node["raw_payload"]?.GetValue<string>();
        Assert.NotNull(rawPayload);
        Assert.DoesNotContain("jwt.eyJhbGciOi", rawPayload);
        Assert.Contains("[REDACTED]", rawPayload);
    }

    [Fact]
    public void SanitizeJsonMalformedValueWithEscapedQuoteRedactsWholeSecret()
    {
        // The value contains a backslash-escaped quote; redaction must not stop at it.
        var malformed = """{"password": "A1\"B2C3D4", "user": "john" """;

        var sanitized = _sanitizer.SanitizeJson(malformed);

        Assert.NotNull(sanitized);
        var rawPayload = JsonNode.Parse(sanitized)!["raw_payload"]?.GetValue<string>();
        Assert.NotNull(rawPayload);
        Assert.DoesNotContain("B2C3D4", rawPayload);
        Assert.Contains("[REDACTED]", rawPayload);
        Assert.Contains("john", rawPayload);
    }

    [Fact]
    public void SanitizeJsonMalformedMultilineValueRedactsEveryLine()
    {
        var malformed = "{\"password\": \"line-one\nline-two-secret\nline-three\" trailing-garbage";

        var sanitized = _sanitizer.SanitizeJson(malformed);

        Assert.NotNull(sanitized);
        var rawPayload = JsonNode.Parse(sanitized)!["raw_payload"]?.GetValue<string>();
        Assert.NotNull(rawPayload);
        Assert.DoesNotContain("line-two-secret", rawPayload);
        Assert.DoesNotContain("line-one", rawPayload);
        Assert.Contains("[REDACTED]", rawPayload);
    }

    [Fact]
    public void SanitizeJsonOversizedMalformedPayloadIsFullyRedacted()
    {
        var malformed = new string('x', 70_000) + " password=leak-value token=leak-token";

        var sanitized = _sanitizer.SanitizeJson(malformed);

        Assert.NotNull(sanitized);
        var rawPayload = JsonNode.Parse(sanitized)!["raw_payload"]?.GetValue<string>();
        Assert.Equal("[REDACTED_MALFORMED_PAYLOAD]", rawPayload);
        Assert.DoesNotContain("leak-value", rawPayload);
        Assert.DoesNotContain("leak-token", rawPayload);
    }
}
