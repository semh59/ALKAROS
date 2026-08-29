using System.Data;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

public sealed class OrderManagementStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;

    public OrderManagementStore(NpgsqlDataSource dataSource, IOrderRepository repository)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<OrderDto> CreateOrUpdateTableDraftAsync(CreateTableDraftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingOrder = await GetActiveOrderByTableIdAsync(request.TableId, cancellationToken);
        var orderId = existingOrder?.OrderId ?? Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var items = new List<OrderItem>();
        foreach (var i in request.Items)
        {
            items.Add(new OrderItem(
                Guid.NewGuid(),
                orderId,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                10.00m,
                skuSnapshot: null,
                discountAmount: 0,
                modifiers: null,
                status: OrderItemState.Draft,
                kitchenState: KitchenState.NotSent,
                portionReservationStatus: PortionReservationStatus.NotApplicable,
                notes: i.SpecialInstructions,
                createdAt: now,
                updatedAt: now
            ));
        }

        var orderNumber = existingOrder != null
            ? $"TBL-{request.TableNumber}-{orderId.ToString("N")[..6].ToUpperInvariant()}"
            : $"TBL-{request.TableNumber}-{now:HHmmssff}";

        var order = new Order(
            orderId,
            OrderSource.Waiter,
            orderNumber,
            items,
            tableId: request.TableId,
            notes: request.OrderNote,
            status: OrderState.Draft,
            createdAt: existingOrder?.CreatedAt ?? now,
            updatedAt: now,
            rowVersion: existingOrder?.RowVersion ?? 1
        );

        if (existingOrder == null)
        {
            await _repository.AddAsync(order, cancellationToken);

            await using var cmd = _dataSource.CreateCommand(
                """
                UPDATE table_mgmt.tables
                SET current_order_id = @order_id,
                    current_status = 'Occupied',
                    row_version = row_version + 1
                WHERE table_id = @table_id;
                """);
            cmd.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = request.TableId;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await _repository.SaveAsync(order, existingOrder.RowVersion, cancellationToken);
        }

        return MapToDto(order, request.TableNumber);
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await GetTableNumberAsync(order.TableId, cancellationToken) ?? "—";
        return MapToDto(order, tableNumber);
    }

    public async Task<OrderDto?> GetActiveOrderByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT order_id
            FROM orders.orders
            WHERE table_id = @table_id
              AND status IN ('Draft', 'Submitted', 'PendingConfirmation', 'Accepted', 'Preparing', 'Ready')
            ORDER BY created_at DESC
            LIMIT 1;
            """);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result is not Guid orderId) return null;

        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await GetTableNumberAsync(tableId, cancellationToken) ?? "—";
        return MapToDto(order, tableNumber);
    }

    public async Task<OrderDto> SubmitOrderAsync(Guid orderId, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        order = order.Submit(changedAt: DateTimeOffset.UtcNow);
        var newVersion = await _repository.SaveAsync(order, expectedRowVersion, cancellationToken);

        var tableNumber = await GetTableNumberAsync(order.TableId, cancellationToken) ?? "—";
        return MapToDto(order, tableNumber);
    }

    private async Task<string?> GetTableNumberAsync(Guid? tableId, CancellationToken cancellationToken)
    {
        if (tableId == null) return null;
        await using var cmd = _dataSource.CreateCommand(
            "SELECT table_number FROM table_mgmt.tables WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId.Value;
        var res = await cmd.ExecuteScalarAsync(cancellationToken);
        return res as string;
    }

    private static OrderDto MapToDto(Order order, string tableNumber)
    {
        var dtos = order.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.ProductNameSnapshot,
            (int)i.Quantity,
            i.UnitPrice,
            i.GrossAmount,
            i.Notes
        )).ToList();

        return new OrderDto(
            order.Id,
            order.TableId ?? Guid.Empty,
            tableNumber,
            order.Status.ToString(),
            order.RowVersion,
            order.Total,
            dtos,
            order.CreatedAt
        );
    }
}
