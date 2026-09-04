namespace ALKAROS.Identity.Authorization.Behavioural;

/// <summary>
/// The manager-facing side of behavioural tightening: list what is currently
/// open and clear one. Detection and the pre-policy enforcement are
/// <see cref="BehaviouralTighteningGate"/>. Consumed by the manager decision
/// surface (V1-IAM-020).
/// </summary>
public interface IBehaviouralTighteningService
{
    Task<IReadOnlyList<BehaviouralTightening>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the tightening so the scope returns to normal auto-approval on the
    /// next request. Throws
    /// <see cref="BehaviouralTighteningAlreadyClearedException"/> when it is
    /// unknown or already cleared.
    /// </summary>
    Task<BehaviouralTightening> ClearAsync(
        Guid tighteningId, Guid managerUserId, CancellationToken cancellationToken = default);
}
