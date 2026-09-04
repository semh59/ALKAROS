namespace ALKAROS.Identity.Authorization.Delegations;

/// <summary>
/// One row of <c>identity.authorization_delegations</c>: a bounded, time-boxed
/// hand-off of <see cref="PermissionCode"/> from <see cref="DelegatorUserId"/> to
/// <see cref="GranteeUserId"/>. Covers a grant only while
/// <c>now &lt; ExpiresAt</c>, <see cref="RevokedAt"/> is null and the grant
/// amount is at most <see cref="LimitAmount"/>.
/// </summary>
public sealed record AuthorizationDelegation(
    Guid DelegationId,
    string PermissionCode,
    Guid GranteeUserId,
    Guid DelegatorUserId,
    decimal LimitAmount,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt)
{
    public bool IsActiveAt(DateTimeOffset instant)
        => RevokedAt is null && instant < ExpiresAt;

    public bool Covers(string permissionCode, decimal amount, DateTimeOffset instant)
        => IsActiveAt(instant)
           && string.Equals(PermissionCode, permissionCode, StringComparison.Ordinal)
           && amount <= LimitAmount;
}

/// <summary>Input to <see cref="IAuthorizationDelegationRepository.CreateAsync"/>.</summary>
public sealed record DelegationRequest(
    string PermissionCode,
    Guid GranteeUserId,
    Guid DelegatorUserId,
    decimal LimitAmount,
    DateTimeOffset ExpiresAt)
{
    public void Validate(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(PermissionCode))
            throw new ArgumentException("A delegation requires a PermissionCode.");
        if (GranteeUserId == Guid.Empty)
            throw new ArgumentException("A delegation requires a GranteeUserId.");
        if (DelegatorUserId == Guid.Empty)
            throw new ArgumentException("A delegation requires a DelegatorUserId.");
        if (GranteeUserId == DelegatorUserId)
            throw new ArgumentException("A delegation cannot name the same user as grantee and delegator.");
        if (LimitAmount < 0m)
            throw new ArgumentException("A delegation LimitAmount must not be negative.");
        if (ExpiresAt <= now)
            throw new ArgumentException("A delegation ExpiresAt must be in the future.");
    }
}
