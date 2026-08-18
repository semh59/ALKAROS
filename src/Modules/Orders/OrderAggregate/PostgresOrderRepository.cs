using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Orders.OrderAggregate;

public sealed class PostgresOrderRepository : IOrderRepository
{
    private const string Orders = "orders.orders";
    private const string Items = "orders.order_items";
    private const string Modifiers = "orders.order_item_modifiers";
    private const string History = "orders.order_status_history";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresOrderRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Order id cannot be empty.", nameof(id));

        var order = await ReadOrderAsync(id, cancellationToken);
        if (order is null)
            return null;

        var items = await ReadItemsAsync(id, cancellationToken);

        var modifierRows = await ReadModifierRowsAsync(items.Select(i => i.Id).ToArray(), cancellationToken);
        foreach (var item in items)
            item.AttachModifiers(modifierRows.Where(m => m.ItemId == item.Id));

        var history = await ReadHistoryAsync(id, cancellationToken);

        return new Order(
            order.Id,
            order.Source,
            order.OrderNumber,
            items.Select(i => i.ToDomain()).ToList(),
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
            history.Select(h => h.ToDomain()).ToList(),
            order.RowVersion,
            order.CreatedAt,
            order.UpdatedAt);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await InsertOrderAsync(connection, transaction, order, cancellationToken);

        foreach (var item in order.Items)
            await InsertItemAsync(connection, transaction, item, cancellationToken);

        foreach (var row in order.History)
            await InsertHistoryAsync(connection, transaction, row, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<long> SaveAsync(Order order, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var newRowVersion = await SaveAsync(order, expectedRowVersion, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return newRowVersion;
    }

    public async Task<long> SaveAsync(
        Order order,
        long expectedRowVersion,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var newRowVersion = await UpdateOrderAsync(connection, transaction, order, expectedRowVersion, cancellationToken);

        var knownItemIds = (await ReadItemIdsAsync(connection, transaction, order.Id, cancellationToken)).ToHashSet();
        foreach (var item in order.Items)
        {
            if (knownItemIds.Contains(item.Id))
                await UpdateItemAsync(connection, transaction, item, cancellationToken);
            else
                await InsertItemAsync(connection, transaction, item, cancellationToken);
        }

        var knownHistoryIds = (await ReadHistoryIdsAsync(connection, transaction, order.Id, cancellationToken)).ToHashSet();
        foreach (var row in order.History)
        {
            if (!knownHistoryIds.Contains(row.Id))
                await InsertHistoryAsync(connection, transaction, row, cancellationToken);
        }

        return newRowVersion;
    }

    private static async Task InsertOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Order order,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {Orders} (
                order_id, source, source_reference_id, source_external_id, table_id, customer_id,
                status, confirmation_status, order_number, notes,
                subtotal, discount_total, tax_total, total, currency_code,
                submitted_at, accepted_at, closed_at, cancelled_at,
                created_at, updated_at, row_version)
            VALUES (@order_id, @source, @source_reference_id, @source_external_id, @table_id, @customer_id,
                    @status, @confirmation_status, @order_number, @notes,
                    @subtotal, @discount_total, @tax_total, @total, @currency_code,
                    @submitted_at, @accepted_at, @closed_at, @cancelled_at,
                    @created_at, @updated_at, @row_version);
            """);
        BindOrder(command, order);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> UpdateOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Order order,
        long expectedRowVersion,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            UPDATE {Orders}
            SET status = @status,
                confirmation_status = @confirmation_status,
                order_number = @order_number,
                notes = @notes,
                subtotal = @subtotal,
                discount_total = @discount_total,
                tax_total = @tax_total,
                total = @total,
                currency_code = @currency_code,
                submitted_at = @submitted_at,
                accepted_at = @accepted_at,
                closed_at = @closed_at,
                cancelled_at = @cancelled_at,
                updated_at = @updated_at,
                row_version = row_version + 1
            WHERE order_id = @order_id AND row_version = @expected_row_version
            RETURNING row_version;
            """);
        BindOrder(command, order);
        command.Parameters.AddWithValue("expected_row_version", expectedRowVersion);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new InvalidOperationException(
                $"Order {order.Id} not found or concurrent modification " +
                $"(expected row version {expectedRowVersion}).");

        return (long)result;
    }

