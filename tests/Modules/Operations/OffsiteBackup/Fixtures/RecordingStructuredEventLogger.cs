using ALKAROS.Observability.StructuredLogging;

namespace ALKAROS.Operations.OffsiteBackup.Tests.Fixtures;

/// <summary>In-memory IStructuredEventLogger test double — StructuredEventLogger's own correctness is V15-OBS-001's concern, not this task's.</summary>
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
        _events.Add((eventName, severity));
    }
}
