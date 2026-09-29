using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Security.IdentityHardening;

namespace ALKAROS.Host.Experience.SecurityAdministration;

/// <summary>
/// V1-RMD-266: durable replacement for the in-memory reference sink of the
/// Security module. Each suspicious-login / recovery event becomes an immutable
/// audit event (actor, reason, prior failed attempts) in audit.audit_events.
/// </summary>
public sealed class AuditEventStoreSuspiciousLoginSink : ISuspiciousLoginAuditSink
{
    private readonly IAuditEventStore _auditEvents;

    public AuditEventStoreSuspiciousLoginSink(IAuditEventStore auditEvents)
    {
        _auditEvents = auditEvents ?? throw new ArgumentNullException(nameof(auditEvents));
    }

    public Task RecordAsync(SuspiciousLoginEvent loginEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loginEvent);

        // An event without a user id (an unknown username) has no aggregate to
        // attach to; a stable empty-guid aggregate keeps it queryable by name.
        var aggregateId = loginEvent.UserId ?? Guid.Empty;
        var hasActor = Guid.TryParse(loginEvent.Actor, out var actorId);
        return _auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: $"security.{ToKebab(loginEvent.Reason)}",
                aggregateType: "User",
                aggregateId: aggregateId,
                actorType: hasActor ? "User" : "System",
                correlationId: Guid.NewGuid().ToString("N"),
                actorId: hasActor ? actorId : null,
                reason: loginEvent.Reason.ToString(),
                afterStateJson: JsonSerializer.Serialize(new
                {
                    username = loginEvent.Username,
                    priorFailedAttempts = loginEvent.PriorFailedAttempts,
                }),
                occurredAt: loginEvent.OccurredAt),
            cancellationToken);
    }

    private static string ToKebab(SuspiciousLoginReason reason) => reason switch
    {
        SuspiciousLoginReason.SuccessAfterRepeatedFailures => "success-after-repeated-failures",
        SuspiciousLoginReason.LockoutTriggered => "lockout-triggered",
        SuspiciousLoginReason.AllSessionsRevoked => "all-sessions-revoked",
        SuspiciousLoginReason.AccountForceUnlocked => "account-force-unlocked",
        SuspiciousLoginReason.AccountDeactivated => "account-deactivated",
        SuspiciousLoginReason.AccountReactivated => "account-reactivated",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown suspicious login reason."),
    };
}
