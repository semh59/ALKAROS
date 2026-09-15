using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Host.Experience.WebPush;
using ALKAROS.Orders.Integration;
using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.Experience.PendingOrderNotifications;

/// <summary>
/// V1-RMD-149: the Host end of <see cref="IPendingOrderAnnouncer"/> — pushes
/// the announcement over <see cref="WaiterOrderStatusHub"/>.
///
/// V1-RMD-202: a brand-new QR order has no <c>ServingUserId</c> yet (nobody
/// has taken it), so unlike the "item is ready" path (V1-RMD-201, which
/// targets the order's own serving waiter) this one has to pick a
/// <see cref="SuggestedWaiterResolver">candidate</see>. When no candidate
/// exists (nobody is logged in) this falls back to the original flat
/// broadcast, so an announcement is never silently dropped.
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
    private readonly WaiterPresenceTracker _presence;
    private readonly SuggestedWaiterResolver _suggestedWaiter;
    private readonly WebPushSender? _push;

    public SignalRPendingOrderAnnouncer(
        IHubContext<WaiterOrderStatusHub> hub,
        WaiterPresenceTracker presence,
        SuggestedWaiterResolver suggestedWaiter,
        WebPushSender? push = null)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _suggestedWaiter = suggestedWaiter ?? throw new ArgumentNullException(nameof(suggestedWaiter));
        _push = push;
    }

    public async Task AnnounceAsync(PendingOrderAnnouncement announcement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        var suggested = await _suggestedWaiter.ResolveMostSuitableWaiterAsync(announcement.TableId, cancellationToken);
        var waiterId = suggested?.UserId;

        // Same wording as the in-app banner: what happened at the table, not
        // which channel it arrived through (docs/UI_STYLE_GUIDE.md).
        var pushMessage = _push is null
            ? null
            : new WebPushMessage(
                "Misafir siparişi",
                $"{announcement.TableNumber} masası sipariş verdi — {announcement.ItemCount} kalem",
                "alkaros-pending-order");

        // V1-RMD-209: per-channel targeting/fallback (V1-RMD-203: the
        // resolved candidate only holds a live identity.device_sessions
        // row, which Cashier/PosTerminal sessions also satisfy — neither
        // ever connects to this hub or registers push, so reachability is
        // checked again per channel) lives in WaiterNotificationDispatch,
        // shared with KitchenOperationsStore's own "item ready" path.
        await WaiterNotificationDispatch.SendAsync(
            _hub, _presence, _push, waiterId,
            WaiterOrderStatusHub.OrderPendingConfirmation, announcement, pushMessage, cancellationToken);
    }
}
