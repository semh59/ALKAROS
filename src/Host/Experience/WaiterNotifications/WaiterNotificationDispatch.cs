using ALKAROS.Host.Experience.WebPush;
using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.Experience.WaiterNotifications;

/// <summary>
/// V1-RMD-209: the "target one waiter, fall back to broadcast independently
/// per channel" decision V1-RMD-201/202/203 established, deduplicated out
/// of two call sites (<c>KitchenOperationsStore</c>,
/// <c>SignalRPendingOrderAnnouncer</c>) that had each grown their own copy —
/// found by an independent review (2026-09-15) after one copy's SignalR
/// half diverged from the other's push half in a way neither author caught
/// in isolation.
/// </summary>
public static class WaiterNotificationDispatch
{
    /// <summary>
    /// Sends <paramref name="hubPayload"/> over <paramref name="hubMethod"/>
    /// to <paramref name="targetUserId"/>'s own SignalR group when they are
    /// currently connected (<paramref name="presence"/>), otherwise to
    /// every connected device; then, if <paramref name="push"/> and
    /// <paramref name="pushMessage"/> are both given, the same decision
    /// independently for push (a device can be live on one channel and not
    /// the other). <paramref name="targetUserId"/> null (no known serving
    /// waiter) always broadcasts on both.
    /// </summary>
    public static async Task SendAsync(
        IHubContext<WaiterOrderStatusHub> hub,
        WaiterPresenceTracker presence,
        WebPushSender? push,
        Guid? targetUserId,
        string hubMethod,
        object hubPayload,
        WebPushMessage? pushMessage,
        CancellationToken cancellationToken)
    {
        var recipients = targetUserId is Guid connectedId && presence.IsConnected(connectedId)
            ? hub.Clients.Group(WaiterOrderStatusHub.GroupName(connectedId))
            : hub.Clients.All;
        await recipients.SendAsync(hubMethod, hubPayload, cancellationToken);

        if (push is null || pushMessage is null) return;

        if (targetUserId is Guid pushId)
            await push.SendToUserOrBroadcastAsync(pushMessage, pushId, cancellationToken);
        else
            await push.BroadcastAsync(pushMessage, cancellationToken);
    }
}
