using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Delegations;
using ALKAROS.Identity.Authorization.Grants;

namespace ALKAROS.Host.Experience.Authorization;

/// <summary>
/// The manager decision surface (V1-IAM-020): read the pending grants, active
/// delegations and open behavioural tightenings, and resolve them. Approve /
/// deny record <c>policy_path = 'manual'</c> with the approver; the first
/// responder wins and a second attempt raises
/// <see cref="AuthorizationGrantAlreadyResolvedException"/>.
/// </summary>
public sealed class AuthorizationDecisionStore
{
    private readonly IAuthorizationGrantRepository _grants;
    private readonly IAuthorizationDelegationRepository _delegations;
    private readonly IBehaviouralTighteningService _tightenings;
    private readonly Func<DateTimeOffset> _nowUtc;

    public AuthorizationDecisionStore(
        IAuthorizationGrantRepository grants,
        IAuthorizationDelegationRepository delegations,
        IBehaviouralTighteningService tightenings,
        Func<DateTimeOffset>? nowUtc = null)
    {
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _delegations = delegations ?? throw new ArgumentNullException(nameof(delegations));
        _tightenings = tightenings ?? throw new ArgumentNullException(nameof(tightenings));
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<PendingGrantV1>> ListPendingGrantsAsync(CancellationToken cancellationToken)
    {
        var grants = await _grants.ListPendingAsync(cancellationToken);
        return grants
            .Select(grant => new PendingGrantV1(
                grant.GrantId,
                grant.PermissionCode,
                grant.RequesterUserId,
                grant.RequesterRoleCode,
                grant.SubjectType,
                grant.SubjectId,
                grant.SubjectServingUserId,
                grant.Amount,
                grant.ReasonCode,
                grant.RequestedAt))
            .ToArray();
    }

    public Task<ResolvedGrantV1> ApproveAsync(Guid grantId, Guid approverUserId, CancellationToken cancellationToken)
        => ResolveAsync(grantId, GrantStatus.Granted, approverUserId, cancellationToken);

    public Task<ResolvedGrantV1> DenyAsync(Guid grantId, Guid approverUserId, CancellationToken cancellationToken)
        => ResolveAsync(grantId, GrantStatus.Denied, approverUserId, cancellationToken);

    private async Task<ResolvedGrantV1> ResolveAsync(
        Guid grantId, GrantStatus status, Guid approverUserId, CancellationToken cancellationToken)
    {
        var resolved = await _grants.ResolveAsync(
            grantId, status, PolicyPath.Manual, approverUserId, cancellationToken);
        return new ResolvedGrantV1(
            resolved.GrantId,
            GrantText.Status(resolved.Status),
            GrantText.Path(resolved.Path!.Value),
            resolved.ApproverUserId!.Value,
            resolved.ResolvedAt!.Value);
    }

    public async Task<IReadOnlyList<ActiveDelegationV1>> ListActiveDelegationsAsync(CancellationToken cancellationToken)
    {
        var delegations = await _delegations.ListActiveAsync(_nowUtc(), cancellationToken);
        return delegations
            .Select(delegation => new ActiveDelegationV1(
                delegation.DelegationId,
                delegation.PermissionCode,
                delegation.GranteeUserId,
                delegation.DelegatorUserId,
                delegation.LimitAmount,
                delegation.GrantedAt,
                delegation.ExpiresAt))
            .ToArray();
    }

    public Task<bool> RevokeDelegationAsync(Guid delegationId, Guid actorUserId, CancellationToken cancellationToken)
        => _delegations.RevokeAsync(delegationId, _nowUtc(), actorUserId, cancellationToken);

    public async Task<IReadOnlyList<OpenTighteningV1>> ListOpenTighteningsAsync(CancellationToken cancellationToken)
    {
        var tightenings = await _tightenings.ListActiveAsync(cancellationToken);
        return tightenings
            .Select(tightening => new OpenTighteningV1(
                tightening.TighteningId,
                tightening.UserId,
                tightening.PermissionCode,
                tightening.RecentCount,
                tightening.TriggerRatio,
                tightening.TriggeredAt))
            .ToArray();
    }

    public Task ClearTighteningAsync(Guid tighteningId, Guid managerUserId, CancellationToken cancellationToken)
        => _tightenings.ClearAsync(tighteningId, managerUserId, cancellationToken);
}
