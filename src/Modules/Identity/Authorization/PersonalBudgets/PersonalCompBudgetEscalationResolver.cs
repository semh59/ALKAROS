using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Grants;

namespace ALKAROS.Identity.Authorization.PersonalBudgets;

/// <summary>
/// V1-WTR-012 (garson audit follow-on, 2026-09-11 ideation): every
/// <c>bills.comp</c> request from a role that does not hold the permission
/// outright used to escalate to a manager, no matter how small — a waiter
/// comping an unhappy guest's 15 TRY tea raised the exact same pending grant
/// as a 500 TRY write-off. This gives the waiter role a small, self-service,
/// per-day comp allowance: within <see cref="PersonalCompBudgetPolicy"/>'s
/// caps, the request resolves immediately with
/// <see cref="PolicyPath.PersonalBudget"/> instead of waiting on a manager —
/// still a real grant row with its own reason code and audit trail, just one
/// nobody had to individually approve.
///
/// Deliberately scoped to <c>bills.comp</c> and the waiter role only:
/// cashier/supervisor/manager already hold <c>bills.comp</c> outright and
/// never reach an escalation resolver for it at all. Runs as one more step
/// in <see cref="IAuthorizationGrantService"/>'s existing escalation chain —
/// the own-check guard (a waiter may only raise this on a check they serve)
/// and any forced escalation from an open behavioural tightening
/// (<c>BehaviouralTighteningGate</c>) are both already evaluated before any
/// resolver runs, so this never needs to re-check either.
/// </summary>
public sealed class PersonalCompBudgetEscalationResolver : IEscalationResolver
{
    private readonly IAuthorizationGrantRepository _grants;
    private readonly Func<DateTimeOffset> _nowUtc;

    public PersonalCompBudgetEscalationResolver(
        IAuthorizationGrantRepository grants, Func<DateTimeOffset>? nowUtc = null)
    {
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<PolicyPath?> TryResolveAsync(
        GrantRequest request, DateTimeOffset instant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.PermissionCode, ApplicationPermissions.BillsComp, StringComparison.Ordinal))
            return null;
        if (!string.Equals(request.RequesterRoleCode, ApplicationPermissions.RoleWaiter, StringComparison.Ordinal))
            return null;
        if (request.Amount <= 0 || request.Amount > PersonalCompBudgetPolicy.PerItemCap)
            return null;

        // Reset boundary (Semih's decision, 2026-09-11): UTC calendar day,
        // matching the existing reporting.authorization_grant_daily view's
        // own bucketing ((resolved_at AT TIME ZONE 'UTC')::date) rather than
        // inventing a second day-boundary convention.
        var now = _nowUtc();
        var startOfUtcDay = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        var spentToday = await _grants.SumPersonalBudgetGrantedSinceAsync(
            request.RequesterUserId, request.PermissionCode, startOfUtcDay, cancellationToken);

        if (spentToday + request.Amount > PersonalCompBudgetPolicy.DailyCap)
            return null;

        return PolicyPath.PersonalBudget;
    }
}

/// <summary>
/// The caps themselves (Semih, 2026-09-11): 150 TRY/day total, 50 TRY/item —
/// both chosen as named constants here rather than a settings row so the
/// policy is one place to read, not a runtime configuration surface; promote
/// to a real settings-backed value if a venue ever needs to tune it.
/// </summary>
public static class PersonalCompBudgetPolicy
{
    public const decimal DailyCap = 150m;
    public const decimal PerItemCap = 50m;
}
