using System.Text.RegularExpressions;

namespace ALKAROS.Observability.StructuredLogging;

/// <summary>
/// Validates the dotted, lowercase event-naming convention (e.g. "order.accepted",
/// "payment.captured") every structured log event name must follow (V15-OBS-001).
/// </summary>
public static partial class EventNameConvention
{
    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? eventName) =>
        !string.IsNullOrWhiteSpace(eventName) && Pattern().IsMatch(eventName);
}
