namespace ALKAROS.Identity.Authorization.Offline;

/// <summary>
/// Issues the offline authority budget for a device session
/// (docs/domain/authorization-model.md §5). Called when a session starts; the
/// caller returns the stored budget to the device, which caches it for offline
/// use. Reconciliation on reconnect is <see cref="IOfflineGrantReconciler"/>.
/// </summary>
public interface IOfflineAuthorityBudgetService
{
    /// <summary>
    /// Snapshots <paramref name="roleCode"/>'s <c>auto_within</c> policies into a
    /// fresh budget for <paramref name="sessionId"/>, expiring
    /// <see cref="OfflineAuthorityBudgetPolicy.DefaultTtlHours"/> hours out (or
    /// <paramref name="ttl"/> when given). Replaces any existing budget for the
    /// session. A role with no <c>auto_within</c> policy gets a budget with no
    /// lines — every offline grant then requires reconnect.
    /// </summary>
    Task<OfflineAuthorityBudget> IssueAsync(
        Guid userId,
        string roleCode,
        Guid sessionId,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);
}
