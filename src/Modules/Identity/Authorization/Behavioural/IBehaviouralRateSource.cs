namespace ALKAROS.Identity.Authorization.Behavioural;

/// <summary>
/// The per-user grant rate, read straight off <c>identity.authorization_grants</c>.
/// </summary>
public interface IBehaviouralRateSource
{
    /// <summary>
    /// How many <c>granted</c> grants <paramref name="userId"/> has for
    /// <paramref name="permissionCode"/> with <c>resolved_at</c> at or after
    /// <paramref name="since"/>.
    /// </summary>
    Task<int> CountGrantedSinceAsync(
        Guid userId,
        string permissionCode,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}
