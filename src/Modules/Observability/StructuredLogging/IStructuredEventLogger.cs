namespace ALKAROS.Observability.StructuredLogging;

/// <summary>
/// Emits named structured log events carrying the active correlation context
/// (V1-OBS-001's <see cref="Foundation.CorrelationContext"/>), a redacted
/// payload and an optional external provider reference (V15-OBS-001).
/// </summary>
public interface IStructuredEventLogger
{
    /// <summary>
    /// Emits <paramref name="eventName"/> (must follow
    /// <see cref="EventNameConvention"/>, e.g. "order.accepted") at
    /// <paramref name="severity"/>. <paramref name="actor"/> identifies the
    /// acting user or device; <paramref name="providerReference"/> identifies
    /// an external provider correlation (e.g. a QNB invoice id or a
    /// Token/Beko terminal id). <paramref name="payload"/> is redacted
    /// (<see cref="Foundation.IRedactionHook"/>) before it reaches the sink.
    /// Subject to sampling (<see cref="IEventSampler"/>): a call may be a
    /// silent no-op if the event name's window limit was already reached.
    /// </summary>
    void Emit(
        string eventName,
        LogSeverity severity,
        string? actor = null,
        string? providerReference = null,
        IReadOnlyDictionary<string, object?>? payload = null);
}
