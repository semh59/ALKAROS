namespace ALKAROS.Identity.Authorization.Behavioural;

/// <summary>Reads and writes <c>identity.behavioural_tightenings</c>.</summary>
public interface IBehaviouralTighteningRepository
{
    /// <summary>The open tightening for the scope, or null.</summary>
    Task<BehaviouralTightening?> FindActiveAsync(
        Guid userId, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a tightening. If one is already open for the scope (a concurrent
    /// request won), returns that one instead of raising.
    /// </summary>
    Task<BehaviouralTightening> OpenAsync(
        BehaviouralTightening draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the open tightening. Throws
    /// <see cref="BehaviouralTighteningAlreadyClearedException"/> when it is
    /// unknown or already cleared (the database trigger is the backstop).
    /// </summary>
    Task<BehaviouralTightening> ClearAsync(
        Guid tighteningId, Guid clearedByUserId, DateTimeOffset at,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// When the scope was last cleared, or null. The gate uses this so a
    /// just-cleared user is not re-tightened by the same trailing window.
    /// </summary>
    Task<DateTimeOffset?> MostRecentClearAsync(
        Guid userId, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>Every open tightening, oldest first. Consumed by the manager surface.</summary>
    Task<IReadOnlyList<BehaviouralTightening>> ListActiveAsync(
        CancellationToken cancellationToken = default);
}
