namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>
/// A step consulted by <see cref="IAuthorizationGrantService"/> when the policy
/// engine escalates a request. Each resolver, in order, gets a chance to resolve
/// the would-be-pending grant without a manager. The first to return a
/// non-null <see cref="PolicyPath"/> wins; if none do, the grant is left
/// <see cref="GrantStatus.Pending"/> for a manager (V1-IAM-020).
/// </summary>
public interface IEscalationResolver
{
    /// <summary>
    /// Returns the <see cref="PolicyPath"/> to record when this resolver
    /// authorizes <paramref name="request"/> at <paramref name="instant"/>, or
    /// null when it does not apply.
    /// </summary>
    Task<PolicyPath?> TryResolveAsync(
        GrantRequest request, DateTimeOffset instant, CancellationToken cancellationToken = default);
}
