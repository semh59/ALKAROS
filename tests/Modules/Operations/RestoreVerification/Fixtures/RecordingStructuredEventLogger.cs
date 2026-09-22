using ALKAROS.Observability.StructuredLogging;

namespace ALKAROS.Operations.RestoreVerification.Tests.Fixtures;

/// <summary>Records every emitted event for assertions, without touching any real sink.</summary>
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
        Emitted.Add((eventName, severity));
    }
}
