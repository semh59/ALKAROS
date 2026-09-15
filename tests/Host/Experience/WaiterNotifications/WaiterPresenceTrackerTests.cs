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
}
