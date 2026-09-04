using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Policies;
using Npgsql;

namespace ALKAROS.Identity.Authorization.Grants;

public sealed class AuthorizationGrantService : IAuthorizationGrantService
{
    private const string UniqueViolation = "23505";

    private static readonly HashSet<string> OwnCheckOnly = new(StringComparer.Ordinal)
    {
        ApplicationPermissions.BillsVoid,
        ApplicationPermissions.BillsComp,
        ApplicationPermissions.BillsDiscount,
    };

    private readonly IAuthorizationGrantRepository _grants;
    private readonly IAuthorizationPolicyRepository _policies;
    private readonly Func<DateTimeOffset> _nowUtc;

    /// <param name="nowUtc">
    /// UTC clock; defaults to <see cref="DateTimeOffset.UtcNow"/>. Injected so the
    /// <c>auto_within</c> window boundary is deterministic in tests.
    /// </param>
    public AuthorizationGrantService(
        IAuthorizationGrantRepository grants,
        IAuthorizationPolicyRepository policies,
        Func<DateTimeOffset>? nowUtc = null)
    {
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
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

        // Own-check guard (model §3, resolved decision #1): a waiter/cashier may
        // only raise a void/comp/discount grant on a check they serve. A grant on
        // someone else's check is refused before it reaches a manager.
        if (OwnCheckOnly.Contains(request.PermissionCode)
            && request.SubjectServingUserId is { } serving
            && serving != request.RequesterUserId)
        {
            return await StoreAsync(request, GrantStatus.Denied, PolicyPath.Auto, cancellationToken);
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

        return outcome switch
        {
            PolicyOutcome.AutoApprove =>
                await StoreAsync(request, GrantStatus.Granted, PolicyPath.Auto, cancellationToken),
            PolicyOutcome.Deny =>
                await StoreAsync(request, GrantStatus.Denied, PolicyPath.Auto, cancellationToken),
            PolicyOutcome.Escalate =>
                await StoreAsync(request, GrantStatus.Pending, path: null, cancellationToken),
            _ => throw new InvalidOperationException($"Unhandled policy outcome '{outcome}'."),
        };
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
