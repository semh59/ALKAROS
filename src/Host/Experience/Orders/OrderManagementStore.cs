using System.Collections.Concurrent;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;

namespace ALKAROS.Host.Experience.Orders;

public sealed class OrderManagementStore
{
    private readonly ConcurrentDictionary<Guid, OrderDto> _orders = new();
    private readonly ConcurrentDictionary<Guid, Guid> _tableToActiveOrder = new();

    public Task<OrderDto> CreateOrUpdateTableDraftAsync(CreateTableDraftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var orderId = _tableToActiveOrder.GetOrAdd(request.TableId, _ => Guid.NewGuid());
        
        var items = request.Items.Select(i => new OrderItemDto(
            Guid.NewGuid(),
            i.ProductId,
            i.ProductName,
            i.Quantity,
            i.UnitPrice,
            i.Quantity * i.UnitPrice,
            i.SpecialInstructions
        )).ToList();

        var totalAmount = items.Sum(i => i.TotalPrice);

        var orderDto = new OrderDto(
            orderId,
            request.TableId,
            request.TableNumber,
            "Draft",
            1,
            totalAmount,
            items,
            DateTimeOffset.UtcNow
        );

        _orders[orderId] = orderDto;
        return Task.FromResult(orderDto);
    }

    public Task<OrderDto?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        _orders.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }

    public Task<OrderDto?> GetActiveOrderByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        if (_tableToActiveOrder.TryGetValue(tableId, out var orderId) && _orders.TryGetValue(orderId, out var order))
        {
            return Task.FromResult<OrderDto?>(order);
        }
        return Task.FromResult<OrderDto?>(null);
    }

    public Task<OrderDto> SubmitOrderAsync(Guid orderId, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(orderId, out var order))
        {
            throw new KeyNotFoundException($"Order {orderId} not found.");
        }

        if (order.RowVersion != expectedRowVersion)
        {
            throw new InvalidOperationException($"Concurrency conflict: expected version {expectedRowVersion}, current is {order.RowVersion}.");
        }

        var submittedOrder = order with
        {
            Status = "Submitted",
            RowVersion = order.RowVersion + 1
        };

        _orders[orderId] = submittedOrder;
        return Task.FromResult(submittedOrder);
    }
}
