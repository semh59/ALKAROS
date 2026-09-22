using System.Text.RegularExpressions;

namespace ALKAROS.Support.DiagnosticBundle;

/// <summary>
/// Scans already key-redacted text for values that still look like a secret
/// or a card number regardless of which JSON key carried them — a
/// defense-in-depth pass over <see cref="Observability.Foundation.IRedactionHook"/>'s
/// key-name-based redaction, which only catches a sensitive VALUE when its
/// property is named for it (V15-SUP-001).
/// </summary>
public interface ISecretPatternScanner
{
    /// <summary>
    /// Returns the input with every matched span replaced by a redaction
    /// placeholder, and whether any match was found.
    /// </summary>
    (string RedactedText, bool FoundMatch) Scan(string text);
}

public sealed partial class SecretPatternScanner : ISecretPatternScanner
{
    public const string Placeholder = "***REDACTED***";

    // 13-19 consecutive digits (optionally separated by spaces/dashes every
    // 4) covers PAN-shaped values (Luhn is deliberately not checked here:
    // a false negative on a non-Luhn-valid but still-sensitive-looking
    // digit run is worse than an occasional over-redaction).
    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b")]
    private static partial Regex CardLikeDigitRun();

    // A JWT's three base64url segments, or any other 24+ character
    // base64/hex-looking token — long enough that natural language or a
    // GUID's dashed form never matches by accident.
    [GeneratedRegex(@"\b[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b|\b[A-Za-z0-9+/]{24,}={0,2}\b")]
    private static partial Regex TokenLikeString();

    private static readonly Regex[] Patterns = [CardLikeDigitRun(), TokenLikeString()];

    public (string RedactedText, bool FoundMatch) Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = false;
        var result = text;
        foreach (var pattern in Patterns)
        {
            if (!pattern.IsMatch(result))
                continue;
            found = true;
            result = pattern.Replace(result, Placeholder);
        }

        return (result, found);
    }
}
