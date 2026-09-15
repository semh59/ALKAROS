using System.Collections.Concurrent;

namespace ALKAROS.Host.Experience.WaiterNotifications;

/// <summary>
/// V1-RMD-203: tracks how many live <see cref="WaiterOrderStatusHub"/>
/// connections each user currently has, so a targeted send can check
/// reachability before it decides not to fall back to a broadcast — a
/// SignalR group send gives no feedback about whether it reached anyone.
///
/// A single user can hold more than one connection (two tabs, phone and
/// desktop), hence a count rather than a flag; the entry is removed once
/// it reaches zero so this never grows unbounded over a long-running host.
/// Single-process, in-memory by design (V0-ARC-001: this deployment model
/// has one Host instance).
/// </summary>
public sealed class WaiterPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connectionCounts = new();

    public void Connected(Guid userId)
        => _connectionCounts.AddOrUpdate(userId, 1, (_, count) => count + 1);

    public void Disconnected(Guid userId)
    {
        _connectionCounts.AddOrUpdate(userId, 0, (_, count) => count > 0 ? count - 1 : 0);
        ((ICollection<KeyValuePair<Guid, int>>)_connectionCounts).Remove(new KeyValuePair<Guid, int>(userId, 0));
    }

    public bool IsConnected(Guid userId) => _connectionCounts.ContainsKey(userId);
}
