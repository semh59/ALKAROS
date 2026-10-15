using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ALKAROS.Observability.ErrorEvents;

public enum ErrorLayer
{
    Database, Api, Background, Printer, Payment, Kitchen, Integration, Browser, Security, Backup,
}

public enum ErrorSeverity
{
    Info = 0, Warning = 1, Error = 2, Critical = 3,
}

/// <summary>
/// One error as the detectors report it. <see cref="Category"/> is a short fixed key (e.g. "printer.offline");
/// <see cref="Detail"/> is optional free text and is cleaned by the privacy guard before it is stored.
/// </summary>
public sealed record ErrorEventInput(
    ErrorLayer Layer,
    ErrorSeverity Severity,
    string Category,
    string? Detail = null,
    string? TraceId = null,
    string? TerminalLabel = null,
    bool Synthetic = false,
    DateTimeOffset? OccurredAt = null);

/// <summary>What changed for one fingerprint in a flushed batch.</summary>
public sealed record ErrorEventChange(
    string Fingerprint,
    ErrorLayer Layer,
    ErrorSeverity Severity,
    string Category,
    string? Detail,
    bool IsNew,
    bool Escalated,
    long Occurrences);

public static class ErrorFingerprint
{
    private static readonly Regex Guid = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);
    private static readonly Regex Hex = new(@"\b[0-9a-fA-F]{12,}\b", RegexOptions.Compiled);
    private static readonly Regex Number = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex Quoted = new(@"'[^']*'|""[^""]*""", RegexOptions.Compiled);
    private static readonly Regex Space = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Same layer, category and shape of detail give the same fingerprint, whatever ids and numbers differ.</summary>
    public static string Compute(ErrorLayer layer, string category, string? detail)
    {
        var shape = Normalize(detail ?? string.Empty);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{layer}|{Normalize(category)}|{shape}"));
        return Convert.ToHexString(bytes, 0, 12).ToLowerInvariant();
    }

    internal static string Normalize(string text)
    {
        var s = Guid.Replace(text, "<id>");
        s = Quoted.Replace(s, "<s>");
        s = Hex.Replace(s, "<h>");
        s = Number.Replace(s, "<n>");
        return Space.Replace(s, " ").Trim().ToLowerInvariant();
    }
}
