namespace ALKAROS.Clients.WaiterPwa.OrderStatus;

/// <summary>
/// Domain controller for Waiter PWA Order Status view (V1-WTR-003, PDF:I.8, PDF:I.16, V0-CMP-005).
/// Enforces read-only status tracking, stale data indication on disconnect, and authoritative server convergence on reconnect.
/// </summary>
public sealed class WaiterOrderStatusEngine
{
    private readonly List<WaiterOrderStatusItem> _orders = new();
    private bool _isConnected = true;
    private bool _isStale;
    private DateTimeOffset _lastSyncedAt = DateTimeOffset.UtcNow;

    public WaiterOrderStatusState CurrentState => new(
        _orders.AsReadOnly(),
        _isConnected,
        _isStale,
        _lastSyncedAt);

    /// <summary>
    /// Handles disconnection (e.g. SignalR / network drop). Marks current UI state as stale (Acceptance Evidence #1).
    /// </summary>
    public void HandleDisconnection()
    {
        _isConnected = false;
        _isStale = true;
    }

    /// <summary>
    /// Handles successful reconnection and applies authoritative server snapshot (Acceptance Evidence #2).
    /// </summary>
    public void HandleReconnection(IEnumerable<WaiterOrderStatusItem> serverSnapshot, DateTimeOffset? utcNow = null)
    {
        _orders.Clear();
        if (serverSnapshot is not null)
        {
            _orders.AddRange(serverSnapshot);
        }

        _isConnected = true;
        _isStale = false;
        _lastSyncedAt = utcNow ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Applies incremental real-time update from server.
    /// </summary>
    public void ApplyServerOrderUpdate(WaiterOrderStatusItem updatedOrder, DateTimeOffset? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(updatedOrder);

        var index = _orders.FindIndex(o => o.OrderId == updatedOrder.OrderId);
        if (index >= 0)
        {
            _orders[index] = updatedOrder;
        }
        else
        {
            _orders.Insert(0, updatedOrder);
        }

        _lastSyncedAt = utcNow ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Status tracking view is strictly read-only for waiters and forbids kitchen status mutation (Acceptance Evidence #3).
    /// </summary>
    public static bool TryMutateKitchenStatus(Guid ticketItemId, string newStatus, out string error)
    {
        error = "Garson PWA arayüzü mutfak durumunu doğrudan değiştiremez. Mutfak durumu yalnızca KDS veya mutfak yetkilisi tarafından güncellenebilir.";
        return false;
    }
}
