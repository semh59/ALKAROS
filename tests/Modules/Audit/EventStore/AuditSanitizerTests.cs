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

public sealed class AuditSanitizerHardeningTests
{
    private readonly AuditSanitizer _sanitizer = new();

    [Theory]
    [InlineData("passwords")]
    [InlineData("tokens")]
    [InlineData("accessToken")]
    [InlineData("access_token")]
    [InlineData("refreshTokens")]
    [InlineData("PINCode")]
    [InlineData("JWTToken")]
    [InlineData("pwd")]
    [InlineData("otp")]
    [InlineData("iban")]
    [InlineData("cardNo")]
    [InlineData("privateKey")]
    [InlineData("authorization")]
    [InlineData("secretkey")]
    [InlineData("api-key")]
    [InlineData("connectionString")]
    [InlineData("secret")]
    [InlineData("credential")]
    [InlineData("password")]
    [InlineData("token")]
    [InlineData("pin")]
    [InlineData("pan")]
    [InlineData("userPin")]
    [InlineData("secrets")]
    [InlineData("pins")]
    [InlineData("pans")]
    [InlineData("cookie")]
    [InlineData("credentials")]
    [InlineData("tckn")]
    [InlineData("bearer")]
    [InlineData("passphrase")]
    [InlineData("cvv")]
    [InlineData("cvc")]
    [InlineData("salt")]
    [InlineData("jwt")]
    [InlineData("cardnumber")]
    [InlineData("creditcard")]
    [InlineData("authtoken")]
    [InlineData("idtoken")]
    [InlineData("refreshtoken")]
    [InlineData("accesstoken")]
    [InlineData("apiKey")]
    [InlineData("cookies")]
    public void SanitizeJsonRedactsEverySensitiveKeyShape(string key)
    {
        var json = new JsonObject { [key] = "leak-value-1" }.ToJsonString();

        var sanitized = _sanitizer.SanitizeJson(json);

        Assert.DoesNotContain("leak-value-1", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
    }

    [Fact]
    public void SanitizeJsonRedactsSensitiveValuesInsideArraysOfObjects()
    {
        var sanitized = _sanitizer.SanitizeJson("""{"items":[{"token":"leak-a"},{"ok":"fine"}]}""");

        Assert.DoesNotContain("leak-a", sanitized);
        Assert.Contains("fine", sanitized);
    }

    [Fact]
    public void SanitizeJsonDoesNotThrowOnDuplicateKeysAndStillRedacts()
    {
        var sanitized = _sanitizer.SanitizeJson("""{"token":"leak-a","token":"leak-b","name":"x"}""");

        Assert.NotNull(sanitized);
        Assert.DoesNotContain("leak-a", sanitized);
        Assert.DoesNotContain("leak-b", sanitized);
    }

    [Theory]
    [InlineData("\"token=leak-root\"", "leak-root")]
    [InlineData("\"password: leak-root\"", "leak-root")]
    public void SanitizeJsonRedactsARootStringValue(string json, string secret)
    {
        var sanitized = _sanitizer.SanitizeJson(json);

        Assert.DoesNotContain(secret, sanitized);
    }

    [Fact]
    public void SanitizeJsonLeavesAPlainRootNumberAndBooleanAlone()
    {
        Assert.Equal("42", _sanitizer.SanitizeJson("42"));
        Assert.Equal("true", _sanitizer.SanitizeJson("true"));
    }

    [Theory]
    [InlineData("Authorization: Bearer leak.jwt.value", "leak.jwt.value")]
    [InlineData("token: Bearer leak.jwt.value", "leak.jwt.value")]
    [InlineData("password: my secret phrase here", "phrase")]
    [InlineData("login failed, password=leak-pw, retry", "leak-pw")]
    public void SanitizeJsonRedactsTheWholeSecretInFreeText(string text, string secret)
    {
        var sanitized = _sanitizer.SanitizeJson(new JsonObject { ["detail"] = text }.ToJsonString());

        Assert.DoesNotContain(secret, sanitized);
    }

    [Theory]
    [InlineData("Trip to Japan: 3 days")]
    [InlineData("spin=5 and basalt: rock")]
    [InlineData("salty: very, panel: wide")]
    public void SanitizeJsonDoesNotRedactWordsThatOnlyContainASensitiveWord(string text)
    {
        var sanitized = _sanitizer.SanitizeJson(new JsonObject { ["note"] = text }.ToJsonString());

        var note = JsonNode.Parse(sanitized!)!["note"]!.GetValue<string>();
        Assert.Equal(text, note);
    }

    [Fact]
    public void SanitizeJsonKeepsUnrelatedFreeTextAfterAQuotedSecret()
    {
        var sanitized = _sanitizer.SanitizeJson("""{"detail":"password=\"abc\" then continue normally"}""");

        Assert.DoesNotContain("abc", sanitized);
        Assert.Contains("then continue normally", sanitized);
    }

    [Fact]
    public void SanitizeJsonLeavesAnEmptyValueAfterASensitiveKeyAlone()
    {
        var sanitized = _sanitizer.SanitizeJson("{\"detail\":\"token=\"}");

        Assert.Equal("token=", JsonNode.Parse(sanitized!)!["detail"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("{\"a\":1,\"a\":2,\"token\":123,\"keep\":\"ok\"}", "{\"a\":1,\"a\":2,\"token\":[REDACTED],\"keep\":\"ok\"}")]
    [InlineData("{\"a\":1,\"a\":2,\"token\":123}", "{\"a\":1,\"a\":2,\"token\":[REDACTED]}")]
    [InlineData("[token=abc]", "[token=[REDACTED]]")]
    [InlineData("token=, other", "token=, other")]
    [InlineData("password=\"abc\" then continue", "password=\"[REDACTED]\" then continue")]
    [InlineData("password=\"a\\\"b\" then continue", "password=\"[REDACTED]\" then continue")]
    [InlineData("pin=\"unterminated", "pin=\"[REDACTED]")]
    public void SanitizeJsonScrubsMalformedTextWithExactBoundaries(string malformed, string expected)
    {
        var sanitized = _sanitizer.SanitizeJson(malformed);

        var raw = JsonNode.Parse(sanitized!)!["raw_payload"]!.GetValue<string>();
        Assert.Equal(expected, raw);
    }

    [Fact]
    public void SanitizeJsonStopsAnUnquotedSecretAtTheEndOfTheLine()
    {
        var sanitized = _sanitizer.SanitizeJson(new JsonObject { ["detail"] = "token=abc\nnext line" }.ToJsonString());

        Assert.Equal("token=[REDACTED]\nnext line", JsonNode.Parse(sanitized!)!["detail"]!.GetValue<string>());
    }

    [Fact]
    public void SanitizeJsonStillRedactsAnEmbeddedSecretUnderAnInnocentKey()
    {
        var sanitized = _sanitizer.SanitizeJson("""{"detail":"auth failed for token=eyJleak"}""");

        Assert.DoesNotContain("eyJleak", sanitized);
    }
}
