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

/// <summary>V1-RMD-407: a manager/supervisor hands a grant-class permission to <see cref="GranteeUserId"/> until <see cref="ExpiresAt"/>.</summary>
public sealed record CreateDelegationRequestV1(
    Guid GranteeUserId,
    string? PermissionCode,
    decimal LimitAmount,
    DateTimeOffset ExpiresAt);

/// <summary>V1-RMD-407: the delegator does not hold the permission they tried to delegate.</summary>
public sealed class DelegatorLacksPermissionException : Exception
{
    public DelegatorLacksPermissionException(string permissionCode)
        : base($"The delegator does not hold '{permissionCode}' and cannot delegate it.")
    {
    }
}

/// <summary>V1-RMD-407: the named grantee is not a known user.</summary>
public sealed class DelegationGranteeNotFoundException : Exception
{
    public DelegationGranteeNotFoundException(Guid granteeUserId)
        : base($"Grantee '{granteeUserId}' is not a known user.")
    {
    }
}
