namespace ALKAROS.Identity.Authorization;

public sealed record DenialEvent(Guid? UserId, string PermissionCode, string Reason, DateTimeOffset OccurredAt);

/// <summary>
/// Denial audit hook. Invoked whenever an authorization decision rejects an
/// actor (V1-IAM-002 "reddetme denetimi kancası"). The hook is write-through:
/// a failure surfaces instead of being silently swallowed.
/// </summary>
public interface IDenialEventSink
{
    Task RecordAsync(DenialEvent denialEvent, CancellationToken cancellationToken = default);
}