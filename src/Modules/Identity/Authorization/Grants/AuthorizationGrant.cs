namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>
/// One row of <c>identity.authorization_grants</c>. Written once as
/// <see cref="GrantStatus.Pending"/>, then resolved exactly once — the database
/// trigger rejects any other mutation.
/// </summary>
public sealed record AuthorizationGrant(
    Guid GrantId,
    string IdempotencyKey,
    string PermissionCode,
    Guid RequesterUserId,
    string RequesterRoleCode,
    string? SubjectType,
    Guid? SubjectId,
    Guid? SubjectServingUserId,
    decimal Amount,
    string ReasonCode,
    DateTimeOffset RequestedAt,
    GrantStatus Status,
    PolicyPath? Path,
    Guid? ApproverUserId,
    DateTimeOffset? ResolvedAt)
{
    public bool IsPending => Status == GrantStatus.Pending;
}

/// <summary>
/// The input to <see cref="IAuthorizationGrantService.RequestAsync"/>. The
/// caller (an Experience endpoint) fills the context it already holds;
/// <see cref="IdempotencyKey"/> identifies the one command instance so a retry
/// resolves to the same grant.
/// </summary>
public sealed record GrantRequest(
    string IdempotencyKey,
    string PermissionCode,
    Guid RequesterUserId,
    string RequesterRoleCode,
    string ReasonCode,
    decimal Amount = 0m,
    string? SubjectType = null,
    Guid? SubjectId = null,
    Guid? SubjectServingUserId = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(IdempotencyKey));
        if (string.IsNullOrWhiteSpace(PermissionCode))
            throw new ArgumentException("PermissionCode is required.", nameof(PermissionCode));
        if (RequesterUserId == Guid.Empty)
            throw new ArgumentException("RequesterUserId is required.", nameof(RequesterUserId));
        if (string.IsNullOrWhiteSpace(RequesterRoleCode))
            throw new ArgumentException("RequesterRoleCode is required.", nameof(RequesterRoleCode));
        if (string.IsNullOrWhiteSpace(ReasonCode))
            throw new ArgumentException("ReasonCode is required.", nameof(ReasonCode));
        if (Amount < 0m)
            throw new ArgumentException("Amount must not be negative.", nameof(Amount));
        if ((SubjectType is null) != (SubjectId is null))
            throw new ArgumentException("SubjectType and SubjectId must be provided together.");
    }
}

/// <summary>What the caller does next.</summary>
public enum GrantOutcome
{
    /// <summary>Proceed now. The grant is <see cref="GrantStatus.Granted"/>.</summary>
    Authorized,

    /// <summary>Stop. The grant is <see cref="GrantStatus.Denied"/>.</summary>
    Refused,

    /// <summary>
    /// Hold the command; the grant is <see cref="GrantStatus.Pending"/> and a
    /// delegation or a manager will resolve it out of band.
    /// </summary>
    Pending,
}

/// <summary>The result of a grant request: the stored row and what it means.</summary>
public sealed record GrantResolution(AuthorizationGrant Grant, GrantOutcome Outcome);
