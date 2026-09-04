using ALKAROS.Identity.Authorization.Grants;

namespace ALKAROS.Identity.Authorization.Delegations;

/// <summary>
/// Step 2 of the grant flow (docs/domain/authorization-model.md §4): when the
/// policy engine escalates, an active delegation that covers the requester,
/// permission and amount authorizes the grant with
/// <see cref="PolicyPath.Delegation"/> instead of a manager.
/// </summary>
public sealed class DelegationEscalationResolver : IEscalationResolver
{
    private readonly IAuthorizationDelegationRepository _delegations;

    public DelegationEscalationResolver(IAuthorizationDelegationRepository delegations)
    {
        _delegations = delegations ?? throw new ArgumentNullException(nameof(delegations));
    }

    public async Task<PolicyPath?> TryResolveAsync(
        GrantRequest request, DateTimeOffset instant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var covering = await _delegations.FindCoveringAsync(
            request.RequesterUserId, request.PermissionCode, request.Amount, instant, cancellationToken);

        return covering is null ? null : PolicyPath.Delegation;
    }
}
