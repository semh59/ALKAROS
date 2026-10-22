using System.Collections.Concurrent;

namespace ALKAROS.Host.Experience.HelpRequests;

/// <summary>
/// How many manager sessions are connected to <see cref="HelpRequestHub"/> right now. A SignalR group send
/// reports nothing about who received it, so the raise endpoint reads this to tell the waiter honestly
/// whether anyone was reached live. Single Host instance, in memory, like WaiterPresenceTracker.
/// </summary>
public sealed class HelpRequestRecipientTracker
{
    private readonly ConcurrentDictionary<string, byte> _connections = new(StringComparer.Ordinal);

    public int Count => _connections.Count;

    public void Connected(string connectionId) => _connections[connectionId] = 0;

    public void Disconnected(string connectionId) => _connections.TryRemove(connectionId, out _);
}
