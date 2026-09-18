using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.DualScreen;

public sealed partial class DualScreenStore
{
    public Task<StartOrderResponse> StartOrderAsync(Guid terminalId, Guid servingUserId, CancellationToken cancellationToken)
        => StartOrderAsync(terminalId, servingUserId, new StartOrderRequest(), cancellationToken);

    public async Task<StartOrderResponse> StartOrderAsync(
        Guid terminalId,
        Guid servingUserId,
        StartOrderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TableId is null && request.ExpectedTableRowVersion is not null)
            throw new ArgumentException("A table row version requires a table id.", nameof(request));
        if (request.ExpectedTableRowVersion is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Expected table row version must be positive.");

        await EnsureTerminalAsync(terminalId, cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        Guid? activeOrderId;
        await using (var lockTerminal = CreateCommand(connection, transaction,
            "SELECT active_order_id FROM customer_display.terminals WHERE terminal_id = @terminal_id FOR UPDATE;"))
        {
            lockTerminal.Parameters.AddWithValue("terminal_id", terminalId);
            var result = await lockTerminal.ExecuteScalarAsync(cancellationToken);
            activeOrderId = result is null or DBNull ? null : (Guid)result;
        }

        if (activeOrderId is { } existingId)
        {
            await using var statusCommand = CreateCommand(connection, transaction,
                "SELECT status FROM orders.orders WHERE order_id = @order_id;");
            statusCommand.Parameters.AddWithValue("order_id", existingId);
            var status = (string?)await statusCommand.ExecuteScalarAsync(cancellationToken);
            if (status is not ("Submitted" or "Completed" or "Cancelled" or "Rejected"))
                throw new DualScreenConflictException("Terminal already has an active order.");
        }

        if (request.TableId is { } tableId)
        {
            string tableStatus;
            bool tableActive;
            Guid? tableOrderId;
            Guid? tableBillId;
            await using (var lockTable = CreateCommand(connection, transaction,
                """
                SELECT active, current_status, current_order_id, current_bill_id
                FROM table_mgmt.tables
                WHERE table_id = @table_id
                FOR UPDATE;
                """))
            {
                lockTable.Parameters.AddWithValue("table_id", tableId);
                await using var reader = await lockTable.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new DualScreenNotFoundException("Table was not found.");
                tableActive = reader.GetBoolean(0);
                tableStatus = reader.GetString(1);
                tableOrderId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
                tableBillId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
            }

            // ExpectedTableRowVersion is accepted for API compatibility but is no
            // longer a gate (V1-RMD-090). The row is held FOR UPDATE and the
            // decision to seat is made from the fresh state below: an inactive
            // table, an open bill, an existing non-editable order, an order active
            // on another terminal, or a non-Available status each reject with a
            // specific error, and the final bind UPDATE re-checks
            // (current_status = 'Available' AND current_order_id IS NULL AND
            // current_bill_id IS NULL) atomically. A stale version on an otherwise
            // seatable table no longer forces a spurious "refresh".
            if (!tableActive)
                throw new DualScreenConflictException("Inactive tables cannot receive an order.");
            if (tableBillId is not null)
                throw new DualScreenConflictException("Table already has an active bill.");

            if (tableOrderId is { } existingTableOrderId)
            {
                string existingStatus;
                string existingOrderNumber;
                long existingRevision;
                Guid? existingOrderTableId;
                await using (var lockOrder = CreateCommand(connection, transaction,
                    """
                    SELECT status, order_number, row_version, table_id
                    FROM orders.orders
                    WHERE order_id = @order_id
                    FOR UPDATE;
                    """))
                {
                    lockOrder.Parameters.AddWithValue("order_id", existingTableOrderId);
                    await using var reader = await lockOrder.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken))
                        throw new DualScreenConflictException("Table order pointer is stale.");
                    existingStatus = reader.GetString(0);
                    existingOrderNumber = reader.GetString(1);
                    existingRevision = reader.GetInt64(2);
                    existingOrderTableId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
                }

                if (existingOrderTableId != tableId || existingStatus != "Draft")
                    throw new DualScreenConflictException("Table already has a non-editable order.");

                await using (var activeTerminal = CreateCommand(connection, transaction,
                    """
                    SELECT EXISTS(
                        SELECT 1
                        FROM customer_display.terminals
                        WHERE active_order_id = @order_id AND terminal_id <> @terminal_id);
                    """))
                {
                    activeTerminal.Parameters.AddWithValue("order_id", existingTableOrderId);
                    activeTerminal.Parameters.AddWithValue("terminal_id", terminalId);
                    if ((bool)(await activeTerminal.ExecuteScalarAsync(cancellationToken) ?? false))
                        throw new DualScreenConflictException("Table order is active on another terminal.");
                }

                await using (var bindExisting = CreateCommand(connection, transaction,
                    """
                    UPDATE customer_display.terminals
                    SET active_order_id = @order_id, row_version = row_version + 1, updated_at = now()
                    WHERE terminal_id = @terminal_id;
                    """))
                {
                    bindExisting.Parameters.AddWithValue("order_id", existingTableOrderId);
                    bindExisting.Parameters.AddWithValue("terminal_id", terminalId);
                    await bindExisting.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return new StartOrderResponse(existingTableOrderId, existingOrderNumber, existingRevision);
            }

            if (tableStatus != "Available")
                throw new DualScreenConflictException("Only an available table can receive a new order.");
        }

        var orderId = Guid.NewGuid();
        var orderNumber = $"POS-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{orderId:N}"[..32].ToUpperInvariant();
        // V1-RMD-120: was a raw INSERT hand-duplicating the Order aggregate's
        // own defaults (Draft/NotRequired/TRY/zeroed totals/row_version 1)
        // instead of going through it — found by an independent audit
        // (2026-09-07, boundary wave) alongside AddItemAsync/MutateExistingItemAsync
        // below: this channel's order-write path never touched Order/OrderItem
        // at all, so a future domain rule change (tax, rounding, state guards)
        // would silently apply to the Waiter/table-draft channel
        // (OrderManagementStore) but not this one. The connection/transaction
        // overload keeps the insert inside this method's existing lock/commit.
        // V1-CUI-011: found by that task's own real-browser E2E suite - every
        // walk-in Cashier sale created here (Order.ServingUserId left null)
        // failed with a 500 at submit time, since
        // OrderSubmissionStockDispatcher.DispatchAsync requires it ("Every
        // Cashier/Waiter order is created with one; a null here means an
        // order was constructed outside that path"). The table-draft channel
        // (OrderManagementStore, the vanilla Cashier client) already
        // threaded the authenticated actor through correctly - this
        // endpoint's own `new Order(...)` call had simply never been
        // updated to do the same.
        var order = new Order(orderId, OrderSource.Cashier, orderNumber, Array.Empty<OrderItem>(), tableId: request.TableId, servingUserId: servingUserId);
        await _orderRepository.AddAsync(order, connection, transaction, cancellationToken);

        if (request.TableId is { } createdTableId)
        {
            await using var bindTable = CreateCommand(connection, transaction,
                """
                UPDATE table_mgmt.tables
                SET current_order_id = @order_id,
                    current_status = 'Occupied',
                    row_version = row_version + 1
                WHERE table_id = @table_id
                  AND current_status = 'Available'
                  AND current_order_id IS NULL
                  AND current_bill_id IS NULL;
                """);
            bindTable.Parameters.AddWithValue("order_id", orderId);
            bindTable.Parameters.AddWithValue("table_id", createdTableId);
            if (await bindTable.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new DualScreenConflictException("Table changed before the order could be bound.");
        }

        await using (var bindTerminal = CreateCommand(connection, transaction,
            """
            UPDATE customer_display.terminals
            SET active_order_id = @order_id, row_version = row_version + 1, updated_at = now()
            WHERE terminal_id = @terminal_id;
            """))
        {
            bindTerminal.Parameters.AddWithValue("order_id", orderId);
            bindTerminal.Parameters.AddWithValue("terminal_id", terminalId);
            await bindTerminal.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new StartOrderResponse(orderId, orderNumber, 1);
    }

    public async Task<OrderMutationResult> AddItemAsync(
        Guid terminalId,
        Guid orderId,
        AddOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0 || request.Quantity > 999)
            throw new ArgumentOutOfRangeException(nameof(request), request.Quantity, "Quantity must be between 0 and 999.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockDraftOrderAsync(connection, transaction, terminalId, orderId, request.ExpectedRevision, cancellationToken);

        ProductRow product;
        await using (var productCommand = CreateCommand(connection, transaction,
            """
            SELECT p.sku, p.name, p.current_price, COALESCE(t.vat_rate, 0)
            FROM catalog.products p
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            WHERE p.product_id = @product_id AND p.active AND p.is_available AND p.current_price IS NOT NULL;
            """))
        {
            productCommand.Parameters.AddWithValue("product_id", request.ProductId);
            await using var reader = await productCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new DualScreenNotFoundException("Product was not found or has no active price.");
            product = new ProductRow(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3));
        }

        // V1-RMD-120: was a raw SELECT ... FOR UPDATE on the single
        // order_items row plus a hand-computed net/tax/gross and either an
        // UPDATE or INSERT — a second, independent implementation of
        // Order.AddItem/OrderItem's own amount computation. The row-level
        // FOR UPDATE was redundant once LockDraftOrderAsync above already
        // serializes every mutation of this order (a concurrent racer blocks
        // on that lock, not this one) — the two concurrency tests in
        // DualScreenStoreTests cover exactly this.
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new DualScreenNotFoundException("Active order was not found for this terminal.");
        var existing = order.Items.FirstOrDefault(i => i.ProductId == request.ProductId && i.Status == OrderItemState.Draft);

        Order updated;
        if (existing is not null)
        {
            var quantity = existing.Quantity + request.Quantity;
            if (quantity > 999)
                throw new ArgumentOutOfRangeException(nameof(request), quantity, "Cumulative quantity must not exceed 999.");
            updated = order.ChangeItemQuantity(existing.Id, quantity);
        }
        else
        {
            var newItem = new OrderItem(
                Guid.NewGuid(), orderId, request.ProductId, product.Name, request.Quantity, product.UnitPrice, product.TaxRate,
                skuSnapshot: product.Sku);
            updated = order.AddItem(newItem);
        }

        var revision = await _orderRepository.SaveAsync(updated, order.RowVersion, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OrderMutationResult(orderId, revision);
    }

    public Task<OrderMutationResult> ChangeItemQuantityAsync(
        Guid terminalId,
        Guid orderId,
        Guid itemId,
        ChangeOrderItemQuantityRequest request,
        CancellationToken cancellationToken)
        => MutateExistingItemAsync(terminalId, orderId, itemId, request.ExpectedRevision, request.Quantity, false, cancellationToken);

    public Task<OrderMutationResult> RemoveItemAsync(
        Guid terminalId,
        Guid orderId,
        Guid itemId,
        long expectedRevision,
        CancellationToken cancellationToken)
        => MutateExistingItemAsync(terminalId, orderId, itemId, expectedRevision, 0, true, cancellationToken);

}
