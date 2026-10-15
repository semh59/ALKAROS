using System.Text.RegularExpressions;

namespace ALKAROS.Observability.TelemetryPrivacy;

/// <summary>
/// Everything the monitoring system stores passes through here first: only fields on the allowlist survive,
/// and a value that looks like personal or payment data is replaced, never stored.
/// </summary>
public static class TelemetryPrivacyGuard
{
    public const string Redacted = "[gizlendi]";
    public const int MaxValueLength = 200;

    public static readonly IReadOnlySet<string> AllowedFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "layer", "severity", "category", "action_key", "route_key", "status_code", "duration_ms",
        "terminal_label", "role_code", "trace_id", "occurred_at", "count", "event_id", "error_type",
    };

    private static readonly Regex Email = new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex Iban = new(@"\bTR\d{2}[ ]?(?:\d{4}[ ]?){5}\d{2}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Phone = new(@"(?<!\d)(?:\+?90|0)?[ ]?\(?5\d{2}\)?[ ]?\d{3}[ ]?\d{2}[ ]?\d{2}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex Digits = new(@"\d[\d \-]{9,}\d", RegexOptions.Compiled);

    /// <summary>Returns only allowlisted fields; sensitive-looking values are replaced and long values clipped.</summary>
    public static IReadOnlyDictionary<string, string> Sanitize(IReadOnlyDictionary<string, string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in fields)
        {
            if (!AllowedFields.Contains(key) || value is null)
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
        if (Email.IsMatch(value) || Iban.IsMatch(value) || Phone.IsMatch(value))
            return true;
        foreach (Match match in Digits.Matches(value))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (IsTurkishId(digits) || IsCardNumber(digits))
                return true;
        }

        return false;
    }

    private static bool IsTurkishId(string d)
    {
        if (d.Length != 11 || d[0] == '0')
            return false;
        var v = d.Select(c => c - '0').ToArray();
        var tenth = ((v[0] + v[2] + v[4] + v[6] + v[8]) * 7 - (v[1] + v[3] + v[5] + v[7])) % 10;
        if (tenth < 0) tenth += 10;
        return tenth == v[9] && v.Take(10).Sum() % 10 == v[10];
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
