using ALKAROS.Host.DualScreen;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// Refactor step 5/7 (docs/engineering/garson-refactor-plan.md, 2026-09-12):
/// <see cref="SubmitOrderAsync"/> and <see cref="FireCourseAsync"/>,
/// extracted out of the former god-class <c>OrderManagementStore</c> — both
/// are "push a round of items forward to the kitchen" actions
/// (<see cref="SubmitOrderHandler"/>/<see cref="Order.FireCourse"/>
/// respectively), the family the plan calls the submission coordinator.
/// </summary>
public sealed class OrderSubmissionCoordinator
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;
    private readonly SubmitOrderHandler _submitHandler;
    private readonly IKitchenTicketRepository _kitchenTickets;
    private readonly OrderDtoAssembler _assembler;

    public OrderSubmissionCoordinator(
        NpgsqlDataSource dataSource,
        IOrderRepository repository,
        SubmitOrderHandler submitHandler,
        IKitchenTicketRepository kitchenTickets,
        OrderDtoAssembler assembler)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _submitHandler = submitHandler ?? throw new ArgumentNullException(nameof(submitHandler));
        _kitchenTickets = kitchenTickets ?? throw new ArgumentNullException(nameof(kitchenTickets));
        _assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
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

        return await ReloadOrderDtoAsync(orderId, cancellationToken)
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
    /// Same shape as <c>OrderManagementStore.GetOrderByIdAsync</c>/
    /// <c>OrderReadStore.GetOrderByIdAsync</c> — kept as its own private
    /// copy rather than a cross-service call so this coordinator does not
    /// need to depend on the read-side store for one reload after submit.
    /// </summary>
    private async Task<OrderDto?> ReloadOrderDtoAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await _assembler.GetTableNumberAsync(order.TableId, cancellationToken) ?? "—";
        var dto = OrderDtoAssembler.MapToDto(order, tableNumber);
        return await _assembler.WithAvailableStockAsync(dto, cancellationToken);
    }
}
