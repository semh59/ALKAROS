namespace ALKAROS.Observability.StructuredLogging;

/// <summary>
/// Decides whether a structured log event should actually reach the sink,
/// bounding how often a high-volume event name is emitted within a time
/// window (V15-OBS-001). Sampling never drops the first occurrence of an
/// event name in a window, so a rare or critical event is never silently
/// lost — only repeated occurrences of the same name within the same window
/// are capped.
/// </summary>
public interface IEventSampler
{
    bool ShouldEmit(string eventName, DateTimeOffset now);
}
