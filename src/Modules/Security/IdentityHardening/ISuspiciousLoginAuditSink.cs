namespace ALKAROS.Security.IdentityHardening;

/// <summary>
/// Write-through audit hook for <see cref="SuspiciousLoginEvent"/> — mirrors
/// <c>IDenialEventSink</c>'s own contract (a failure surfaces instead of
/// being silently swallowed).
/// </summary>
public interface ISuspiciousLoginAuditSink
{
    Task RecordAsync(SuspiciousLoginEvent loginEvent, CancellationToken cancellationToken = default);
}
