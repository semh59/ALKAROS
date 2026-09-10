using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Orders.Integration;
using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.Experience.PendingOrderNotifications;

/// <summary>
/// V1-RMD-149: the Host end of <see cref="IPendingOrderAnnouncer"/> — pushes
/// the announcement to every connected waiter device over
/// <see cref="WaiterOrderStatusHub"/>.
///
/// Flat broadcast, like the hub's existing "item is ready" push and for the
/// same reason: nothing in this system records which waiter serves which
/// table, so there is no group to target. The payload carries the table so a
/// device can decide what to show.
/// </summary>
public sealed class SignalRPendingOrderAnnouncer : IPendingOrderAnnouncer
{
    private readonly IHubContext<WaiterOrderStatusHub> _hub;

    public SignalRPendingOrderAnnouncer(IHubContext<WaiterOrderStatusHub> hub)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    }

    public Task AnnounceAsync(PendingOrderAnnouncement announcement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        return _hub.Clients.All.SendAsync(
            WaiterOrderStatusHub.OrderPendingConfirmation, announcement, cancellationToken);
    }
}
