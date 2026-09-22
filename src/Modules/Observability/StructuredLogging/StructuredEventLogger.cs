using System.Collections;
using System.Text.Json;
using ALKAROS.Observability.Foundation;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Observability.StructuredLogging;

/// <summary>
/// Default <see cref="IStructuredEventLogger"/>. Reads the ambient
/// <see cref="CorrelationContext"/> for correlation/request/user fields,
/// redacts the payload via <see cref="IRedactionHook"/>, applies
/// <see cref="IEventSampler"/> before touching the sink, and writes through
/// the standard <see cref="ILogger"/> pipeline as a structured state object
/// so any provider (console, OpenTelemetry, ...) can extract the named
/// fields, not just the rendered message text (V15-OBS-001).
/// </summary>
public sealed class StructuredEventLogger : IStructuredEventLogger
{
    private readonly ILogger<StructuredEventLogger> _logger;
    private readonly IRedactionHook _redactionHook;
    private readonly IEventSampler _sampler;
    private readonly TimeProvider _timeProvider;

    public StructuredEventLogger(
        ILogger<StructuredEventLogger> logger,
        IRedactionHook redactionHook,
        IEventSampler sampler,
        TimeProvider? timeProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redactionHook = redactionHook ?? throw new ArgumentNullException(nameof(redactionHook));
        _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

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

        var level = ToLogLevel(severity);
        if (!_logger.IsEnabled(level))
            return;

        var now = _timeProvider.GetUtcNow();
        if (!_sampler.ShouldEmit(eventName, now))
            return;

        var correlation = CorrelationContext.Current;
        var payloadJson = payload is null || payload.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(payload);
        var redactedPayload = _redactionHook.RedactJson(payloadJson);

        var logEvent = new StructuredLogEvent(
            eventName,
            severity,
            correlation?.CorrelationId ?? CorrelationContext.CorrelationId,
            correlation?.RequestId ?? CorrelationContext.RequestId,
            actor ?? correlation?.UserId?.ToString(),
            providerReference,
            redactedPayload,
            now);

        _logger.Log(
            level,
            new EventId(0, logEvent.EventName),
            new StructuredLogState(logEvent),
            exception: null,
            formatter: static (state, _) => Format(state.Event));
    }

    private static string Format(StructuredLogEvent state) =>
        $"{state.EventName} correlationId={state.CorrelationId} requestId={state.RequestId} " +
        $"actor={state.Actor ?? "-"} provider={state.ProviderReference ?? "-"} payload={state.RedactedPayloadJson}";

    private static LogLevel ToLogLevel(LogSeverity severity) => severity switch
    {
        LogSeverity.Info => LogLevel.Information,
        LogSeverity.Warning => LogLevel.Warning,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Critical => LogLevel.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

    /// <summary>
    /// Exposes a <see cref="StructuredLogEvent"/>'s fields as named
    /// key/value pairs — the same shape <c>LoggerMessage.Define</c>'s own
    /// generated state uses — so structured-logging providers (and this
    /// module's own tests) can read individual fields instead of only the
    /// rendered message string.
    /// </summary>
    internal sealed class StructuredLogState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly KeyValuePair<string, object?>[] _fields;

        public StructuredLogState(StructuredLogEvent logEvent)
        {
            Event = logEvent;
            _fields =
            [
                new("EventName", logEvent.EventName),
                new("CorrelationId", logEvent.CorrelationId),
                new("RequestId", logEvent.RequestId),
                new("Actor", logEvent.Actor),
                new("ProviderReference", logEvent.ProviderReference),
                new("Payload", logEvent.RedactedPayloadJson),
                new("{OriginalFormat}", logEvent.EventName),
            ];
        }

        public StructuredLogEvent Event { get; }

        public KeyValuePair<string, object?> this[int index] => _fields[index];
        public int Count => _fields.Length;
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<string, object?>>)_fields).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
