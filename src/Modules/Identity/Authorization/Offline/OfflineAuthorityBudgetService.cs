using ALKAROS.Identity.Authorization.Policies;

namespace ALKAROS.Identity.Authorization.Offline;

public sealed class OfflineAuthorityBudgetService : IOfflineAuthorityBudgetService
{
    private readonly IAuthorizationPolicyRepository _policies;
    private readonly IOfflineAuthorityBudgetRepository _budgets;
    private readonly Func<DateTimeOffset> _nowUtc;

    /// <param name="nowUtc">
    /// UTC clock; defaults to <see cref="DateTimeOffset.UtcNow"/>. Injected so the
    /// budget's expiry is deterministic in tests.
    /// </param>
    public OfflineAuthorityBudgetService(
        IAuthorizationPolicyRepository policies,
        IOfflineAuthorityBudgetRepository budgets,
        Func<DateTimeOffset>? nowUtc = null)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<OfflineAuthorityBudget> IssueAsync(
        Guid userId,
        string roleCode,
        Guid sessionId,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);

        var lifetime = ttl ?? TimeSpan.FromHours(OfflineAuthorityBudgetPolicy.DefaultTtlHours);
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "Budget lifetime must be positive.");

        var allPolicies = await _policies.ListAsync(cancellationToken);
        var lines = OfflineAuthorityBudgetPolicy.LinesFor(roleCode, allPolicies);

        var issuedAt = _nowUtc();
        return await _budgets.CreateAsync(
            userId, sessionId, lines, issuedAt, issuedAt + lifetime, cancellationToken);
    }
}
