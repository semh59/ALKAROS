namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// V1-RMD-287: tells the till that its queue of checks waiting for payment changed (a waiter sent a check
/// over, or one was taken back), so the screen refreshes at once instead of on its next poll. Kept as a
/// small port so the order endpoints never depend on SignalR; a host without live connections uses
/// <see cref="NoOpCashierQueueAnnouncer"/>.
/// </summary>
public interface ICashierQueueAnnouncer
{
    /// <param name="change"><c>Sent</c> or <c>Recalled</c>.</param>
    Task AnnouncePendingChecksChangedAsync(string change, Guid orderId, Guid tableId, CancellationToken cancellationToken = default);
}

public sealed class NoOpCashierQueueAnnouncer : ICashierQueueAnnouncer
{
    public Task AnnouncePendingChecksChangedAsync(string change, Guid orderId, Guid tableId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
