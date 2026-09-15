using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace ALKAROS.Host.Experience.KitchenOperations.Tests;

/// <summary>
/// V1-RMD-201: <see cref="KitchenOperationsStore.DispatchItemReadyNotificationAsync"/>
/// is a static method that takes its collaborators as parameters precisely so
/// this targeting decision can be exercised without a full
/// <see cref="KitchenOperationsStore"/> (which needs a live database for its
/// other dependencies).
/// </summary>
public sealed class KitchenOperationsStoreNotificationDispatchTests
{
    [Fact]
    public async Task TargetsOnlyTheServingWaitersGroupWhenOneIsKnownAndConnected()
    {
        var waiterId = Guid.NewGuid();
        var order = NewOrder(waiterId);
        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        presence.Connected(waiterId);
        var item = NewItem();

        await KitchenOperationsStore.DispatchItemReadyNotificationAsync(
            hub, presence, push: null, new FakeOrderRepository(order), order.Id, item, CancellationToken.None);

        var group = Assert.Single(hub.Clients.GroupCalls);
        Assert.Equal(WaiterOrderStatusHub.GroupName(waiterId), group);
        Assert.Equal(0, hub.Clients.AllCalls);
        var sent = Assert.Single(hub.Clients.Sent);
        Assert.Equal(WaiterOrderStatusHub.OrderItemReady, sent.Method);
    }

    [Fact]
    public async Task BroadcastsWhenTheServingWaiterHasNoLiveHubConnection()
    {
        // V1-RMD-203: the regression this guards against — a table opened
        // from Cashier/PosTerminal makes that staff member ServingUserId,
        // but neither client ever connects to this hub. Targeting them
        // would silently reach nobody.
        var waiterId = Guid.NewGuid();
        var order = NewOrder(waiterId);
        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        var item = NewItem();

        await KitchenOperationsStore.DispatchItemReadyNotificationAsync(
            hub, presence, push: null, new FakeOrderRepository(order), order.Id, item, CancellationToken.None);

        Assert.Equal(1, hub.Clients.AllCalls);
        Assert.Empty(hub.Clients.GroupCalls);
    }

    [Fact]
    public async Task BroadcastsWhenTheOrderHasNoServingWaiter()
    {
        var order = NewOrder(servingUserId: null);
        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        var item = NewItem();

        await KitchenOperationsStore.DispatchItemReadyNotificationAsync(
            hub, presence, push: null, new FakeOrderRepository(order), order.Id, item, CancellationToken.None);

        Assert.Equal(1, hub.Clients.AllCalls);
        Assert.Empty(hub.Clients.GroupCalls);
    }

    [Fact]
    public async Task BroadcastsWhenTheOrderIsMissingEntirely()
    {
        var hub = new RecordingHubContext();
        var presence = new WaiterPresenceTracker();
        var item = NewItem();

        await KitchenOperationsStore.DispatchItemReadyNotificationAsync(
            hub, presence, push: null, new FakeOrderRepository(order: null), Guid.NewGuid(), item, CancellationToken.None);

        Assert.Equal(1, hub.Clients.AllCalls);
        Assert.Empty(hub.Clients.GroupCalls);
    }

    private static KitchenTicketItem NewItem() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Mercimek çorbası", 1);

    private static Order NewOrder(Guid? servingUserId) =>
        new(Guid.NewGuid(), OrderSource.Waiter, "ORD-1", items: [], servingUserId: servingUserId);
}

/// <summary>Only <see cref="GetByIdAsync"/> is exercised by the code under test.</summary>
internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly Order? _order;

    public FakeOrderRepository(Order? order) => _order = order;

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_order);

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task AddAsync(
        Order order, Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<long> SaveAsync(Order order, long expectedRowVersion, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<long> SaveAsync(
        Order order, long expectedRowVersion, Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> ReparentActiveOrdersToTableAsync(
        Guid fromTableId, Guid toTableId, DateTimeOffset timestamp,
        Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> ReparentOrderToTableAsync(
        Guid orderId, Guid expectedFromTableId, Guid toTableId, DateTimeOffset timestamp,
        Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> ReassignServingUserAsync(
        Guid fromUserId, Guid toUserId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>Records which selector (<c>All</c> vs <c>Group</c>) a send used, and the payload sent.</summary>
internal sealed class RecordingHubContext : IHubContext<WaiterOrderStatusHub>
{
    public RecordingClients Clients { get; } = new();

    IHubClients IHubContext<WaiterOrderStatusHub>.Clients => Clients;

    public IGroupManager Groups => throw new NotSupportedException();
}

internal sealed class RecordingClients : IHubClients
{
    public int AllCalls { get; private set; }

    public List<string> GroupCalls { get; } = [];

    public List<(string Method, object?[] Args)> Sent { get; } = [];

    public IClientProxy All
    {
        get
        {
            AllCalls++;
            return new RecordingProxy(Sent);
        }
    }

    public IClientProxy Group(string groupName)
    {
        GroupCalls.Add(groupName);
        return new RecordingProxy(Sent);
    }

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

    public IClientProxy Client(string connectionId) => throw new NotSupportedException();

    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
        => throw new NotSupportedException();

    public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

    public IClientProxy OthersInGroup(string groupName) => throw new NotSupportedException();

    public IClientProxy User(string userId) => throw new NotSupportedException();

    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
}

internal sealed class RecordingProxy : IClientProxy
{
    private readonly List<(string Method, object?[] Args)> _sent;

    public RecordingProxy(List<(string Method, object?[] Args)> sent) => _sent = sent;

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        _sent.Add((method, args));
        return Task.CompletedTask;
    }
}
