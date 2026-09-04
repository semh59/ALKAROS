using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Policies;
using Npgsql;

namespace ALKAROS.Identity.Authorization.Grants;

public sealed class AuthorizationGrantService : IAuthorizationGrantService
{
    private const string UniqueViolation = "23505";

    // Only void and comp carry the "(own check)" restriction in the model's §3
    // role matrix; bills.discount is a plain grant for every role.
    private static readonly HashSet<string> OwnCheckOnly = new(StringComparer.Ordinal)
    {
        ApplicationPermissions.BillsVoid,
        ApplicationPermissions.BillsComp,
    };

    private readonly IAuthorizationGrantRepository _grants;
    private readonly IAuthorizationPolicyRepository _policies;
    private readonly IReadOnlyList<IEscalationResolver> _escalationResolvers;
    private readonly IReadOnlyList<IPrePolicyGate> _prePolicyGates;
    private readonly Func<DateTimeOffset> _nowUtc;

    /// <param name="escalationResolvers">
    /// Consulted in order when the policy engine escalates; the first to return a
    /// <see cref="PolicyPath"/> authorizes the grant without a manager (e.g. an
    /// active delegation, V1-IAM-021). Empty by default — every escalation then
    /// waits for a manager.
    /// </param>
    /// <param name="prePolicyGates">
    /// Consulted before the policy engine; if any forces escalation, a
    /// would-be <c>auto</c> approval is downgraded to a pending manager decision
    /// (e.g. behavioural tightening, V1-IAM-023). Empty by default.
    /// </param>
    /// <param name="nowUtc">
    /// UTC clock; defaults to <see cref="DateTimeOffset.UtcNow"/>. Injected so the
    /// <c>auto_within</c> window boundary is deterministic in tests.
    /// </param>
    public AuthorizationGrantService(
        IAuthorizationGrantRepository grants,
        IAuthorizationPolicyRepository policies,
        IEnumerable<IEscalationResolver>? escalationResolvers = null,
        IEnumerable<IPrePolicyGate>? prePolicyGates = null,
        Func<DateTimeOffset>? nowUtc = null)
    {
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _escalationResolvers = escalationResolvers?.ToArray() ?? [];
        _prePolicyGates = prePolicyGates?.ToArray() ?? [];
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<GrantResolution> RequestAsync(
        GrantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var replay = await _grants.FindByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (replay is not null)
            return new GrantResolution(replay, OutcomeOf(replay.Status));

        // Own-check guard (authorization model §3, resolved decision #1): a
        // *waiter* may raise a void/comp grant only on a check they serve — a
        // grant on another server's check is refused before it reaches a
        // manager. cashier/supervisor/manager void/comp is an unrestricted
        // grant, so the guard is scoped to the waiter role.
        if (OwnCheckOnly.Contains(request.PermissionCode)
            && string.Equals(
                request.RequesterRoleCode, ApplicationPermissions.RoleWaiter, StringComparison.Ordinal)
            && request.SubjectServingUserId is { } serving
            && serving != request.RequesterUserId)
        {
            return await StoreAsync(request, GrantStatus.Denied, PolicyPath.Auto, cancellationToken);
        }

        var forceEscalation = false;
        foreach (var gate in _prePolicyGates)
        {
            if (await gate.ShouldForceEscalationAsync(request, _nowUtc(), cancellationToken))
            {
                forceEscalation = true;
                break;
            }
        }

        var policy = await _policies.GetAsync(
            request.PermissionCode, request.RequesterRoleCode, cancellationToken);

        var priorAutoGrants = 0;
        if (policy is { Mode: PolicyMode.AutoWithin })
        {
            var since = _nowUtc().AddSeconds(-policy.WindowSeconds!.Value);
            priorAutoGrants = await _grants.CountAutoGrantsSinceAsync(
                request.RequesterUserId, request.PermissionCode, since, cancellationToken);
        }

        var outcome = AuthorizationPolicyEvaluator.Evaluate(policy, request.Amount, priorAutoGrants);

        // A pre-policy gate never denies and never overrides always_deny; it only
        // stops a routine auto-approval so a manager looks (model §1).
        if (forceEscalation && outcome == PolicyOutcome.AutoApprove)
            outcome = PolicyOutcome.Escalate;

        switch (outcome)
        {
            case PolicyOutcome.AutoApprove:
                return await StoreAsync(request, GrantStatus.Granted, PolicyPath.Auto, cancellationToken);
            case PolicyOutcome.Deny:
                return await StoreAsync(request, GrantStatus.Denied, PolicyPath.Auto, cancellationToken);
            case PolicyOutcome.Escalate:
                break;
            default:
                throw new InvalidOperationException($"Unhandled policy outcome '{outcome}'.");
        }

        foreach (var resolver in _escalationResolvers)
        {
            var path = await resolver.TryResolveAsync(request, _nowUtc(), cancellationToken);
            if (path is { } resolved)
                return await StoreAsync(request, GrantStatus.Granted, resolved, cancellationToken);
        }

        return await StoreAsync(request, GrantStatus.Pending, path: null, cancellationToken);
    }

    private async Task<GrantResolution> StoreAsync(
        GrantRequest request, GrantStatus status, PolicyPath? path, CancellationToken cancellationToken)
    {
        var now = _nowUtc();
        var draft = new AuthorizationGrant(
            GrantId: Guid.Empty,
            IdempotencyKey: request.IdempotencyKey,
            PermissionCode: request.PermissionCode,
            RequesterUserId: request.RequesterUserId,
            RequesterRoleCode: request.RequesterRoleCode,
            SubjectType: request.SubjectType,
            SubjectId: request.SubjectId,
            SubjectServingUserId: request.SubjectServingUserId,
            Amount: request.Amount,
            ReasonCode: request.ReasonCode,
            RequestedAt: now,
            Status: status,
            Path: path,
            ApproverUserId: null,
            ResolvedAt: status == GrantStatus.Pending ? null : now);

        try
        {
            var stored = await _grants.InsertAsync(draft, cancellationToken);
            return new GrantResolution(stored, OutcomeOf(stored.Status));
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            // A concurrent request for the same command instance won the insert.
            var winner = await _grants.FindByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
            if (winner is null)
                throw;
            return new GrantResolution(winner, OutcomeOf(winner.Status));
        }
    }

    private static GrantOutcome OutcomeOf(GrantStatus status) => status switch
    {
        GrantStatus.Granted => GrantOutcome.Authorized,
        GrantStatus.Denied => GrantOutcome.Refused,
        GrantStatus.Pending => GrantOutcome.Pending,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
