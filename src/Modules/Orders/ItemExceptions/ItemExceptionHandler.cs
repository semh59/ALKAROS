namespace ALKAROS.Orders.ItemExceptions;

using System.Text.Json;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

/// <summary>
/// Handles Item Void and Complimentary commands with policy verification,
/// kitchen state validation, and audit recording (V1-ORD-003, PDF:I.24, PDF:I.28.1, V0-DOM-006).
/// </summary>
public sealed class ItemExceptionHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly NpgsqlDataSource _dataSource;

    public ItemExceptionHandler(
        IOrderRepository orderRepository,
        NpgsqlDataSource dataSource)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ItemExceptionResult> VoidItemAsync(
        VoidOrderItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        if (!command.IsManagerAuthorized)
        {
            throw new UnauthorizedItemOperationException("Void", command.ActorId);
        }

        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Order '{command.OrderId}' was not found.");

        if (order.RowVersion != command.ExpectedRowVersion)
        {
            throw new StaleOrderRowVersionException(order.Id, command.ExpectedRowVersion, order.RowVersion);
        }

        var itemIndex = -1;
        for (var i = 0; i < order.Items.Count; i++)
        {
            if (order.Items[i].Id == command.OrderItemId)
            {
                itemIndex = i;
                break;
            }
        }

        if (itemIndex < 0)
        {
            throw new OrderItemNotFoundException(order.Id, command.OrderItemId);
        }

        var targetItem = order.Items[itemIndex];
        if (targetItem.Status != OrderItemState.Active)
        {
            throw new InvalidOperationException(
                $"Order item '{targetItem.Id}' cannot be voided from status {targetItem.Status}.");
        }

        if (targetItem.KitchenState != KitchenState.NotSent)
        {
            throw new LateVoidRejectedException(targetItem.Id, targetItem.KitchenState.ToString());
        }

        var now = DateTimeOffset.UtcNow;
        var voidedItem = targetItem.Cancel();

        var updatedItems = new List<OrderItem>(order.Items);
        updatedItems[itemIndex] = voidedItem;

        var historyReason = string.IsNullOrWhiteSpace(command.Notes)
            ? $"Void:{command.ReasonCode}"
            : $"Void:{command.ReasonCode} - {command.Notes}";

        var historyEntry = new OrderStatusHistoryEntry(
            Guid.NewGuid(),
            order.Id,
            order.Status,
            order.Status,
            historyReason,
            command.ActorId,
            now);

        var updatedHistory = order.History.Append(historyEntry).ToList();

        var updatedOrder = new Order(
            order.Id,
            order.Source,
            order.OrderNumber,
            updatedItems,
            order.TableId,
            order.CustomerId,
            order.SourceReferenceId,
            order.SourceExternalId,
            order.Notes,
            order.Status,
            order.ConfirmationStatus,
            order.CurrencyCode,
            order.SubmittedAt,
            order.AcceptedAt,
            order.ClosedAt,
            order.CancelledAt,
            updatedHistory,
            order.RowVersion,
            order.CreatedAt,
            now);

        var newVersion = await _orderRepository.SaveAsync(updatedOrder, command.ExpectedRowVersion, cancellationToken).ConfigureAwait(false);

        await AppendAuditAsync(
            "Order.ItemVoided",
            order.Id,
            command.ActorId,
            command.ReasonCode,
            command.CorrelationId,
            beforeState: new { ItemId = targetItem.Id, Status = targetItem.Status.ToString(), KitchenState = targetItem.KitchenState.ToString(), Total = targetItem.GrossAmount },
            afterState: new { ItemId = voidedItem.Id, Status = voidedItem.Status.ToString(), KitchenState = voidedItem.KitchenState.ToString(), Total = voidedItem.GrossAmount },
            cancellationToken).ConfigureAwait(false);

        return new ItemExceptionResult(
            order.Id,
            targetItem.Id,
            OrderItemState.Cancelled,
            newVersion,
            updatedOrder.Subtotal,
            updatedOrder.TaxTotal,
            updatedOrder.Total,
            now);
    }

    public async Task<ItemExceptionResult> ApplyComplimentaryAsync(
        ApplyComplimentaryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        if (!command.IsManagerAuthorized)
        {
            throw new UnauthorizedItemOperationException("Complimentary", command.ActorId);
        }

        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Order '{command.OrderId}' was not found.");

        if (order.RowVersion != command.ExpectedRowVersion)
        {
            throw new StaleOrderRowVersionException(order.Id, command.ExpectedRowVersion, order.RowVersion);
        }

        var itemIndex = -1;
        for (var i = 0; i < order.Items.Count; i++)
        {
            if (order.Items[i].Id == command.OrderItemId)
            {
                itemIndex = i;
                break;
            }
        }

        if (itemIndex < 0)
        {
            throw new OrderItemNotFoundException(order.Id, command.OrderItemId);
        }

        var targetItem = order.Items[itemIndex];
        if (targetItem.Status != OrderItemState.Active)
        {
            throw new InvalidOperationException(
                $"Order item '{targetItem.Id}' cannot be marked complimentary from status {targetItem.Status}.");
        }

        var now = DateTimeOffset.UtcNow;

        // Complimentary item retains original quantity, snapshot unit price and tax rate for tax records,
        // while setting effective payable amounts to 0 (PDF:I.28.1, V0-DOM-006).
        var compItem = new OrderItem(
            targetItem.Id,
            targetItem.OrderId,
            targetItem.ProductId,
            targetItem.ProductNameSnapshot,
            targetItem.Quantity,
            targetItem.UnitPrice,
            targetItem.TaxRate,
            targetItem.SkuSnapshot,
            discountAmount: targetItem.DiscountAmount,
            modifiers: targetItem.Modifiers,
            status: OrderItemState.Complimentary,
            kitchenState: targetItem.KitchenState,
            portionReservationStatus: targetItem.PortionReservationStatus,
            netAmount: 0m,
            taxAmount: 0m,
            grossAmount: 0m,
            notes: targetItem.Notes,
            rowVersion: targetItem.RowVersion,
            createdAt: targetItem.CreatedAt,
            updatedAt: now);

        var updatedItems = new List<OrderItem>(order.Items);
        updatedItems[itemIndex] = compItem;

        var historyReason = string.IsNullOrWhiteSpace(command.Notes)
            ? $"Complimentary:{command.ReasonCode}"
            : $"Complimentary:{command.ReasonCode} - {command.Notes}";

        var historyEntry = new OrderStatusHistoryEntry(
            Guid.NewGuid(),
            order.Id,
            order.Status,
            order.Status,
            historyReason,
            command.ActorId,
            now);

        var updatedHistory = order.History.Append(historyEntry).ToList();

        var updatedOrder = new Order(
            order.Id,
            order.Source,
            order.OrderNumber,
            updatedItems,
            order.TableId,
            order.CustomerId,
            order.SourceReferenceId,
            order.SourceExternalId,
            order.Notes,
            order.Status,
            order.ConfirmationStatus,
            order.CurrencyCode,
            order.SubmittedAt,
            order.AcceptedAt,
            order.ClosedAt,
            order.CancelledAt,
            updatedHistory,
            order.RowVersion,
            order.CreatedAt,
            now);

        var newVersion = await _orderRepository.SaveAsync(updatedOrder, command.ExpectedRowVersion, cancellationToken).ConfigureAwait(false);

        await AppendAuditAsync(
            "Order.ItemComplimentary",
            order.Id,
            command.ActorId,
            command.ReasonCode,
            command.CorrelationId,
            beforeState: new { ItemId = targetItem.Id, Status = targetItem.Status.ToString(), Total = targetItem.GrossAmount },
            afterState: new { ItemId = compItem.Id, Status = compItem.Status.ToString(), Total = compItem.GrossAmount },
            cancellationToken).ConfigureAwait(false);

        return new ItemExceptionResult(
            order.Id,
            targetItem.Id,
            OrderItemState.Complimentary,
            newVersion,
            updatedOrder.Subtotal,
            updatedOrder.TaxTotal,
            updatedOrder.Total,
            now);
    }

    private async Task AppendAuditAsync(
        string eventName,
        Guid orderId,
        Guid actorId,
        string reason,
        string correlationId,
        object beforeState,
        object afterState,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO audit.audit_events (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json,
                metadata_json, occurred_at
            ) VALUES (
                @id, @event_name, @aggregate_type, @aggregate_id, @actor_id, @actor_type,
                @reason, @correlation_id, @causation_id, @before_state_json, @after_state_json,
                @metadata_json, @occurred_at
            );
            """;
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("event_name", eventName);
        cmd.Parameters.AddWithValue("aggregate_type", "Order");
        cmd.Parameters.AddWithValue("aggregate_id", orderId);
        cmd.Parameters.AddWithValue("actor_id", actorId);
        cmd.Parameters.AddWithValue("actor_type", "User");
        cmd.Parameters.AddWithValue("reason", reason);
        cmd.Parameters.AddWithValue("correlation_id", correlationId);
        cmd.Parameters.AddWithValue("causation_id", DBNull.Value);

        var pBefore = cmd.Parameters.AddWithValue("before_state_json", JsonSerializer.Serialize(beforeState));
        pBefore.NpgsqlDbType = NpgsqlDbType.Jsonb;

        var pAfter = cmd.Parameters.AddWithValue("after_state_json", JsonSerializer.Serialize(afterState));
        pAfter.NpgsqlDbType = NpgsqlDbType.Jsonb;

        cmd.Parameters.AddWithValue("metadata_json", DBNull.Value);
        cmd.Parameters.AddWithValue("occurred_at", DateTimeOffset.UtcNow);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
