namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>
/// Step 1 of the grant flow: takes a <see cref="GrantRequest"/> for an action a
/// role does not hold outright and resolves it through the own-check guard and
/// the policy engine, recording exactly one <c>identity.authorization_grants</c>
/// row. An <see cref="GrantOutcome.Pending"/> result is left for a delegation
/// (V1-IAM-021) or a manager (V1-IAM-020) to resolve.
/// </summary>
public interface IAuthorizationGrantService
{
    Task<GrantResolution> RequestAsync(GrantRequest request, CancellationToken cancellationToken = default);
}
