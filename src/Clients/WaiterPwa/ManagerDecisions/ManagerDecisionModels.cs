namespace ALKAROS.Clients.WaiterPwa.ManagerDecisions;

/// <summary>
/// One pending authorization request as a floor supervisor (şef garson) sees it
/// on the Waiter PWA (V1-IAM-020, authorization model §4 step 3). Read-only
/// context: the supervisor approves or denies on the server; this app never
/// mutates the grant itself.
/// </summary>
public sealed record WaiterPendingGrant(
    Guid GrantId,
    string PermissionCode,
    Guid RequesterUserId,
    string RequesterRoleCode,
    decimal Amount,
    string ReasonCode,
    DateTimeOffset RequestedAt);

/// <summary>Reactive state of the Waiter PWA manager decision view (V1-IAM-020).</summary>
public sealed record WaiterManagerDecisionState(
    IReadOnlyList<WaiterPendingGrant> Pending,
    bool IsConnected,
    bool IsStale,
    DateTimeOffset LastSyncedAt);
