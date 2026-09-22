namespace ALKAROS.Observability.StructuredLogging;

/// <summary>
/// One structured log entry: correlation/request/actor/provider-reference
/// fields plus an already-redacted payload (V15-OBS-001, PDF:II.2.25). The
/// payload is redacted before this record is built, so no raw sensitive
/// value is ever carried by it.
/// </summary>
public sealed record StructuredLogEvent(
    string EventName,
    LogSeverity Severity,
    string CorrelationId,
    string RequestId,
    string? Actor,
    string? ProviderReference,
    string RedactedPayloadJson,
    DateTimeOffset Timestamp);
