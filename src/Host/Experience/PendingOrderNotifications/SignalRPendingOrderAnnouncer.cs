using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Host.Experience.WebPush;
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
///
/// V1-WTR-011: and over Web Push, for a device whose app is closed. The two
/// channels are deliberately both used rather than one chosen: SignalR is
/// instant but only reaches an open app, while a push survives a locked
/// screen. A device that receives both shows one notification — the service
/// worker and the in-page handler share the tag "alkaros-pending-order".
/// </summary>
public sealed class SignalRPendingOrderAnnouncer : IPendingOrderAnnouncer
{
    private readonly IHubContext<WaiterOrderStatusHub> _hub;
    private readonly WebPushSender? _push;

    public SignalRPendingOrderAnnouncer(IHubContext<WaiterOrderStatusHub> hub, WebPushSender? push = null)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _push = push;
    }

    public async Task AnnounceAsync(PendingOrderAnnouncement announcement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        await _hub.Clients.All.SendAsync(
            WaiterOrderStatusHub.OrderPendingConfirmation, announcement, cancellationToken);

        if (_push is null) return;

        // Same wording as the in-app banner: what happened at the table, not
        // which channel it arrived through (docs/UI_STYLE_GUIDE.md).
        await _push.BroadcastAsync(
            new WebPushMessage(
                "Misafir siparişi",
                $"{announcement.TableNumber} masası sipariş verdi — {announcement.ItemCount} kalem",
                "alkaros-pending-order"),
            cancellationToken);
    }
}
