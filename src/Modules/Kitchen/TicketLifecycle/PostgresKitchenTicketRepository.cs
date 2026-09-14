namespace ALKAROS.Kitchen.TicketLifecycle;

using Npgsql;

public sealed class PostgresKitchenTicketRepository : IKitchenTicketRepository
{
    // Defensive ceiling for a filtered list read: a real filter returns far
    // fewer rows. Hitting this means the filter is too broad, or the relation
    // outgrew its assumption — fail loud, do not load unboundedly.
    private const int MaxUnpagedRows = 5000;

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
        return await LoadTicketGraphAsync(
            connection,
            "t.order_id = @filter",
            orderId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KitchenTicket>> GetActiveByStationAsync(string stationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stationId);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await LoadTicketGraphAsync(
            connection,
            "t.station_id = @filter AND t.status NOT IN ('Ready', 'Cancelled')",
            stationId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(KitchenTicket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await AddAsync(ticket, connection, tx, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(
        KitchenTicket ticket,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
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
            itemCmd.Transaction = transaction;
            itemCmd.CommandText =
                """
                INSERT INTO kitchen.kitchen_ticket_items (
                    id, ticket_id, order_item_id, product_id, product_name_snapshot,
                    quantity, modifiers_summary, notes, status, row_version,
                    created_at, updated_at, ready_at, served_at, cancelled_at, cancellation_reason,
                    is_age_restricted
                ) VALUES (
                    @id, @ticket_id, @order_item_id, @product_id, @product_name_snapshot,
                    @quantity, @modifiers_summary, @notes, @status, @row_version,
                    @created_at, @updated_at, @ready_at, @served_at, @cancelled_at, @cancellation_reason,
                    @is_age_restricted
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
            itemCmd.Parameters.AddWithValue("is_age_restricted", item.IsAgeRestricted);

            await itemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

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
            // The WHERE clause on the ON CONFLICT action below (found by an
            // independent audit, 2026-09-05) is required: SaveAsync
            // re-sends every item on the ticket on every save, not just the
            // one a caller transitioned. Without it, this ON CONFLICT DO
            // UPDATE unconditionally bumped row_version for every untouched
            // item too, even though KitchenOperationsStore promises
            // item-level optimistic concurrency (ExpectedItemRowVersion) —
            // a client holding a correct, unchanged item row_version could
            // still be told its version was stale. Status is the only
            // field TransitionTo ever changes together with every other
            // mutable column, so "status unchanged" reliably means "this
            // item was not touched by this save."
            itemCmd.CommandText =
                """
                INSERT INTO kitchen.kitchen_ticket_items (
                    id, ticket_id, order_item_id, product_id, product_name_snapshot,
                    quantity, modifiers_summary, notes, status, row_version,
                    created_at, updated_at, ready_at, served_at, cancelled_at, cancellation_reason,
                    is_age_restricted
                ) VALUES (
                    @id, @ticket_id, @order_item_id, @product_id, @product_name_snapshot,
                    @quantity, @modifiers_summary, @notes, @status, @row_version,
                    @created_at, @updated_at, @ready_at, @served_at, @cancelled_at, @cancellation_reason,
                    @is_age_restricted
                )
                ON CONFLICT (id) DO UPDATE SET
                    status = EXCLUDED.status,
                    row_version = kitchen.kitchen_ticket_items.row_version + 1,
                    updated_at = EXCLUDED.updated_at,
                    ready_at = EXCLUDED.ready_at,
                    served_at = EXCLUDED.served_at,
                    cancelled_at = EXCLUDED.cancelled_at,
                    cancellation_reason = EXCLUDED.cancellation_reason
                WHERE kitchen.kitchen_ticket_items.status IS DISTINCT FROM EXCLUDED.status;
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
            itemCmd.Parameters.AddWithValue("is_age_restricted", item.IsAgeRestricted);

            await itemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        ticket.RowVersion = newRowVersion;
        return newRowVersion;
    }

    public async Task<IReadOnlyList<CompletedTicketTimingRow>> GetCompletedTicketTimingsAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT station_id, created_at, ready_at
            FROM kitchen.kitchen_tickets
            WHERE created_at >= @window_start AND created_at < @window_end AND ready_at IS NOT NULL
            ORDER BY created_at
            LIMIT @max_rows;
            """);
        command.Parameters.AddWithValue("window_start", windowStart);
        command.Parameters.AddWithValue("window_end", windowEnd);
        command.Parameters.AddWithValue("max_rows", MaxUnpagedRows + 1);

        var result = new List<CompletedTicketTimingRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new CompletedTicketTimingRow(
                reader.GetString(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetFieldValue<DateTimeOffset>(2)));
        }

        if (result.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"More than {MaxUnpagedRows} completed tickets in the requested window; " +
                "GetCompletedTicketTimingsAsync must be paginated or the window narrowed.");
        }

        return result;
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
                   created_at, updated_at, ready_at, served_at, cancelled_at, cancellation_reason,
                   is_age_restricted
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
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.GetBoolean(16)));
        }

        return list;
    }

    private static async Task<IReadOnlyList<KitchenTicket>> LoadTicketGraphAsync(
        NpgsqlConnection connection,
        string predicate,
        object filter,
        CancellationToken cancellationToken)
    {
        var allowedPredicates = new HashSet<string>(StringComparer.Ordinal)
        {
            "t.order_id = @filter",
            "t.station_id = @filter AND t.status NOT IN ('Ready', 'Cancelled')",
        };
        if (!allowedPredicates.Contains(predicate))
            throw new ArgumentException("Unsupported kitchen ticket query predicate.", nameof(predicate));

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT t.id, t.order_id, t.ticket_number, t.station_id, t.status, t.row_version,
                   t.created_at, t.updated_at, t.accepted_at, t.ready_at, t.cancelled_at, t.cancellation_reason,
                   i.id, i.ticket_id, i.order_item_id, i.product_id, i.product_name_snapshot,
                   i.quantity, i.modifiers_summary, i.notes, i.status, i.row_version,
                   i.created_at, i.updated_at, i.ready_at, i.served_at, i.cancelled_at, i.cancellation_reason,
                   i.is_age_restricted
            FROM kitchen.kitchen_tickets AS t
            LEFT JOIN kitchen.kitchen_ticket_items AS i ON i.ticket_id = t.id
            WHERE {predicate}
            ORDER BY t.created_at, i.created_at
            LIMIT {MaxUnpagedRows + 1};
            """;
        command.Parameters.AddWithValue("filter", filter);

        var ticketItemRows = 0;
        var rows = new Dictionary<Guid, TicketGraphRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ticketItemRows++;
            var ticketId = reader.GetGuid(0);
            if (!rows.TryGetValue(ticketId, out var row))
            {
                row = new TicketGraphRow(
                    ticketId,
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    Enum.Parse<KitchenTicketState>(reader.GetString(4)),
                    reader.GetInt64(5),
                    reader.GetFieldValue<DateTimeOffset>(6),
                    reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                    reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                    reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
                    reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11));
                rows.Add(ticketId, row);
            }

            if (!reader.IsDBNull(12))
            {
                row.Items.Add(new KitchenTicketItem(
                    reader.GetGuid(12),
                    reader.GetGuid(13),
                    reader.GetGuid(14),
                    reader.GetGuid(15),
                    reader.GetString(16),
                    reader.GetDecimal(17),
                    reader.IsDBNull(18) ? null : reader.GetString(18),
                    reader.IsDBNull(19) ? null : reader.GetString(19),
                    Enum.Parse<KitchenTicketItemState>(reader.GetString(20)),
                    reader.GetInt64(21),
                    reader.GetFieldValue<DateTimeOffset>(22),
                    reader.IsDBNull(23) ? null : reader.GetFieldValue<DateTimeOffset>(23),
                    reader.IsDBNull(24) ? null : reader.GetFieldValue<DateTimeOffset>(24),
                    reader.IsDBNull(25) ? null : reader.GetFieldValue<DateTimeOffset>(25),
                    reader.IsDBNull(26) ? null : reader.GetFieldValue<DateTimeOffset>(26),
                    reader.IsDBNull(27) ? null : reader.GetString(27),
                    reader.GetBoolean(28)));
            }
        }

        if (ticketItemRows > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"Kitchen ticket query '{predicate}' returned more than {MaxUnpagedRows} ticket-item rows; narrow the filter or paginate.");

        return rows.Values.Select(row => new KitchenTicket(
            row.Id,
            row.OrderId,
            row.TicketNumber,
            row.StationId,
            row.Items,
            row.Status,
            row.RowVersion,
            row.CreatedAt,
            row.UpdatedAt,
            row.AcceptedAt,
            row.ReadyAt,
            row.CancelledAt,
            row.CancellationReason)).ToArray();
    }

    private sealed record TicketGraphRow(
        Guid Id,
        Guid OrderId,
        string TicketNumber,
        string StationId,
        KitchenTicketState Status,
        long RowVersion,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        DateTimeOffset? AcceptedAt,
        DateTimeOffset? ReadyAt,
        DateTimeOffset? CancelledAt,
        string? CancellationReason)
    {
        public List<KitchenTicketItem> Items { get; } = [];
    }
}
