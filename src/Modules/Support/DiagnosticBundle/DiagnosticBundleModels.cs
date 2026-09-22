namespace ALKAROS.Support.DiagnosticBundle;

/// <summary>
/// A support engineer's request for a redacted diagnostic bundle: which
/// correlation IDs to trace and the time window to bound the search to
/// (V15-SUP-001). The requester and reason are recorded on the bundle's own
/// provenance audit entry.
/// </summary>
public sealed record DiagnosticBundleRequest(
    string RequestedByActorId,
    IReadOnlyList<string> CorrelationIds,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string Reason)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RequestedByActorId))
            throw new ArgumentException("Requested-by actor id cannot be empty.", nameof(RequestedByActorId));
        if (CorrelationIds is null || CorrelationIds.Count == 0)
            throw new DiagnosticBundleException(
                DiagnosticBundleFailureReason.NoCorrelationIdsProvided,
                "At least one correlation id must be selected.");
        if (CorrelationIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Correlation ids cannot contain blank entries.", nameof(CorrelationIds));
        if (WindowEnd <= WindowStart)
            throw new ArgumentException("Window end must be after window start.", nameof(WindowEnd));
        if (WindowEnd - WindowStart > DiagnosticBundleLimits.MaxWindow)
        {
            throw new DiagnosticBundleException(
                DiagnosticBundleFailureReason.TimeWindowTooLarge,
                $"Requested window {WindowEnd - WindowStart} exceeds the maximum of {DiagnosticBundleLimits.MaxWindow}.");
        }
        if (string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("A reason must be recorded for the bundle's own audit trail.", nameof(Reason));
    }
}

/// <summary>
/// A single redacted, size-accounted entry contributed by one correlated
/// audit event.
/// </summary>
public sealed record DiagnosticBundleLogEntry(
    string EventName,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    string RedactedDetailsJson);

/// <summary>
/// A non-sensitive fingerprint of the running build — no secret, connection
/// string or environment variable value.
/// </summary>
public sealed record VersionFingerprint(
    string InformationalVersion,
    string RuntimeFrameworkDescription,
    string OSDescription);

/// <summary>
/// A summary of the system's current health, reusing V1-OBS-001's own
/// unhealthy-check query rather than a second status model.
/// </summary>
public sealed record SystemStatusSummary(
    int UnhealthyCheckCount,
    IReadOnlyList<string> UnhealthyTargets);

/// <summary>
/// The finished, redacted, size-bounded diagnostic bundle (V15-SUP-001).
/// </summary>
public sealed record DiagnosticBundleResult(
    Guid BundleId,
    DateTimeOffset GeneratedAt,
    string RequestedByActorId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    VersionFingerprint Version,
    SystemStatusSummary SystemStatus,
    IReadOnlyList<DiagnosticBundleLogEntry> LogEntries,
    long SizeBytes);

public static class DiagnosticBundleLimits
{
    /// <summary>
    /// A support bundle traces a specific, already-known incident window;
    /// 30 days comfortably covers even a slow-escalating case without
    /// turning the bundle into an unbounded data export.
    /// </summary>
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(30);

    /// <summary>
    /// Keeps a bundle small enough to attach to a support ticket. Exceeding
    /// this signals the requester narrowed correlation ids/window too
    /// broadly, not that the limit should grow to fit.
    /// </summary>
    public const long MaxSizeBytes = 5 * 1024 * 1024;
}

public enum DiagnosticBundleFailureReason
{
    NoCorrelationIdsProvided,
    TimeWindowTooLarge,
    SizeLimitExceeded,
}

public sealed class DiagnosticBundleException : Exception
{
    public DiagnosticBundleFailureReason Reason { get; }

    public DiagnosticBundleException(DiagnosticBundleFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }
}
