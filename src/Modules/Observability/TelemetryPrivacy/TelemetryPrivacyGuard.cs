using System.Text.RegularExpressions;

namespace ALKAROS.Observability.TelemetryPrivacy;

/// <summary>
/// Everything the monitoring system stores passes through here first. Personal and payment context (names,
/// phones, e-mail, IBAN, amounts, identifiers) and error codes are allowed to pass by product decision; secrets
/// never are: fields named like a secret are dropped, and values that look like a card number, a bearer or
/// JWT token or a long secret string are replaced.
/// </summary>
public static class TelemetryPrivacyGuard
{
    public const string Redacted = "[gizlendi]";
    public const int MaxValueLength = 200;

    /// <summary>Field names that carry secrets; such fields are never stored, whatever their value.</summary>
    private static readonly Regex ForbiddenField = new(
        @"pass(word|wd)?|secret|token|cvv|cvc|card_?(number|no)|\bpan\b|authorization|cookie|api_?key|private_?key",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BearerOrJwt = new(
        @"\bBearer\s+[A-Za-z0-9._\-]{8,}|\beyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Digits = new(@"\d[\d \-]{9,}\d", RegexOptions.Compiled);

    /// <summary>Drops secret-named fields; secret-looking values are replaced and long values clipped.</summary>
    public static IReadOnlyDictionary<string, string> Sanitize(IReadOnlyDictionary<string, string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in fields)
        {
            if (value is null || ForbiddenField.IsMatch(key))
                continue;
            var clean = LooksSensitive(value) ? Redacted : value;
            result[key] = clean.Length > MaxValueLength ? clean[..MaxValueLength] : clean;
        }

        return result;
    }

    public static bool LooksSensitive(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        if (BearerOrJwt.IsMatch(value))
            return true;
        foreach (Match match in Digits.Matches(value))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (IsCardNumber(digits))
                return true;
        }

        return false;
    }

    private static bool IsCardNumber(string d)
    {
        if (d.Length is < 13 or > 19)
            return false;
        var sum = 0;
        var alternate = false;
        for (var i = d.Length - 1; i >= 0; i--)
        {
            var n = d[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
