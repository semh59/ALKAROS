namespace ALKAROS.Identity.Authorization.Delegations;

/// <summary>Reads and writes <c>identity.authorization_delegations</c>.</summary>
public interface IAuthorizationDelegationRepository
{
    /// <summary>Creates a delegation. Returns the stored row.</summary>
    Task<AuthorizationDelegation> CreateAsync(
        DelegationRequest request, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a delegation early, recording who cancelled it. Returns false
    /// when the id is unknown or the delegation is already revoked.
    /// </summary>
    Task<bool> RevokeAsync(
        Guid delegationId, DateTimeOffset at, Guid revokedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The newest delegation that covers <paramref name="permissionCode"/> for
    /// <paramref name="granteeUserId"/> at or above <paramref name="amount"/> and
    /// active at <paramref name="instant"/>, or null.
    /// </summary>
    Task<AuthorizationDelegation?> FindCoveringAsync(
        Guid granteeUserId,
        string permissionCode,
        decimal amount,
        DateTimeOffset instant,
        CancellationToken cancellationToken = default);

    /// <summary>Delegations that are neither revoked nor expired at <paramref name="instant"/>.</summary>
    Task<IReadOnlyList<AuthorizationDelegation>> ListActiveAsync(
        DateTimeOffset instant, CancellationToken cancellationToken = default);
}
