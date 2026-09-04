namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>Reads and writes <c>identity.authorization_grants</c>.</summary>
public interface IAuthorizationGrantRepository
{
    /// <summary>The grant for an idempotency key, or null when the key is new.</summary>
    Task<AuthorizationGrant?> FindByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>One grant by id, or null.</summary>
    Task<AuthorizationGrant?> GetAsync(Guid grantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new grant row. <paramref name="grant"/> must be
    /// <see cref="GrantStatus.Pending"/> with no resolution fields, or a terminal
    /// status with <see cref="AuthorizationGrant.Path"/> set — the caller
    /// (<see cref="IAuthorizationGrantService"/>) enforces which.
    /// </summary>
    Task<AuthorizationGrant> InsertAsync(
        AuthorizationGrant grant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a pending grant. Returns the resolved row; throws
    /// <see cref="AuthorizationGrantAlreadyResolvedException"/> when the grant is
    /// no longer pending (the database trigger is the backstop).
    /// </summary>
    Task<AuthorizationGrant> ResolveAsync(
        Guid grantId,
        GrantStatus status,
        PolicyPath path,
        Guid? approverUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many <see cref="PolicyPath.Auto"/> grants the requester has for this
    /// permission with <c>resolved_at</c> at or after <paramref name="since"/>.
    /// Feeds the <c>auto_within</c> count check.
    /// </summary>
    Task<int> CountAutoGrantsSinceAsync(
        Guid requesterUserId,
        string permissionCode,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Every pending grant, oldest first. Consumed by the manager surface.</summary>
    Task<IReadOnlyList<AuthorizationGrant>> ListPendingAsync(CancellationToken cancellationToken = default);
}

/// <summary>Raised when resolving a grant that is already terminal.</summary>
public sealed class AuthorizationGrantAlreadyResolvedException : Exception
{
    public AuthorizationGrantAlreadyResolvedException(Guid grantId)
        : base($"Authorization grant {grantId} is already resolved.")
    {
        GrantId = grantId;
    }

    public Guid GrantId { get; }
}