    private static void BindOrder(NpgsqlCommand command, Order order)
    {
        command.Parameters.AddWithValue("order_id", order.Id);
        command.Parameters.AddWithValue("source", order.Source.ToString());
        command.Parameters.AddWithValue("source_reference_id", order.SourceReferenceId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("source_external_id", order.SourceExternalId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("table_id", order.TableId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("customer_id", order.CustomerId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("status", order.Status.ToString());
        command.Parameters.AddWithValue("confirmation_status", order.ConfirmationStatus.ToString());
        command.Parameters.AddWithValue("order_number", order.OrderNumber);
        command.Parameters.AddWithValue("notes", order.Notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("subtotal", order.Subtotal);
        command.Parameters.AddWithValue("discount_total", order.DiscountTotal);
        command.Parameters.AddWithValue("tax_total", order.TaxTotal);
        command.Parameters.AddWithValue("total", order.Total);
        command.Parameters.AddWithValue("currency_code", order.CurrencyCode);
        command.Parameters.AddWithValue("submitted_at", order.SubmittedAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("accepted_at", order.AcceptedAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("closed_at", order.ClosedAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("cancelled_at", order.CancelledAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("created_at", order.CreatedAt);
        command.Parameters.AddWithValue("updated_at", order.UpdatedAt);
        command.Parameters.AddWithValue("row_version", order.RowVersion);
    }

    private static async Task InsertItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OrderItem item,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {Items} (
                order_item_id, order_id, product_id, product_name_snapshot, sku_snapshot,
                quantity, unit_price, discount_amount, tax_rate, tax_amount, net_amount, gross_amount,
                status, kitchen_state, portion_reservation_status, notes,
                created_at, updated_at, row_version)
            VALUES (@order_item_id, @order_id, @product_id, @product_name_snapshot, @sku_snapshot,
                    @quantity, @unit_price, @discount_amount, @tax_rate, @tax_amount, @net_amount, @gross_amount,
                    @status, @kitchen_state, @portion_reservation_status, @notes,
                    @created_at, @updated_at, @row_version);
            """);
        BindItem(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);

        foreach (var modifier in item.Modifiers)
            await InsertModifierAsync(connection, transaction, modifier, cancellationToken);
    }

    private static async Task UpdateItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OrderItem item,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            UPDATE {Items}
            SET quantity = @quantity,
                discount_amount = @discount_amount,
                tax_amount = @tax_amount,
                net_amount = @net_amount,
                gross_amount = @gross_amount,
                status = @status,
                kitchen_state = @kitchen_state,
                portion_reservation_status = @portion_reservation_status,
                notes = @notes,
                updated_at = @updated_at,
                row_version = row_version + 1
            WHERE order_item_id = @order_item_id AND row_version = @row_version
            RETURNING order_item_id;
            """);
        BindItem(command, item);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new InvalidOperationException(
                $"Order item {item.Id} not found or concurrent modification " +
                $"(expected row version {item.RowVersion}).");
    }

    private static void BindItem(NpgsqlCommand command, OrderItem item)
    {
        command.Parameters.AddWithValue("order_item_id", item.Id);
        command.Parameters.AddWithValue("order_id", item.OrderId);
        command.Parameters.AddWithValue("product_id", item.ProductId);
        command.Parameters.AddWithValue("product_name_snapshot", item.ProductNameSnapshot);
        command.Parameters.AddWithValue("sku_snapshot", item.SkuSnapshot ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("quantity", item.Quantity);
        command.Parameters.AddWithValue("unit_price", item.UnitPrice);
        command.Parameters.AddWithValue("discount_amount", item.DiscountAmount);
        command.Parameters.AddWithValue("tax_rate", item.TaxRate);
        command.Parameters.AddWithValue("tax_amount", item.TaxAmount);
        command.Parameters.AddWithValue("net_amount", item.NetAmount);
        command.Parameters.AddWithValue("gross_amount", item.GrossAmount);
        command.Parameters.AddWithValue("status", item.Status.ToString());
        command.Parameters.AddWithValue("kitchen_state", item.KitchenState.ToString());
        command.Parameters.AddWithValue("portion_reservation_status", item.PortionReservationStatus.ToString());
        command.Parameters.AddWithValue("notes", item.Notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("created_at", item.CreatedAt);
        command.Parameters.AddWithValue("updated_at", item.UpdatedAt);
        command.Parameters.AddWithValue("row_version", item.RowVersion);
    }

    private static async Task InsertModifierAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OrderItemModifier modifier,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {Modifiers} (
                order_item_modifier_id, order_item_id, modifier_id, modifier_name_snapshot,
                price_delta, quantity)
            VALUES (@order_item_modifier_id, @order_item_id, @modifier_id, @modifier_name_snapshot,
                    @price_delta, @quantity);
            """);
        command.Parameters.AddWithValue("order_item_modifier_id", modifier.Id);
        command.Parameters.AddWithValue("order_item_id", modifier.OrderItemId);
        command.Parameters.AddWithValue("modifier_id", modifier.ModifierId);
        command.Parameters.AddWithValue("modifier_name_snapshot", modifier.ModifierNameSnapshot);
        command.Parameters.AddWithValue("price_delta", modifier.PriceDelta);
        command.Parameters.AddWithValue("quantity", modifier.Quantity);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OrderStatusHistoryEntry row,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {History} (
                order_status_history_id, order_id, old_status, new_status, reason, changed_by, changed_at)
            VALUES (@order_status_history_id, @order_id, @old_status, @new_status, @reason, @changed_by, @changed_at);
            """);
        command.Parameters.AddWithValue("order_status_history_id", row.Id);
        command.Parameters.AddWithValue("order_id", row.OrderId);
        command.Parameters.AddWithValue("old_status", row.OldStatus.ToString());
        command.Parameters.AddWithValue("new_status", row.NewStatus.ToString());
        command.Parameters.AddWithValue("reason", row.Reason ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("changed_by", row.ChangedBy ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("changed_at", row.ChangedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<OrderRow?> ReadOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT order_id, source, source_reference_id, source_external_id, table_id, customer_id,
                   status, confirmation_status, order_number, notes,
                   subtotal, discount_total, tax_total, total, currency_code,
                   submitted_at, accepted_at, closed_at, cancelled_at,
                   created_at, updated_at, row_version
            FROM {Orders}
            WHERE order_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new OrderRow(
            reader.GetGuid(0),
            Enum.Parse<OrderSource>(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            Enum.Parse<OrderState>(reader.GetString(6)),
            Enum.Parse<ConfirmationStatus>(reader.GetString(7)),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetDateTime(15),
            reader.IsDBNull(16) ? null : reader.GetDateTime(16),
            reader.IsDBNull(17) ? null : reader.GetDateTime(17),
            reader.IsDBNull(18) ? null : reader.GetDateTime(18),
            reader.GetDateTime(19),
            reader.GetDateTime(20),
            reader.GetInt64(21));
    }

    private async Task<IReadOnlyList<ItemRow>> ReadItemsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = new List<ItemRow>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT order_item_id, order_id, product_id, product_name_snapshot, sku_snapshot,
                   quantity, unit_price, discount_amount, tax_rate, tax_amount, net_amount, gross_amount,
                   status, kitchen_state, portion_reservation_status, notes,
                   created_at, updated_at, row_version
            FROM {Items}
            WHERE order_id = @order_id
            ORDER BY created_at;
            """);
        command.Parameters.AddWithValue("order_id", orderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ItemRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetDecimal(10),
                reader.GetDecimal(11),
                Enum.Parse<OrderItemState>(reader.GetString(12)),
                Enum.Parse<KitchenState>(reader.GetString(13)),
                Enum.Parse<PortionReservationStatus>(reader.GetString(14)),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.GetDateTime(16),
                reader.GetDateTime(17),
                reader.GetInt64(18)));
        }

        return result;
    }

    private async Task<IReadOnlyList<ModifierRow>> ReadModifierRowsAsync(
        Guid[] itemIds,
        CancellationToken cancellationToken)
    {
        var result = new List<ModifierRow>();
        if (itemIds.Length == 0)
            return result;

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT order_item_modifier_id, order_item_id, modifier_id, modifier_name_snapshot,
                   price_delta, quantity
            FROM {Modifiers}
            WHERE order_item_id = ANY(@item_ids);
            """);
        command.Parameters.AddWithValue("item_ids", itemIds);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ModifierRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5)));
        }

        return result;
    }

    private async Task<IReadOnlyList<HistoryRow>> ReadHistoryAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = new List<HistoryRow>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT order_status_history_id, order_id, old_status, new_status, reason, changed_by, changed_at
            FROM {History}
            WHERE order_id = @order_id
            ORDER BY changed_at;
            """);
        command.Parameters.AddWithValue("order_id", orderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new HistoryRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                Enum.Parse<OrderState>(reader.GetString(2)),
                Enum.Parse<OrderState>(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.GetDateTime(6)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<Guid>> ReadItemIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = new List<Guid>();

        await using var command = CreateCommand(connection, transaction,
            $"SELECT order_item_id FROM {Items} WHERE order_id = @order_id;");
        command.Parameters.AddWithValue("order_id", orderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0));

        return result;
    }

    private static async Task<IReadOnlyList<Guid>> ReadHistoryIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = new List<Guid>();

        await using var command = CreateCommand(connection, transaction,
            $"SELECT order_status_history_id FROM {History} WHERE order_id = @order_id;");
        command.Parameters.AddWithValue("order_id", orderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0));

        return result;
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private sealed record OrderRow(
        Guid Id,
        OrderSource Source,
        Guid? SourceReferenceId,
        string? SourceExternalId,
        Guid? TableId,
        Guid? CustomerId,
        OrderState Status,
        ConfirmationStatus ConfirmationStatus,
        string OrderNumber,
        string? Notes,
        string CurrencyCode,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset? AcceptedAt,
        DateTimeOffset? ClosedAt,
        DateTimeOffset? CancelledAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        long RowVersion);

    private sealed record ItemRow(
        Guid Id,
        Guid OrderId,
        Guid ProductId,
        string ProductNameSnapshot,
        string? SkuSnapshot,
        decimal Quantity,
        decimal UnitPrice,
        decimal DiscountAmount,
        decimal TaxRate,
        decimal TaxAmount,
        decimal NetAmount,
        decimal GrossAmount,
        OrderItemState Status,
        KitchenState KitchenState,
        PortionReservationStatus PortionReservationStatus,
        string? Notes,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        long RowVersion)
    {
        public List<OrderItemModifier> Modifiers { get; } = new();

        public ItemRow AttachModifiers(IEnumerable<ModifierRow> rows)
        {
            foreach (var row in rows)
                Modifiers.Add(row.ToDomain());
            return this;
        }

        public OrderItem ToDomain()
            => new(
                Id,
                OrderId,
                ProductId,
                ProductNameSnapshot,
                Quantity,
                UnitPrice,
                TaxRate,
                SkuSnapshot,
                DiscountAmount,
                Modifiers,
                Status,
                KitchenState,
                PortionReservationStatus,
                netAmount: NetAmount,
                taxAmount: TaxAmount,
                grossAmount: GrossAmount,
                notes: Notes,
                rowVersion: RowVersion,
                createdAt: CreatedAt,
                updatedAt: UpdatedAt);
    }

    private sealed record ModifierRow(
        Guid Id,
        Guid ItemId,
        Guid ModifierId,
        string ModifierNameSnapshot,
        decimal PriceDelta,
        decimal Quantity)
    {
        public OrderItemModifier ToDomain()
            => new(Id, ItemId, ModifierId, ModifierNameSnapshot, PriceDelta, Quantity);
    }

    private sealed record HistoryRow(
        Guid Id,
        Guid OrderId,
        OrderState OldStatus,
        OrderState NewStatus,
        string? Reason,
        Guid? ChangedBy,
        DateTimeOffset ChangedAt)
    {
        public OrderStatusHistoryEntry ToDomain()
            => new(Id, OrderId, OldStatus, NewStatus, Reason, ChangedBy, ChangedAt);
    }
}