using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Observability.Foundation;

namespace ALKAROS.Support.DiagnosticBundle;

public interface IDiagnosticBundleService
{
    Task<DiagnosticBundleResult> GenerateAsync(DiagnosticBundleRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Assembles a redacted, size- and time-bounded support diagnostic bundle
/// from the system's own current health status, a non-sensitive build
/// fingerprint, and the audit trail for the caller's selected correlation
/// ids (V15-SUP-001). Every field is either read-only infrastructure state
/// or passes through two independent redaction passes (key-based, then
/// value-pattern-based) before it reaches the result — this type holds no
/// mutable state of its own, so concurrent calls never interfere.
/// </summary>
public sealed class DiagnosticBundleService : IDiagnosticBundleService
{
    private readonly IObservabilityService _observabilityService;
    private readonly IRedactionHook _redactionHook;
    private readonly ISecretPatternScanner _secretScanner;
    private readonly IAuditEventStore _auditEventStore;
    private readonly TimeProvider _timeProvider;

    public DiagnosticBundleService(
        IObservabilityService observabilityService,
        IRedactionHook redactionHook,
        ISecretPatternScanner secretScanner,
        IAuditEventStore auditEventStore,
        TimeProvider? timeProvider = null)
    {
        _observabilityService = observabilityService ?? throw new ArgumentNullException(nameof(observabilityService));
        _redactionHook = redactionHook ?? throw new ArgumentNullException(nameof(redactionHook));
        _secretScanner = secretScanner ?? throw new ArgumentNullException(nameof(secretScanner));
        _auditEventStore = auditEventStore ?? throw new ArgumentNullException(nameof(auditEventStore));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DiagnosticBundleResult> GenerateAsync(
        DiagnosticBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var status = await BuildSystemStatusAsync(cancellationToken).ConfigureAwait(false);
        var version = BuildVersionFingerprint();
        var entries = await CollectLogEntriesAsync(request, cancellationToken).ConfigureAwait(false);

        var sizeBytes = MeasureSize(status, version, entries);
        if (sizeBytes > DiagnosticBundleLimits.MaxSizeBytes)
        {
            throw new DiagnosticBundleException(
                DiagnosticBundleFailureReason.SizeLimitExceeded,
                $"Bundle size {sizeBytes} bytes exceeds the maximum of {DiagnosticBundleLimits.MaxSizeBytes} bytes " +
                "- narrow the selected correlation ids or the time window.");
        }

        var bundleId = Guid.NewGuid();
        var generatedAt = _timeProvider.GetUtcNow();

        await RecordProvenanceAsync(request, bundleId, generatedAt, entries.Count, sizeBytes, cancellationToken)
            .ConfigureAwait(false);

        return new DiagnosticBundleResult(
            bundleId,
            generatedAt,
            request.RequestedByActorId,
            request.WindowStart,
            request.WindowEnd,
            version,
            status,
            entries,
            sizeBytes);
    }

    private async Task<SystemStatusSummary> BuildSystemStatusAsync(CancellationToken cancellationToken)
    {
        var unhealthy = await _observabilityService.GetUnhealthyChecksAsync(cancellationToken).ConfigureAwait(false);
        var targets = unhealthy.Select(check => check.Target).Distinct(StringComparer.Ordinal).ToList();
        return new SystemStatusSummary(unhealthy.Count, targets);
    }

    private static VersionFingerprint BuildVersionFingerprint()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";
        return new VersionFingerprint(
            informationalVersion,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription);
    }

    private async Task<IReadOnlyList<DiagnosticBundleLogEntry>> CollectLogEntriesAsync(
        DiagnosticBundleRequest request,
        CancellationToken cancellationToken)
    {
        var entries = new List<DiagnosticBundleLogEntry>();
        foreach (var correlationId in request.CorrelationIds)
        {
            var auditEvents = await _auditEventStore
                .GetByCorrelationIdAsync(correlationId, cancellationToken)
                .ConfigureAwait(false);

            foreach (var auditEvent in auditEvents)
            {
                if (auditEvent.OccurredAt < request.WindowStart || auditEvent.OccurredAt > request.WindowEnd)
                    continue;

                entries.Add(RedactEntry(auditEvent));
            }
        }

        return entries.OrderBy(entry => entry.OccurredAt).ToList();
    }

    private DiagnosticBundleLogEntry RedactEntry(AuditEvent auditEvent)
    {
        var detailsJson = JsonSerializer.Serialize(new
        {
            auditEvent.AggregateType,
            auditEvent.AggregateId,
            auditEvent.ActorType,
            auditEvent.Reason,
            Before = auditEvent.BeforeStateJson,
            After = auditEvent.AfterStateJson,
            Metadata = auditEvent.MetadataJson,
        });

        // Pass 1: key-name-based redaction (a field literally named
        // "password"/"pan"/... never leaves this method).
        var keyRedacted = _redactionHook.RedactJson(detailsJson);

        // Pass 2: value-pattern scan - catches a sensitive VALUE that
        // survived pass 1 because its own JSON key wasn't recognized.
        var (valueRedacted, _) = _secretScanner.Scan(keyRedacted);

        return new DiagnosticBundleLogEntry(
            auditEvent.EventName,
            auditEvent.CorrelationId,
            auditEvent.OccurredAt,
            valueRedacted);
    }

    private static long MeasureSize(
        SystemStatusSummary status,
        VersionFingerprint version,
        IReadOnlyList<DiagnosticBundleLogEntry> entries)
    {
        var payload = JsonSerializer.Serialize(new { status, version, entries });
        return Encoding.UTF8.GetByteCount(payload);
    }

    private Task RecordProvenanceAsync(
        DiagnosticBundleRequest request,
        Guid bundleId,
        DateTimeOffset generatedAt,
        int entryCount,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            request.Reason,
            CorrelationIdCount = request.CorrelationIds.Count,
            request.WindowStart,
            request.WindowEnd,
            EntryCount = entryCount,
            SizeBytes = sizeBytes,
        });

        var provenanceEvent = new AuditEvent(
            id: Guid.NewGuid(),
            eventName: "support.diagnostic_bundle.generated",
            aggregateType: "DiagnosticBundle",
            aggregateId: bundleId,
            actorType: "SupportUser",
            correlationId: request.CorrelationIds[0],
            actorId: Guid.TryParse(request.RequestedByActorId, out var actorGuid) ? actorGuid : null,
            reason: request.Reason,
            metadataJson: metadata,
            occurredAt: generatedAt);

        return _auditEventStore.AppendAsync(provenanceEvent, cancellationToken);
    }
}
