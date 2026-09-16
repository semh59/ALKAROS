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

    // V1-RMD-226: found by an independent audit (2026-09-16) - this used to
    // be two separate atomic operations (AddOrUpdate the count down, then a
    // conditional Remove once it hit zero), each atomic on its own but not
    // together: in the window between them the dictionary held {userId: 0},
    // and the OLD IsConnected (a plain ContainsKey) read that as "still
    // connected". IsConnected below reads the count itself instead of key
    // presence, so a zero entry left sitting in the dictionary (this method
    // no longer removes it at all) is never mistaken for a live connection
    // - the entry becoming a permanent zero-value once it reaches zero is a
    // one-time, per-distinct-user cost, not the unbounded growth the
    // original Remove was guarding against.
    public void Disconnected(Guid userId)
        => _connectionCounts.AddOrUpdate(userId, 0, (_, count) => count > 0 ? count - 1 : 0);

    public bool IsConnected(Guid userId) => _connectionCounts.TryGetValue(userId, out var count) && count > 0;
}
