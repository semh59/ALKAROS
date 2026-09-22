using ALKAROS.Observability.StructuredLogging;

namespace ALKAROS.Operations.RestoreVerification.Tests.Fixtures;

/// <summary>
/// Records every emitted event for assertions, without touching any real
/// sink. Enforces <see cref="EventNameConvention"/> the same way the real
/// <c>StructuredEventLogger</c> does (V15-OBS-001), so an invalid event name
/// fails here exactly as it would against the real DI graph.
/// </summary>
public sealed class RecordingStructuredEventLogger : IStructuredEventLogger
{
    public List<(string EventName, LogSeverity Severity)> Emitted { get; } = [];

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

        Emitted.Add((eventName, severity));
    }
}
