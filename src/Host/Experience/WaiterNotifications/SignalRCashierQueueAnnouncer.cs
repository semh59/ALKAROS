using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.Experience.WaiterNotifications;

/// <summary>
/// V1-RMD-287: pushes <see cref="WaiterOrderStatusHub.PendingChecksChanged"/> to every connected device. Same
/// flat broadcast as the rest of this hub (nothing records which till serves which waiter); waiter devices
/// ignore the event. Best effort: a failed push must never fail the send or recall that caused it - the till
/// still refreshes on its slower poll.
/// </summary>
public sealed class SignalRCashierQueueAnnouncer : ICashierQueueAnnouncer
{
    private readonly IHubContext<WaiterOrderStatusHub> _hub;

    public SignalRCashierQueueAnnouncer(IHubContext<WaiterOrderStatusHub> hub)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    }

    public async Task AnnouncePendingChecksChangedAsync(string change, Guid orderId, Guid tableId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.All.SendAsync(
                WaiterOrderStatusHub.PendingChecksChanged,
                new PendingChecksChangedV1(change, orderId, tableId),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Best effort by design (see the class comment).
        }
    }
}
