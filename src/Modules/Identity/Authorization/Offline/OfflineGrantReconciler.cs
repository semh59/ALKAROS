using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Policies;
using Npgsql;

namespace ALKAROS.Identity.Authorization.Offline;

public sealed class OfflineGrantReconciler : IOfflineGrantReconciler
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";

    private readonly IOfflineAuthorityBudgetRepository _budgets;
    private readonly IAuthorizationGrantRepository _grants;
    private readonly IAuthorizationPolicyRepository _policies;
    private readonly IOfflineReplayLedger _replays;
    private readonly IBehaviouralTighteningRepository _tightenings;
    private readonly Func<DateTimeOffset> _nowUtc;

    public OfflineGrantReconciler(
        IOfflineAuthorityBudgetRepository budgets,
        IAuthorizationGrantRepository grants,
        IAuthorizationPolicyRepository policies,
        IOfflineReplayLedger replays,
        IBehaviouralTighteningRepository tightenings,
        Func<DateTimeOffset>? nowUtc = null)
    {
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _replays = replays ?? throw new ArgumentNullException(nameof(replays));
        _tightenings = tightenings ?? throw new ArgumentNullException(nameof(tightenings));
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<OfflineReconciliationResult>> ReconcileAsync(
        Guid budgetId,
        IReadOnlyList<OfflineAuthorizedAction> actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var budget = await _budgets.GetAsync(budgetId, cancellationToken)
            ?? throw new UnknownOfflineAuthorityBudgetException(budgetId);

        var results = new List<OfflineReconciliationResult>(actions.Count);
        var grantedThisBatch = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var action in actions)
        {
            action.Validate();

            // Idea 2: surface the requester's existing behavioural-tightening
            // signal (docs/domain/authorization-model.md's rolling rate
            // check, V1-IAM-023) alongside the offline result itself, so a
            // manager reviewing a reconnect batch sees which pending items
            // belong to someone already flagged for an unusual rate on this
            // permission - not just an undifferentiated list. This never
            // changes the reconciliation decision itself (Admit/Deny above
            // is unaffected); it only annotates it.
            var flagged = await _tightenings.FindActiveAsync(
                action.RequesterUserId, action.PermissionCode, cancellationToken) is not null;

            var replay = await _grants.FindByIdempotencyKeyAsync(action.IdempotencyKey, cancellationToken);
            if (replay is not null)
            {
                results.Add(new OfflineReconciliationResult(
                    action.IdempotencyKey, replay.GrantId, replay.Status, "already reconciled", flagged, IsReplay: true));
                continue;
            }

            var priorBatch = grantedThisBatch.GetValueOrDefault(action.PermissionCode);
            var (status, detail) = await DecideAsync(budget, action, priorBatch, cancellationToken);

            var stored = await StoreAsync(budget.BudgetId, action, status, cancellationToken);
            if (stored.Status == GrantStatus.Pending)
                grantedThisBatch[action.PermissionCode] = priorBatch + 1;

            results.Add(new OfflineReconciliationResult(
                action.IdempotencyKey, stored.GrantId, stored.Status, detail, flagged, IsReplay: false));
        }

        return results;
    }

    private async Task<(GrantStatus Status, string Detail)> DecideAsync(
        OfflineAuthorityBudget budget,
        OfflineAuthorizedAction action,
        int priorInThisBatch,
        CancellationToken cancellationToken)
    {
        // V1-RMD-404 (V1-RMD-399 H-06): the same own-check the online grant path applies (model §3 decision #1,
        // V1-RMD-402) — a waiter's offline void/comp on a check they do not serve, unassigned or subject-less
        // included, never reaches a manager. SubjectServingUserId is the server's own value (the endpoint reads it
        // from the order), never the device's.
        if ((string.Equals(action.PermissionCode, ApplicationPermissions.BillsVoid, StringComparison.Ordinal)
                || string.Equals(action.PermissionCode, ApplicationPermissions.BillsComp, StringComparison.Ordinal))
            && string.Equals(action.RequesterRoleCode, ApplicationPermissions.RoleWaiter, StringComparison.Ordinal)
            && action.SubjectServingUserId != action.RequesterUserId)
        {
            return (GrantStatus.Denied, "a waiter may void or comp only a check they serve");
        }

        if (budget.IsExpiredAt(action.OfflineAuthorizedAt))
            return (GrantStatus.Denied, "offline authority budget had expired when the action was taken");

        var line = budget.LineFor(action.PermissionCode);
        if (line is null)
            return (GrantStatus.Denied, "permission is not in the offline authority budget");

        var priorReconciled = await _replays.CountReconciledAsync(
            budget.BudgetId, action.PermissionCode, cancellationToken);
        if (!line.Admits(action.Amount, priorReconciled + priorInThisBatch))
            return (GrantStatus.Denied, "action is outside the offline budget for this permission");

        var policy = await _policies.GetAsync(
            action.PermissionCode, action.RequesterRoleCode, cancellationToken);
        if (AuthorizationPolicyEvaluator.Evaluate(policy, action.Amount, 0) == PolicyOutcome.Deny)
            return (GrantStatus.Denied, "live policy now denies this action");

        return (GrantStatus.Pending, "recorded for manager review (offline_pending_review)");
    }

    private async Task<AuthorizationGrant> StoreAsync(
        Guid budgetId, OfflineAuthorizedAction action, GrantStatus status, CancellationToken cancellationToken)
    {
        var now = _nowUtc();
        var draft = new AuthorizationGrant(
            GrantId: Guid.Empty,
            IdempotencyKey: action.IdempotencyKey,
            PermissionCode: action.PermissionCode,
            RequesterUserId: action.RequesterUserId,
            RequesterRoleCode: action.RequesterRoleCode,
            SubjectType: action.SubjectType,
            SubjectId: action.SubjectId,
            SubjectServingUserId: action.SubjectServingUserId,
            Amount: action.Amount,
            ReasonCode: action.ReasonCode,
            RequestedAt: now,
            Status: status,
            Path: status == GrantStatus.Pending ? null : PolicyPath.Auto,
            ApproverUserId: null,
            ResolvedAt: status == GrantStatus.Pending ? null : now);

        try
        {
            return await _replays.RecordAsync(
                draft, budgetId, action.OfflineAuthorizedAt, now, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            // A concurrent reconcile of the same action won the insert.
            var winner = await _grants.FindByIdempotencyKeyAsync(action.IdempotencyKey, cancellationToken);
            if (winner is null)
                throw;
            return winner;
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolation)
        {
            // The budget was deleted between GetAsync and this insert (a
            // same-session re-issue race, see PostgresOfflineAuthorityBudgetRepository).
            throw new UnknownOfflineAuthorityBudgetException(budgetId);
        }
    }
}
