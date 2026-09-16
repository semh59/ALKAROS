using Xunit;

namespace ALKAROS.Host.Experience.WaiterNotifications.Tests;

public sealed class WaiterPresenceTrackerTests
{
    [Fact]
    public void AnUnknownUserIsNotConnected()
    {
        var tracker = new WaiterPresenceTracker();
        Assert.False(tracker.IsConnected(Guid.NewGuid()));
    }

    [Fact]
    public void ConnectedMarksTheUserPresent()
    {
        var tracker = new WaiterPresenceTracker();
        var userId = Guid.NewGuid();

        tracker.Connected(userId);

        Assert.True(tracker.IsConnected(userId));
    }

    [Fact]
    public void DisconnectedMarksTheUserAbsentAfterItsOnlyConnectionCloses()
    {
        var tracker = new WaiterPresenceTracker();
        var userId = Guid.NewGuid();
        tracker.Connected(userId);

        tracker.Disconnected(userId);

        Assert.False(tracker.IsConnected(userId));
    }

    [Fact]
    public void ASecondDeviceKeepsTheUserPresentUntilBothDisconnect()
    {
        var tracker = new WaiterPresenceTracker();
        var userId = Guid.NewGuid();
        tracker.Connected(userId);
        tracker.Connected(userId); // second tab/device

        tracker.Disconnected(userId);
        Assert.True(tracker.IsConnected(userId));

        tracker.Disconnected(userId);
        Assert.False(tracker.IsConnected(userId));
    }

    [Fact]
    public void AnExtraDisconnectedCallNeverGoesNegativeOrThrows()
    {
        var tracker = new WaiterPresenceTracker();
        var userId = Guid.NewGuid();

        tracker.Disconnected(userId);

        Assert.False(tracker.IsConnected(userId));
    }

    /// <summary>
    /// V1-RMD-226: found by an independent audit (2026-09-16) - Disconnected
    /// used to be two separate atomic steps (decrement the count, then
    /// remove the entry once it hit zero), leaving a window where the
    /// dictionary held {userId: 0} and the old IsConnected (a plain
    /// ContainsKey) read that as "still connected". IsConnected now reads
    /// the count itself, so a lingering zero entry (Disconnected no longer
    /// removes it at all) can never be mistaken for a live connection -
    /// proven here by reconnecting and disconnecting the same user several
    /// times, which repeatedly re-uses that same lingering dictionary entry.
    /// </summary>
    [Fact]
    public void RepeatedConnectDisconnectCyclesOnTheSameUserNeverLeaveAStaleConnectedReading()
    {
        var tracker = new WaiterPresenceTracker();
        var userId = Guid.NewGuid();

        for (var cycle = 0; cycle < 5; cycle++)
        {
            tracker.Connected(userId);
            Assert.True(tracker.IsConnected(userId));

            tracker.Disconnected(userId);
            Assert.False(tracker.IsConnected(userId));
        }
    }
}
