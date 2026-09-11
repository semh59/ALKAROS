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

    /// <summary>
    /// V1-WTR-012: total <paramref name="amount"/> already granted to
    /// <paramref name="requesterUserId"/> for <paramref name="permissionCode"/>
    /// under <see cref="PolicyPath.PersonalBudget"/> with <c>resolved_at</c> at
    /// or after <paramref name="since"/> — feeds the personal daily comp
    /// budget check. 0 when nothing has been granted yet.
    /// </summary>
    Task<decimal> SumPersonalBudgetGrantedSinceAsync(
        Guid requesterUserId,
        string permissionCode,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Found in an independent review (2026-09-11): <see cref="CountAutoGrantsSinceAsync"/>
    /// and <see cref="SumPersonalBudgetGrantedSinceAsync"/> each read a
    /// running total that a separate, later <see cref="InsertAsync"/> call
    /// then adds to — with nothing tying the read and the write together,
    /// two concurrent requests from the same requester for the same
    /// permission could both read the same pre-insert total and both pass a
    /// cap (auto_within's count, the personal comp budget's daily sum) meant
    /// to block the second one. Acquires a session-scoped Postgres advisory
    /// lock keyed by <paramref name="requesterUserId"/> +
    /// <paramref name="permissionCode"/>, held until the returned handle is
    /// disposed — serializes exactly that (requester, permission) pair;
    /// every other pair is completely unaffected. The caller
    /// (<see cref="IAuthorizationGrantService"/>) holds it across its own
    /// read-check-then-insert sequence.
    /// </summary>
    Task<IAsyncDisposable> AcquireRequesterLockAsync(
        Guid requesterUserId, string permissionCode, CancellationToken cancellationToken = default);
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
