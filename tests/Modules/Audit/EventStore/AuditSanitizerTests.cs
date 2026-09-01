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
