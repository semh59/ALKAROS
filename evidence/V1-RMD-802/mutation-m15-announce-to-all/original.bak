using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.Experience.WaiterNotifications;

/// <summary>
/// Pushes <see cref="WaiterOrderStatusHub.PendingChecksChanged"/> to the sessions that may read the till's queue
/// (those holding <c>orders.create</c>, joined on connect). Best effort: a failed push must never fail the send or recall that caused it - the till
/// still refreshes on its slower poll.
/// </summary>
public sealed class SignalRCashierQueueAnnouncer : ICashierQueueAnnouncer
{
    private readonly IHubContext<WaiterOrderStatusHub> _hub;

    public SignalRCashierQueueAnnouncer(IHubContext<WaiterOrderStatusHub> hub)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    }

    public async Task AnnouncePendingChecksChangedAsync(string change, Guid orderId, Guid? tableId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group(WaiterOrderStatusHub.CashierQueueGroup).SendAsync(
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
