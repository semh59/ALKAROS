using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.WaiterPwa.OrderStatus.Tests;

public sealed class WaiterOrderStatusTests
{
    private readonly WaiterOrderStatusEngine _engine = new();

    [Fact]
    public void DisconnectionSetsStaleDataFlag()
    {
        var initialOrder = new WaiterOrderStatusItem(
            OrderId: Guid.NewGuid(),
            TableNumber: "M-02",
            OrderStatus: "InKitchen",
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            Items: new List<WaiterTicketItemProgress>
            {
                new(Guid.NewGuid(), "Köfte", 2, "InPrep", false, null)
            });

        _engine.HandleReconnection(new[] { initialOrder });
        _engine.CurrentState.IsConnected.Should().BeTrue();
        _engine.CurrentState.IsStale.Should().BeFalse();

        // Network drops / SignalR disconnected (Acceptance Evidence #1)
        _engine.HandleDisconnection();

        _engine.CurrentState.IsConnected.Should().BeFalse();
        _engine.CurrentState.IsStale.Should().BeTrue();
        _engine.CurrentState.Orders.Should().ContainSingle();
    }

    [Fact]
    public void ReconnectionConvergesToAuthoritativeServerState()
    {
        _engine.HandleDisconnection();

        var syncTime = DateTimeOffset.UtcNow;
        var serverSnapshot = new List<WaiterOrderStatusItem>
        {
            new(
                OrderId: Guid.NewGuid(),
                TableNumber: "B-03",
                OrderStatus: "Ready",
                CreatedAt: syncTime.AddMinutes(-15),
                Items: new List<WaiterTicketItemProgress>
                {
                    new(Guid.NewGuid(), "Pizza", 1, "Ready", false, syncTime.AddMinutes(-1))
                })
        };

        // Reconnect and apply server snapshot (Acceptance Evidence #2)
        _engine.HandleReconnection(serverSnapshot, syncTime);

        var state = _engine.CurrentState;
        state.IsConnected.Should().BeTrue();
        state.IsStale.Should().BeFalse();
        state.LastSyncedAt.Should().Be(syncTime);
        state.Orders.Should().HaveCount(1);
        state.Orders[0].TableNumber.Should().Be("B-03");
        state.Orders[0].HasReadyItems.Should().BeTrue();
    }

    [Fact]
    public void WaiterCannotMutateKitchenTicketStatusThroughView()
    {
        var ticketItemId = Guid.NewGuid();

        // Attempting to mutate kitchen status from Waiter PWA view (Acceptance Evidence #3)
        var result = WaiterOrderStatusEngine.TryMutateKitchenStatus(ticketItemId, "Ready", out var error);

        result.Should().BeFalse();
        error.Should().Contain("Garson PWA arayüzü mutfak durumunu doğrudan değiştiremez");
    }

    [Fact]
    public void RealTimeUpdateReflectsReadyAndCancelledItems()
    {
        var orderId = Guid.NewGuid();
        var order = new WaiterOrderStatusItem(
            OrderId: orderId,
            TableNumber: "T-01",
            OrderStatus: "InKitchen",
            CreatedAt: DateTimeOffset.UtcNow,
            Items: new List<WaiterTicketItemProgress>
            {
                new(Guid.NewGuid(), "Çorba", 1, "Delivered", false, null),
                new(Guid.NewGuid(), "Salata", 1, "Cancelled", true, null)
            });

        _engine.ApplyServerOrderUpdate(order);

        var state = _engine.CurrentState;
        state.Orders.Should().ContainSingle();
        state.Orders[0].HasCancelledItems.Should().BeTrue();
        state.Orders[0].HasReadyItems.Should().BeFalse();
    }
}
