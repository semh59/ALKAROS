using System.Text.Json;
using System.Text.Json.Nodes;

namespace ALKAROS.Observability.Foundation;

/// <summary>
/// Redaction hook contract for sanitizing sensitive PII/credential data before logging or persistence (V1-OBS-001, V0-CMP-003, V1-SEC-002).
/// </summary>
public interface IRedactionHook
{
    /// <summary>
    /// Redacts sensitive keys and values from a JSON string.
    /// </summary>
    string RedactJson(string? json);

    /// <summary>
    /// Checks if a property key name represents a sensitive field requiring redaction.
    /// </summary>
    bool IsSensitiveKey(string key);
}

/// <summary>
/// Observability redaction implementation enforcing KVKK / PCI sensitive data protection rules (V1-OBS-001).
/// </summary>
public sealed class ObservabilityRedactionHook : IRedactionHook
{
    public const string RedactedPlaceholder = "***REDACTED***";

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "pwd",
        "secret",
        "client_secret",
        "token",
        "auth_token",
        "access_token",
        "refresh_token",
        "api_key",
        "apikey",
        "private_key",
        "credential",
        "credentials",
        "pan",
        "cvv",
        "cvv2",
        "card_number",
        "cardnumber",
        "pin",
        "tc_kimlik",
        "tckn",
        "identity_no",
        "tax_id"
    };

    public bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        var cleanKey = key.Trim();
        if (SensitiveKeys.Contains(cleanKey)) return true;

        var lowerKey = cleanKey.ToLowerInvariant();
        foreach (var sensitive in SensitiveKeys)
        {
            if (lowerKey.Contains(sensitive, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public string RedactJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "{}";

        try
        {
            var node = JsonNode.Parse(json);
            if (node is null)
                return "{}";

            RedactNode(node);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            // If raw text is not valid JSON, return safe placeholder
            return $"{{\"raw_message\":\"{RedactedPlaceholder}\"}}";
        }
    }

    private void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var propertyNames = obj.Select(kvp => kvp.Key).ToList();
            foreach (var propName in propertyNames)
            {
                if (IsSensitiveKey(propName))
                {
                    obj[propName] = RedactedPlaceholder;
                }
                else if (obj[propName] is not null)
                {
                    RedactNode(obj[propName]!);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                {
                    RedactNode(item);
                }
            }
        }
    }
}
