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

    private static string FallbackSanitizeText(string text)
    {
        var sanitized = text;
        foreach (var pattern in SensitiveSubstrings)
        {
            // Quoted values with a closing quote present
            var quotedRegex = new System.Text.RegularExpressions.Regex(
                $@"(?i)([""']?{pattern}[""']?\s*[:=]\s*[""'])([^""'\r\n]+)([""'])",
                System.Text.RegularExpressions.RegexOptions.Compiled);
            sanitized = quotedRegex.Replace(sanitized, "$1[REDACTED]$3");

            // Truncated/malformed: opening quote present but no closing quote before end-of-string
            var truncatedRegex = new System.Text.RegularExpressions.Regex(
                $@"(?i)([""']?{pattern}[""']?\s*[:=]\s*[""'])([^""'\r\n]+)$",
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Multiline);
            sanitized = truncatedRegex.Replace(sanitized, "$1[REDACTED]");

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
                else
                {
                    SanitizeNode(obj[propName]);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                SanitizeNode(item);
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        var normalized = key.Replace("-", "").Replace("_", "").ToLowerInvariant();
        foreach (var pattern in SensitiveSubstrings)
        {
            if (normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
