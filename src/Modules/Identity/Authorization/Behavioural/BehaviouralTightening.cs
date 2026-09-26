namespace ALKAROS.Identity.Authorization.Behavioural;

/// <summary>
/// One row of <c>identity.behavioural_tightenings</c>: a user whose granted rate
/// for <see cref="PermissionCode"/> spiked past their baseline, auto-moved to
/// "requires grant" until a manager clears it. Written once open, cleared
/// exactly once — the database trigger rejects any other mutation.
/// </summary>
public sealed record BehaviouralTightening(
    Guid TighteningId,
    Guid UserId,
    string PermissionCode,
    int RecentCount,
    decimal BaselinePerWindow,
    decimal TriggerRatio,
    DateTimeOffset TriggeredAt,
    DateTimeOffset? ClearedAt,
    Guid? ClearedByUserId)
{
    public bool IsActive => ClearedAt is null;
}

/// <summary>Raised when clearing a tightening that is already cleared or unknown.</summary>
public sealed class BehaviouralTighteningAlreadyClearedException : Exception
{
    public BehaviouralTighteningAlreadyClearedException(Guid tighteningId)
        : base($"Behavioural tightening {tighteningId} is already cleared or does not exist.")
    {
        TighteningId = tighteningId;
    }

    public Guid TighteningId { get; }
}

/// <summary>
/// V1-RMD-316 (independent 2026-09-26 audit, finding K9): raised when the user whose OWN behaviour
/// triggered the tightening attempts to clear it themselves (same self-approval gap as
/// <see cref="ALKAROS.Identity.Authorization.Grants.AuthorizationSelfApprovalException"/>, one schema
/// over - a small establishment's manager can be the same person whose spiking auto-grant rate opened
/// this tightening in the first place).
/// </summary>
public sealed class BehaviouralTighteningSelfClearException : Exception
{
    public BehaviouralTighteningSelfClearException(Guid tighteningId, Guid userId)
        : base($"User {userId} cannot clear their own behavioural tightening {tighteningId}.")
    {
        TighteningId = tighteningId;
        UserId = userId;
    }

    public Guid TighteningId { get; }

    public Guid UserId { get; }
}
