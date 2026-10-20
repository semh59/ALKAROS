namespace ALKAROS.Audit.EventStore;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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
    // IsSensitiveKey matches these as whole identifier tokens (split on `_`,
    // `-`, and camelCase boundaries), not a raw substring: a raw substring
    // match falsely redacted shipping_address ("pin" inside "shiPINg") and
    // company_name ("pan" inside "comPANy"). Plurals and short forms are listed
    // explicitly because tokens are compared whole.
    private static readonly HashSet<string> SensitiveKeyTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "passwords", "passphrase", "pin", "pins", "secret", "secrets", "token", "tokens",
            "cvv", "cvc", "pan", "pans", "salt", "jwt", "pwd", "otp", "iban", "authorization", "bearer",
            "cookie", "cookies", "credential", "credentials", "tckn"
        };

    // Run-together words (and acronym-led names the camelCase split cannot
    // separate) stay substring-matched on the key with separators stripped.
    private static readonly string[] SensitiveCompoundKeySubstrings =
    [
        "cardnumber", "creditcard", "cardno", "apikey", "accesstoken", "refreshtoken", "authtoken",
        "idtoken", "jwttoken", "secretkey", "privatekey", "connectionstring", "pincode"
    ];

    private static readonly Regex CamelCaseBoundary =
        new(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

    // "key" followed by a ':' or '=' separator, optionally with a closing quote
    // on the key (JSON text). The value is consumed by hand so that a
    // non-sensitive key never swallows a sensitive pair that follows it.
    private static readonly Regex KeySeparator =
        new(@"(?<key>[A-Za-z0-9_\-]+)[""']?\s*[:=]\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string RedactedMarker = "[REDACTED]";

    public string? SanitizeJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node == null)
                return null;

            if (node is JsonValue rootValue && rootValue.TryGetValue(out string? rootText))
                return JsonValue.Create(RedactEmbeddedSecrets(rootText))!.ToJsonString();

            SanitizeNode(node);
            return node.ToJsonString();
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // ArgumentException: System.Text.Json accepts duplicate property names
            // at parse time and fails when the tree is walked.
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

    // Value-level scrubbing for text: a secret embedded in a string VALUE under
    // a non-sensitive KEY (e.g. {"detail": "auth failed for token=eyJ..."}) is
    // found by its own key=value shape. A quoted value is redacted up to its
    // closing quote; an unquoted value up to the next delimiter or line end, so
    // free text after a password ("password: my secret phrase") does not leak.
    private static string RedactEmbeddedSecrets(string text)
    {
        var result = new StringBuilder(text.Length);
        var position = 0;
        while (true)
        {
            var match = KeySeparator.Match(text, position);
            if (!match.Success)
                break;

            var valueStart = match.Index + match.Length;
            result.Append(text, position, valueStart - position);
            position = valueStart;
            if (IsSensitiveKey(match.Groups["key"].Value))
                position = AppendRedactedValue(text, position, result);
        }

        result.Append(text, position, text.Length - position);
        return result.ToString();
    }

    private static int AppendRedactedValue(string text, int start, StringBuilder result)
    {
        if (start >= text.Length)
            return start;

        var first = text[start];
        if (first is '"' or '\'')
        {
            var index = start + 1;
            while (index < text.Length && text[index] != first)
                index += text[index] == '\\' ? 2 : 1;

            var closed = index < text.Length;
            result.Append(first).Append(RedactedMarker);
            if (closed)
                result.Append(first);
            return closed ? index + 1 : text.Length;
        }

        var end = start;
        while (end < text.Length && text[end] is not (',' or '"' or '}' or ']' or '\r' or '\n'))
            end++;

        if (end > start)
            result.Append(RedactedMarker);
        return end;
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
