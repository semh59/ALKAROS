namespace ALKAROS.Audit.EventStore;

using System.Text.Json;
using System.Text.Json.Nodes;

public interface IAuditSanitizer
{
    string? SanitizeJson(string? rawJson);
    string? SerializeAndSanitize<T>(T? payload);
}

/// <summary>
/// Redacts sensitive personal and security fields (passwords, PINs, secrets, payment tokens)
/// from audit payloads before persistence (PDF:II.9, PDF:III.24, V0-CMP-003).
/// </summary>
public sealed class AuditSanitizer : IAuditSanitizer
{
    private static readonly string[] SensitiveSubstrings =
    [
        "password",
        "passphrase",
        "pin",
        "secret",
        "token",
        "cvv",
        "cvc",
        "pan",
        "cardnumber",
        "creditcard",
        "salt",
        "jwt",
        "apikey"
    ];

    // IsSensitiveKey matches these as whole identifier tokens (split on `_`,
    // `-`, and camelCase boundaries), not a raw substring — found by an
    // independent audit (2026-09-05, H3): the old Contains()-based check
    // falsely redacted shipping_address ("pin" inside "shiPINg"),
    // company_name / expansion_plan ("pan" inside "comPANy" / "exPANsion").
    private static readonly HashSet<string> SensitiveKeyTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "passphrase", "pin", "secret", "token", "cvv", "cvc", "pan", "salt", "jwt"
        };

    // These are two real words concatenated, so they stay substring-matched
    // (separators stripped) rather than whole-token — token-splitting would
    // separate "card"+"number" or "api"+"key", and neither half alone is
    // sensitive on its own.
    private static readonly string[] SensitiveCompoundKeySubstrings =
        ["cardnumber", "creditcard", "apikey"];

    private static readonly System.Text.RegularExpressions.Regex CamelCaseBoundary =
        new(@"(?<=[a-z0-9])(?=[A-Z])", System.Text.RegularExpressions.RegexOptions.Compiled);

    public string? SanitizeJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node == null)
                return null;

            SanitizeNode(node);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            var sanitized = FallbackSanitizeText(rawJson);
            return JsonSerializer.Serialize(new { raw_payload = sanitized, unparsed = true });
        }
    }

    // An audit event payload is expected to be small. Beyond this bound a
    // malformed blob cannot be redacted reliably token by token, so the whole
    // body is replaced instead of risking a partial secret leak.
    private const int MaxFallbackLength = 64 * 1024;
    private const string FullyRedactedMarker = "[REDACTED_MALFORMED_PAYLOAD]";

    private static string FallbackSanitizeText(string text)
    {
        if (text.Length > MaxFallbackLength)
            return FullyRedactedMarker;

        return RedactEmbeddedSecrets(text);
    }

    // Extracted so well-formed JSON's structured path (SanitizeNode) can run
    // the same value-level regex scrubbing that used to run only on the
    // JSON-parse-failure fallback — found by an independent audit
    // (2026-09-05, H2): a secret embedded in a string VALUE under a
    // non-sensitive KEY (e.g. {"detail": "auth failed for token=eyJ..."})
    // passed into the audit store verbatim on well-formed JSON, since
    // IsSensitiveKey only ever inspected property names.
    private static string RedactEmbeddedSecrets(string text)
    {
        var sanitized = text;
        foreach (var pattern in SensitiveSubstrings)
        {
            // Quoted values: tolerate backslash-escaped quotes and embedded
            // newlines, up to the matching closing quote.
            var quotedRegex = new System.Text.RegularExpressions.Regex(
                $@"(?i)([""']?{pattern}[""']?\s*[:=]\s*)(?<q>[""'])((?:\\.|(?!\k<q>)[^\\])*)(\k<q>)",
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Singleline);
            sanitized = quotedRegex.Replace(sanitized, "$1${q}[REDACTED]$3");

            // Truncated/malformed: opening quote present but no closing quote
            // before end-of-input, including multi-line values.
            var truncatedRegex = new System.Text.RegularExpressions.Regex(
                $@"(?i)([""']?{pattern}[""']?\s*[:=]\s*)(?<q>[""'])((?:\\.|(?!\k<q>)[^\\])+)$",
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Singleline);
            sanitized = truncatedRegex.Replace(sanitized, "$1${q}[REDACTED]");

            // Unquoted values
            var unquotedRegex = new System.Text.RegularExpressions.Regex(
                $@"(?i)([""']?{pattern}[""']?\s*[:=]\s*)([^,\s""}}\]]+)",
                System.Text.RegularExpressions.RegexOptions.Compiled);
            sanitized = unquotedRegex.Replace(sanitized, "$1[REDACTED]");
        }
        return sanitized;
    }

    public string? SerializeAndSanitize<T>(T? payload)
    {
        if (payload == null)
            return null;

        var rawJson = JsonSerializer.Serialize(payload);
        return SanitizeJson(rawJson);
    }

    private static void SanitizeNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var propertyNames = obj.Select(p => p.Key).ToList();
            foreach (var propName in propertyNames)
            {
                if (IsSensitiveKey(propName))
                {
                    obj[propName] = "[REDACTED]";
                }
                else if (obj[propName] is JsonValue value && value.TryGetValue(out string? text))
                {
                    var redacted = RedactEmbeddedSecrets(text);
                    if (!string.Equals(redacted, text, StringComparison.Ordinal))
                        obj[propName] = redacted;
                }
                else
                {
                    SanitizeNode(obj[propName]);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                if (arr[i] is JsonValue value && value.TryGetValue(out string? text))
                {
                    var redacted = RedactEmbeddedSecrets(text);
                    if (!string.Equals(redacted, text, StringComparison.Ordinal))
                        arr[i] = redacted;
                }
                else
                {
                    SanitizeNode(arr[i]);
                }
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        var normalized = key.Replace("-", "").Replace("_", "").ToLowerInvariant();
        foreach (var pattern in SensitiveCompoundKeySubstrings)
        {
            if (normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var part in key.Split('_', '-'))
        {
            foreach (var token in CamelCaseBoundary.Split(part))
            {
                if (SensitiveKeyTokens.Contains(token))
                    return true;
            }
        }
        return false;
    }
}
