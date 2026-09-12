using System.Data;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

public sealed class OrderManagementStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;
    private readonly IRoleRepository _roles;
    private readonly SubmitOrderHandler _submitHandler;
    private readonly IKitchenTicketRepository _kitchenTickets;
    // Refactor step 1/7 (docs/engineering/garson-refactor-plan.md): every
    // "Order -> OrderDto (+ available-stock enrichment)" concern now lives
    // in one shared place instead of copied private methods here.
    private readonly OrderDtoAssembler _assembler;

    public OrderManagementStore(
        NpgsqlDataSource dataSource,
        IOrderRepository repository,
        IRoleRepository roles,
        SubmitOrderHandler submitHandler,
        IKitchenTicketRepository kitchenTickets,
        OrderDtoAssembler assembler)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _submitHandler = submitHandler ?? throw new ArgumentNullException(nameof(submitHandler));
        _kitchenTickets = kitchenTickets ?? throw new ArgumentNullException(nameof(kitchenTickets));
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
    /// V1-RMD-113: found by an independent audit (2026-09-06) to be a
    /// completely separate, ad-hoc submit path that never created a
    /// kitchen ticket, never checked idempotency (despite the endpoint
    /// already accepting and discarding an OperationId), and never
    /// notified the customer display — unlike the terminal-wide quick-sale
    /// "/submit" route, which had all three via <see cref="SubmitOrderHandler"/>
    /// from day one. This now delegates to that same handler instead of
    /// reimplementing a thinner version of it, so a table order gets the
    /// identical guarantees a quick-sale order already had.
    /// </summary>
    public async Task<OrderDto> SubmitOrderAsync(
        Guid terminalId, Guid orderId, long expectedRowVersion, string operationId, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        await _submitHandler.HandleAsync(
            new SubmitOrderCommand(
                $"cashier:{terminalId:D}", operationId, orderId, expectedRowVersion,
                actorId, DateTimeOffset.UtcNow, "Masa siparişi mutfağa gönderildi."),
            cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} was submitted but could not be reloaded.");
    }

    /// <summary>
    /// V1-WTR-025: calls in one Held course (<see cref="Order.FireCourse"/>)
    /// and prints a dedicated "fire" ticket for exactly the items that
    /// course promotes to Sent — the explicit "fire" action the full
    /// course model needs. No stock dispatch here: unlike a fresh round,
    /// these items already activated (and their stock already consumed) the
    /// moment the order was first fired — only their kitchen state moves.
    ///
    /// Deliberately does NOT reuse <see cref="KitchenOrderSubmissionDispatcher"/>:
    /// that dispatcher's own already-ticketed guard exists to make a RETRY
    /// of the SAME dispatch idempotent, and these exact order items are
    /// already on a ticket by design (the original whole-plan ticket,
    /// printed Held) — the guard would read that as "nothing to do" and
    /// silently skip the fire notice. Idempotency here is the ticket number
    /// itself instead, one per (order, station, course), locked the same
    /// way the dispatcher locks its own ticket creation.
    /// </summary>
    public async Task<OrderDto> FireCourseAsync(
        Guid orderId, int courseNumber, Guid actorId, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");

        var (fired, firedItems) = order.FireCourse(courseNumber, changedBy: actorId);

        var stationId = Environment.GetEnvironmentVariable(DualScreenApplication.KitchenStationEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(stationId))
        {
            throw new InvalidOperationException(
                $"{DualScreenApplication.KitchenStationEnvironmentVariable} is required before a course can be fired.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await _repository.SaveAsync(fired, order.RowVersion, connection, transaction, cancellationToken);

        var ticketNumber = $"KT-{fired.OrderNumber}-{stationId}-FIRE-{courseNumber}";
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@lock_key, 0));";
            lockCommand.Parameters.AddWithValue("lock_key", ticketNumber);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        bool alreadyPrinted;
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = "SELECT EXISTS(SELECT 1 FROM kitchen.kitchen_tickets WHERE ticket_number = @ticket_number);";
            existing.Parameters.AddWithValue("ticket_number", ticketNumber);
            alreadyPrinted = (bool)(await existing.ExecuteScalarAsync(cancellationToken))!;
        }

        if (!alreadyPrinted)
        {
            var firedIdSet = firedItems.Select(item => item.Id).ToHashSet();
            var ticket = KitchenTicket.CreateFromOrder(
                fired,
                stationId,
                ticketNumber: ticketNumber,
                itemFilter: item => firedIdSet.Contains(item.Id));
            await _kitchenTickets.AddAsync(ticket, connection, transaction, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var tableNumber = await _assembler.GetTableNumberAsync(fired.TableId, cancellationToken) ?? string.Empty;
        return OrderDtoAssembler.MapToDto(fired, tableNumber);
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
