using ALKAROS.Identity.Authorization.Grants;

namespace ALKAROS.Identity.Authorization.Offline;

/// <summary>
/// Writes a reconciled offline action to the grant ledger. One call inserts the
/// <c>identity.authorization_grants</c> row and its
/// <c>identity.offline_authority_replays</c> link atomically, so an offline
/// grant is never recorded without its budget attribution.
/// </summary>
public interface IOfflineReplayLedger
{
    /// <summary>
    /// Inserts <paramref name="draftGrant"/> and links it to
    /// <paramref name="budgetId"/>. Returns the stored grant. The draft carries a
    /// terminal status with <see cref="AuthorizationGrant.Path"/> set (a denied
    /// reconciliation) or <see cref="GrantStatus.Pending"/> (queued for a
    /// manager).
    /// </summary>
    Task<AuthorizationGrant> RecordAsync(
        AuthorizationGrant draftGrant,
        Guid budgetId,
        DateTimeOffset offlineAuthorizedAt,
        DateTimeOffset reconciledAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many offline grants for <paramref name="permissionCode"/> have already
    /// been reconciled against <paramref name="budgetId"/> without being denied —
    /// the count already spent against that budget line.
    /// </summary>
    Task<int> CountReconciledAsync(
        Guid budgetId, string permissionCode, CancellationToken cancellationToken = default);
}
