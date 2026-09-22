using ALKAROS.Observability.StructuredLogging;

namespace ALKAROS.Operations.OffsiteBackup.Tests.Fixtures;

/// <summary>
/// In-memory IStructuredEventLogger test double. Enforces
/// <see cref="EventNameConvention"/> the same way the real
/// <c>StructuredEventLogger</c> does (V15-OBS-001), so a call site emitting
/// an invalid event name fails here exactly as it would against the real DI
/// graph, instead of silently recording it.
/// </summary>
public sealed class RecordingStructuredEventLogger : IStructuredEventLogger
{
    private readonly List<(string EventName, LogSeverity Severity)> _events = [];

    public IReadOnlyList<(string EventName, LogSeverity Severity)> Events => _events;

    public void Emit(
        string eventName,
        LogSeverity severity,
        string? actor = null,
        string? providerReference = null,
        IReadOnlyDictionary<string, object?>? payload = null)
    {
        if (!EventNameConvention.IsValid(eventName))
        {
            throw new ArgumentException(
                $"Event name '{eventName}' must follow the dotted lowercase convention (e.g. 'order.accepted').",
                nameof(eventName));
        }

        _events.Add((eventName, severity));
    }
}
