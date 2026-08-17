namespace ALKAROS.Kitchen.TicketLifecycle;

using Npgsql;

public sealed class PostgresKitchenTicketRepository : IKitchenTicketRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresKitchenTicketRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<KitchenTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var ticketCmd = connection.CreateCommand();
        ticketCmd.CommandText =
            """
            SELECT id, order_id, ticket_number, station_id, status, row_version,
                   created_at, updated_at, accepted_at, ready_at, cancelled_at, cancellation_reason
            FROM kitchen.kitchen_tickets
            WHERE id = @id;
            """;
        ticketCmd.Parameters.AddWithValue("id", id);

        KitchenTicketState status;
        Guid orderId;
        string ticketNumber, stationId;
        long rowVersion;
        DateTimeOffset createdAt;
        DateTimeOffset? updatedAt, acceptedAt, readyAt, cancelledAt;
        string? cancellationReason;

        await using (var reader = await ticketCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            orderId = reader.GetGuid(1);
            ticketNumber = reader.GetString(2);
            stationId = reader.GetString(3);
            status = Enum.Parse<KitchenTicketState>(reader.GetString(4));
            rowVersion = reader.GetInt64(5);
            createdAt = reader.GetFieldValue<DateTimeOffset>(6);
            updatedAt = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7);
            acceptedAt = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8);
            readyAt = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9);
            cancelledAt = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10);
            cancellationReason = reader.IsDBNull(11) ? null : reader.GetString(11);
        }

        var items = await LoadItemsAsync(connection, id, cancellationToken).ConfigureAwait(false);

        return new KitchenTicket(
            id,
            orderId,
            ticketNumber,
            stationId,
            items,
            status,
            rowVersion,
            createdAt,
            updatedAt,
            acceptedAt,
            readyAt,
            cancelledAt,
            cancellationReason);
    }

    public async Task<IReadOnlyList<KitchenTicket>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var ticketIds = new List<Guid>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id FROM kitchen.kitchen_tickets WHERE order_id = @order_id ORDER BY created_at;";
            cmd.Parameters.AddWithValue("order_id", orderId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ticketIds.Add(reader.GetGuid(0));
            }
        }

        var results = new List<KitchenTicket>();
        foreach (var ticketId in ticketIds)
        {
            var ticket = await GetByIdAsync(ticketId, cancellationToken).ConfigureAwait(false);
            if (ticket != null)
                results.Add(ticket);
        }

        return results;
    }

    public async Task<IReadOnlyList<KitchenTicket>> GetActiveByStationAsync(string stationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var ticketIds = new List<Guid>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                """
                SELECT id
                FROM kitchen.kitchen_tickets
                WHERE station_id = @station_id AND status NOT IN ('Ready', 'Cancelled')
                ORDER BY created_at;
                """;
            cmd.Parameters.AddWithValue("station_id", stationId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ticketIds.Add(reader.GetGuid(0));
            }
        }

        var results = new List<KitchenTicket>();
        foreach (var ticketId in ticketIds)
        {
            var ticket = await GetByIdAsync(ticketId, cancellationToken).ConfigureAwait(false);
            if (ticket != null)
                results.Add(ticket);
        }

        return results;
    }

    public async Task AddAsync(KitchenTicket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                INSERT INTO kitchen.kitchen_tickets (
                    id, order_id, ticket_number, station_id, status, row_version,
                    created_at, updated_at, accepted_at, ready_at, cancelled_at, cancellation_reason
                ) VALUES (
                    @id, @order_id, @ticket_number, @station_id, @status, @row_version,
                    @created_at, @updated_at, @accepted_at, @ready_at, @cancelled_at, @cancellation_reason
                );
                """;
            cmd.Parameters.AddWithValue("id", ticket.Id);
            cmd.Parameters.AddWithValue("order_id", ticket.OrderId);
            cmd.Parameters.AddWithValue("ticket_number", ticket.TicketNumber);
            cmd.Parameters.AddWithValue("station_id", ticket.StationId);
            cmd.Parameters.AddWithValue("status", ticket.Status.ToString());
            cmd.Parameters.AddWithValue("row_version", ticket.RowVersion);
            cmd.Parameters.AddWithValue("created_at", ticket.CreatedAt);
            cmd.Parameters.AddWithValue("updated_at", (object?)ticket.UpdatedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("accepted_at", (object?)ticket.AcceptedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ready_at", (object?)ticket.ReadyAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cancelled_at", (object?)ticket.CancelledAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cancellation_reason", (object?)ticket.CancellationReason ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var item in ticket.Items)
        {
            await using var itemCmd = connection.CreateCommand();
            itemCmd.Transaction = tx;
            itemCmd.CommandText =
                """
                INSERT INTO kitchen.kitchen_ticket_items (
                    id, ticket_id, order_item_id, product_id, product_name_snapshot,
                    quantity, modifiers_summary, notes, status, row_version,
                    created_at, updated_at, ready_at, served_at, cancelled_at, cancellation_reason
                ) VALUES (
                    @id, @ticket_id, @order_item_id, @product_id, @product_name_snapshot,
                    @quantity, @modifiers_summary, @notes, @status, @row_version,
                    @created_at, @updated_at, @ready_at, @served_at, @cancelled_at, @cancellation_reason
                );
                """;
            itemCmd.Parameters.AddWithValue("id", item.Id);
            itemCmd.Parameters.AddWithValue("ticket_id", ticket.Id);
            itemCmd.Parameters.AddWithValue("order_item_id", item.OrderItemId);
            itemCmd.Parameters.AddWithValue("product_id", item.ProductId);
            itemCmd.Parameters.AddWithValue("product_name_snapshot", item.ProductNameSnapshot);
            itemCmd.Parameters.AddWithValue("quantity", item.Quantity);
            itemCmd.Parameters.AddWithValue("modifiers_summary", (object?)item.ModifiersSummary ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("notes", (object?)item.Notes ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("status", item.Status.ToString());
            itemCmd.Parameters.AddWithValue("row_version", item.RowVersion);
            itemCmd.Parameters.AddWithValue("created_at", item.CreatedAt);
            itemCmd.Parameters.AddWithValue("updated_at", (object?)item.UpdatedAt ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("ready_at", (object?)item.ReadyAt ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("served_at", (object?)item.ServedAt ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("cancelled_at", (object?)item.CancelledAt ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("cancellation_reason", (object?)item.CancellationReason ?? DBNull.Value);

            await itemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> SaveAsync(KitchenTicket ticket, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var newRowVersion = expectedRowVersion + 1;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                UPDATE kitchen.kitchen_tickets
                SET status = @status,
                    row_version = @new_row_version,
                    updated_at = @updated_at,
                    accepted_at = @accepted_at,
                    ready_at = @ready_at,
                    cancelled_at = @cancelled_at,
                    cancellation_reason = @cancellation_reason
                WHERE id = @id AND row_version = @expected_row_version;
                """;
            cmd.Parameters.AddWithValue("id", ticket.Id);
            cmd.Parameters.AddWithValue("status", ticket.Status.ToString());
            cmd.Parameters.AddWithValue("new_row_version", newRowVersion);
            cmd.Parameters.AddWithValue("updated_at", (object?)ticket.UpdatedAt ?? DateTimeOffset.UtcNow);
            cmd.Parameters.AddWithValue("accepted_at", (object?)ticket.AcceptedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ready_at", (object?)ticket.ReadyAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cancelled_at", (object?)ticket.CancelledAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cancellation_reason", (object?)ticket.CancellationReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("expected_row_version", expectedRowVersion);

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affected == 0)
            {
                throw new InvalidOperationException(
                    $"Kitchen ticket '{ticket.Id}' not found or concurrent modification (expected row version {expectedRowVersion}).");
            }
        }

        foreach (var item in ticket.Items)
        {
            await using var itemCmd = connection.CreateCommand();
            itemCmd.Transaction = tx;
            itemCmd.CommandText =
                """
                INSERT INTO kitchen.kitchen_ticket_items (
                    id, ticket_id, order_item_id, product_id, product_name_snapshot,
                    quantity, modifiers_summary, notes, status, row_version,
                    created_at, updated_at, ready_at, served_at, cancelled_at, cancellation_reason
                ) VALUES (
                    @id, @ticket_id, @order_item_id, @product_id, @product_name_snapshot,
                    @quantity, @modifiers_summary, @notes, @status, @row_version,
                    @created_at, @updated_at, @ready_at, @served_at, @cancelled_at, @cancellation_reason
                )
                ON CONFLICT (id) DO UPDATE SET
                    status = EXCLUDED.status,
                    row_version = kitchen.kitchen_ticket_items.row_version + 1,
                    updated_at = EXCLUDED.updated_at,
                    ready_at = EXCLUDED.ready_at,
                    served_at = EXCLUDED.served_at,
                    cancelled_at = EXCLUDED.cancelled_at,
                    cancellation_reason = EXCLUDED.cancellation_reason;
                """;
            itemCmd.Parameters.AddWithValue("id", item.Id);
            itemCmd.Parameters.AddWithValue("ticket_id", ticket.Id);
            itemCmd.Parameters.AddWithValue("order_item_id", item.OrderItemId);
            itemCmd.Parameters.AddWithValue("product_id", item.ProductId);
            itemCmd.Parameters.AddWithValue("product_name_snapshot", item.ProductNameSnapshot);
            itemCmd.Parameters.AddWithValue("quantity", item.Quantity);
            itemCmd.Parameters.AddWithValue("modifiers_summary", (object?)item.ModifiersSummary ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("notes", (object?)item.Notes ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("status", item.Status.ToString());
            itemCmd.Parameters.AddWithValue("row_version", item.RowVersion);
            itemCmd.Parameters.AddWithValue("created_at", item.CreatedAt);
            itemCmd.Parameters.AddWithValue("updated_at", (object?)item.UpdatedAt ?? DateTimeOffset.UtcNow);
            itemCmd.Parameters.AddWithValue("ready_at", (object?)item.ReadyAt ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("served_at", (object?)item.ServedAt ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("cancelled_at", (object?)item.CancelledAt ?? DBNull.Value);
            itemCmd.Parameters.AddWithValue("cancellation_reason", (object?)item.CancellationReason ?? DBNull.Value);

            await itemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        ticket.RowVersion = newRowVersion;
        return newRowVersion;
    }

    private static async Task<IReadOnlyList<KitchenTicketItem>> LoadItemsAsync(
        NpgsqlConnection connection,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, ticket_id, order_item_id, product_id, product_name_snapshot,
                   quantity, modifiers_summary, notes, status, row_version,
                   created_at, updated_at, ready_at, served_at, cancelled_at, cancellation_reason
            FROM kitchen.kitchen_ticket_items
            WHERE ticket_id = @ticket_id
            ORDER BY created_at;
            """;
        cmd.Parameters.AddWithValue("ticket_id", ticketId);

        var list = new List<KitchenTicketItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new KitchenTicketItem(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                Enum.Parse<KitchenTicketItemState>(reader.GetString(8)),
                reader.GetInt64(9),
                reader.GetFieldValue<DateTimeOffset>(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
                reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
                reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
                reader.IsDBNull(15) ? null : reader.GetString(15)));
        }

        return list;
    }
}
