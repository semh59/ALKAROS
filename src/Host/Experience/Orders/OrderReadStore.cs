using ALKAROS.Identity.Authorization;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// Refactor step 6/7 (docs/engineering/garson-refactor-plan.md, 2026-09-12):
/// the former god-class <c>OrderManagementStore</c>'s own final remainder,
/// renamed rather than further split. <see cref="GetOrderByIdAsync"/>,
/// <see cref="GetActiveOrderByTableIdAsync"/>,
/// <see cref="TransferServingUserAsync"/> and
/// <see cref="GetPendingOrdersAsync"/> share no real cohesion with each
/// other — the plan's own Section 1.3 deliberately left this as one small
/// grab-bag group instead of forcing a fake shared abstraction on four
/// unrelated single-endpoint reads/writes; a real common point (e.g. a
/// shared authorization pattern) would be the trigger to split it further,
/// not file size alone.
/// </summary>
public sealed class OrderReadStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;
    private readonly IRoleRepository _roles;
    private readonly OrderDtoAssembler _assembler;

    public OrderReadStore(
        NpgsqlDataSource dataSource,
        IOrderRepository repository,
        IRoleRepository roles,
        OrderDtoAssembler assembler)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await _assembler.GetTableNumberAsync(order.TableId, cancellationToken) ?? "—";
        var dto = OrderDtoAssembler.MapToDto(order, tableNumber);
        return await _assembler.WithAvailableStockAsync(dto, cancellationToken);
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

        return await _assembler.LoadOrderDtoAsync(orderId, tableId, cancellationToken);
    }

    /// <summary>
    /// The garson-masa hand-off (V1-RMD-111): reassigns every non-terminal
    /// order currently attributed to <paramref name="fromUserId"/> to
    /// <paramref name="toUserId"/>. Callers must already have authorized
    /// orders.transfer-server (self, fromUserId == the acting user) or
    /// orders.transfer-server-any (broad) before calling this — that
    /// decision is not this method's job. Guards only against a no-op
    /// transfer and a target who cannot actually serve.
    /// </summary>
    public async Task<int> TransferServingUserAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken = default)
    {
        if (fromUserId == toUserId)
            throw new InvalidTransferTargetException("A server cannot transfer their own checks to themselves.");

        var (exists, active) = await _roles.GetUserStateAsync(toUserId, cancellationToken);
        if (!exists || !active)
            throw new InvalidTransferTargetException($"Target user {toUserId} does not exist or is not active.");

        return await _repository.ReassignServingUserAsync(fromUserId, toUserId, cancellationToken);
    }

    /// <summary>
    /// V1-RMD-149: orders sitting in PendingConfirmation, oldest first. The
    /// live SignalR announcement can be missed — the app was closed, the
    /// network dropped, the device just connected — and without this a
    /// waiter had no way back to a guest order that is already waiting.
    /// Reads the aggregate's own rows rather than a projection so the totals
    /// match what the confirmation screen will show.
    /// </summary>
    public async Task<IReadOnlyList<PendingOrderSummaryV1>> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<PendingOrderSummaryV1>();
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT o.order_id, o.table_id, COALESCE(t.table_number, ''),
                   count(i.order_item_id), o.total, o.created_at
            FROM orders.orders o
            LEFT JOIN table_mgmt.tables t ON t.table_id = o.table_id
            LEFT JOIN orders.order_items i
              ON i.order_id = o.order_id AND i.status <> 'Cancelled'
            WHERE o.status = 'PendingConfirmation'
            GROUP BY o.order_id, o.table_id, t.table_number, o.total, o.created_at
            ORDER BY o.created_at;
            """);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PendingOrderSummaryV1(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetString(2),
                (int)reader.GetInt64(3),
                reader.GetDecimal(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return results;
    }
}

/// <summary>V1-RMD-111: the requested server hand-off target is invalid.</summary>
public sealed class InvalidTransferTargetException : Exception
{
    public InvalidTransferTargetException(string message) : base(message) { }
}
