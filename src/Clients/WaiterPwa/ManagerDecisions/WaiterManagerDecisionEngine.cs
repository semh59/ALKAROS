namespace ALKAROS.Clients.WaiterPwa.ManagerDecisions;

/// <summary>
/// Domain controller for the Waiter PWA manager decision view (V1-IAM-020):
/// a floor supervisor on a phone sees the pending authorization requests, and
/// approve / deny is a server call. The engine keeps the list read-only,
/// converges on the authoritative server snapshot after a reconnect, marks the
/// view stale on disconnect, and hides a request the supervisor has just
/// resolved until the next snapshot confirms it.
/// </summary>
public sealed class WaiterManagerDecisionEngine
{
    private readonly List<WaiterPendingGrant> _pending = new();
    private readonly HashSet<Guid> _locallyResolved = new();
    private bool _isConnected = true;
    private bool _isStale;
    private DateTimeOffset _lastSyncedAt = DateTimeOffset.UtcNow;

    public WaiterManagerDecisionState CurrentState => new(
        _pending.Where(grant => !_locallyResolved.Contains(grant.GrantId)).ToList().AsReadOnly(),
        _isConnected,
        _isStale,
        _lastSyncedAt);

    /// <summary>Network / SignalR drop: the current list is now stale.</summary>
    public void HandleDisconnection()
    {
        _isConnected = false;
        _isStale = true;
    }

    /// <summary>Reconnect: replace the list with the authoritative server snapshot.</summary>
    public void HandleReconnection(
        IEnumerable<WaiterPendingGrant> serverSnapshot, DateTimeOffset? utcNow = null)
    {
        ApplyServerSnapshot(serverSnapshot, utcNow);
        _isConnected = true;
        _isStale = false;
    }

    /// <summary>A real-time snapshot from the server while connected.</summary>
    public void ApplyServerSnapshot(
        IEnumerable<WaiterPendingGrant> serverSnapshot, DateTimeOffset? utcNow = null)
    {
        _pending.Clear();
        if (serverSnapshot is not null)
            _pending.AddRange(serverSnapshot);

        // A local optimistic hide is only kept while the server still lists it;
        // once the snapshot drops the row the hide has served its purpose.
        _locallyResolved.RemoveWhere(id => _pending.All(grant => grant.GrantId != id));
        _lastSyncedAt = utcNow ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records that the supervisor successfully approved or denied
    /// <paramref name="grantId"/> on the server, so it drops out of the view
    /// before the next snapshot arrives.
    /// </summary>
    public void MarkResolvedLocally(Guid grantId)
    {
        if (_pending.Any(grant => grant.GrantId == grantId))
            _locallyResolved.Add(grantId);
    }

    /// <summary>
    /// The manager view never edits kitchen or bill state directly; the only
    /// action is a server-side approve / deny of an authorization request.
    /// </summary>
    public static bool TryMutateDirectly(out string error)
    {
        error = "Garson PWA yönetici görünümü yalnızca yetki isteğini onaylar veya reddeder; "
            + "hesap ya da mutfak durumu bu ekrandan değiştirilemez.";
        return false;
    }
}
