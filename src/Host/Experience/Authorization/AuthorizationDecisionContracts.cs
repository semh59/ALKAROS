namespace ALKAROS.Host.Experience.Authorization;

/// <summary>The context block a manager sees for one pending grant (model §4 step 3).</summary>
public sealed record PendingGrantV1(
    Guid GrantId,
    string PermissionCode,
    Guid RequesterUserId,
    string RequesterRoleCode,
    string? SubjectType,
    Guid? SubjectId,
    Guid? SubjectServingUserId,
    decimal Amount,
    string ReasonCode,
    DateTimeOffset RequestedAt);

/// <summary>One active time-boxed delegation (V1-IAM-021), for the revoke list.</summary>
public sealed record ActiveDelegationV1(
    Guid DelegationId,
    string PermissionCode,
    Guid GranteeUserId,
    Guid DelegatorUserId,
    decimal LimitAmount,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt);

/// <summary>One open behavioural tightening (V1-IAM-023), for the clear list.</summary>
public sealed record OpenTighteningV1(
    Guid TighteningId,
    Guid UserId,
    string PermissionCode,
    int RecentCount,
    decimal TriggerRatio,
    DateTimeOffset TriggeredAt);

/// <summary>The resolved grant returned by approve / deny.</summary>
public sealed record ResolvedGrantV1(
    Guid GrantId,
    string Status,
    string PolicyPath,
    Guid ApproverUserId,
    DateTimeOffset ResolvedAt);

public sealed record AuthorizationDecisionErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record AuthorizationDecisionErrorEnvelopeV1(AuthorizationDecisionErrorV1 Error);
