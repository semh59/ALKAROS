namespace ALKAROS.Kitchen.PhysicalPrintRecovery;

using Npgsql;

public sealed class PostgresPhysicalPrintRecoveryRepository : IPhysicalPrintRecoveryRepository
{
    // Defensive ceiling for a filtered list read: a real filter returns
    // far fewer rows. Hitting this means the filter is too broad, or the
    // relation outgrew its assumption — fail loud, do not load unboundedly.
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _staleInFlightAfter;

    public PostgresPhysicalPrintRecoveryRepository(
        NpgsqlDataSource dataSource,
        TimeSpan? staleInFlightAfter = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _staleInFlightAfter = staleInFlightAfter ?? TimeSpan.FromMinutes(5);
        if (_staleInFlightAfter <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(staleInFlightAfter), "Stale timeout must be positive.");
    }

    public async Task<PhysicalPrintDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, print_job_id, ticket_id, printer_id, status, attempt_number,
                   is_reprint, operator_id, operator_reason, crash_window_reason,
                   payload_snapshot, reprint_payload, created_at, delivered_at,
                   resolved_at, row_version
            FROM kitchen.physical_print_deliveries
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapDelivery(reader);
    }

    public async Task<IReadOnlyList<PhysicalPrintDelivery>> GetByPrintJobIdAsync(Guid printJobId, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT id, print_job_id, ticket_id, printer_id, status, attempt_number,
                   is_reprint, operator_id, operator_reason, crash_window_reason,
                   payload_snapshot, reprint_payload, created_at, delivered_at,
                   resolved_at, row_version
            FROM kitchen.physical_print_deliveries
            WHERE print_job_id = @print_job_id
            ORDER BY created_at
            LIMIT {MaxUnpagedRows + 1};
            """;
        cmd.Parameters.AddWithValue("print_job_id", printJobId);

        var list = new List<PhysicalPrintDelivery>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapDelivery(reader));
        }

        if (list.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetByPrintJobIdAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return list;
    }

    public async Task<IReadOnlyList<PhysicalPrintDelivery>> GetByTicketIdAsync(Guid ticketId, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT id, print_job_id, ticket_id, printer_id, status, attempt_number,
                   is_reprint, operator_id, operator_reason, crash_window_reason,
                   payload_snapshot, reprint_payload, created_at, delivered_at,
                   resolved_at, row_version
            FROM kitchen.physical_print_deliveries
            WHERE ticket_id = @ticket_id
            ORDER BY created_at
            LIMIT {MaxUnpagedRows + 1};
            """;
        cmd.Parameters.AddWithValue("ticket_id", ticketId);

        var list = new List<PhysicalPrintDelivery>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapDelivery(reader));
        }

        if (list.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetByTicketIdAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return list;
    }

    public async Task<IReadOnlyList<PhysicalPrintDelivery>> GetPendingUnknownDeliveriesAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using (var recoveryCommand = connection.CreateCommand())
        {
            recoveryCommand.Transaction = transaction;
            recoveryCommand.CommandText =
                """
                UPDATE kitchen.physical_print_deliveries
                SET status = 'Unknown',
                    is_reprint = FALSE,
                    crash_window_reason = CASE
                        WHEN status = 'ReprintInFlight' THEN 'Reprint worker lease expired before acknowledgement.'
                        ELSE 'Print worker lease expired before acknowledgement.'
                    END,
                    reprint_payload = NULL,
                    delivered_at = NULL,
                    resolved_at = NULL,
                    state_changed_at = now(),
                    row_version = row_version + 1
                WHERE status IN ('InFlight', 'ReprintInFlight')
                  AND state_changed_at <= now() - @stale_after;
                """;
            recoveryCommand.Parameters.AddWithValue("stale_after", _staleInFlightAfter);
            await recoveryCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            $"""
            SELECT id, print_job_id, ticket_id, printer_id, status, attempt_number,
                   is_reprint, operator_id, operator_reason, crash_window_reason,
                   payload_snapshot, reprint_payload, created_at, delivered_at,
                   resolved_at, row_version
            FROM kitchen.physical_print_deliveries
            WHERE status = 'Unknown'
            ORDER BY created_at
            LIMIT {MaxUnpagedRows + 1};
            """;

        var list = new List<PhysicalPrintDelivery>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapDelivery(reader));
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        if (list.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetPendingUnknownDeliveriesAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return list;
    }

    public async Task AddAsync(PhysicalPrintDelivery delivery, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO kitchen.physical_print_deliveries (
                id, print_job_id, ticket_id, printer_id, status, attempt_number,
                is_reprint, operator_id, operator_reason, crash_window_reason,
                payload_snapshot, reprint_payload, created_at, delivered_at,
                resolved_at, row_version
            ) VALUES (
                @id, @print_job_id, @ticket_id, @printer_id, @status, @attempt_number,
                @is_reprint, @operator_id, @operator_reason, @crash_window_reason,
                @payload_snapshot, @reprint_payload, @created_at, @delivered_at,
                @resolved_at, @row_version
            );
            """;

        cmd.Parameters.AddWithValue("id", delivery.Id);
        cmd.Parameters.AddWithValue("print_job_id", delivery.PrintJobId);
        cmd.Parameters.AddWithValue("ticket_id", delivery.TicketId);
        cmd.Parameters.AddWithValue("printer_id", delivery.PrinterId);
        cmd.Parameters.AddWithValue("status", delivery.Status.ToString());
        cmd.Parameters.AddWithValue("attempt_number", delivery.AttemptNumber);
        cmd.Parameters.AddWithValue("is_reprint", delivery.IsReprint);
        cmd.Parameters.AddWithValue("operator_id", (object?)delivery.OperatorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("operator_reason", (object?)delivery.OperatorReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("crash_window_reason", (object?)delivery.CrashWindowReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("payload_snapshot", delivery.PayloadSnapshot);
        cmd.Parameters.AddWithValue("reprint_payload", (object?)delivery.ReprintPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("created_at", delivery.CreatedAt);
        cmd.Parameters.AddWithValue("delivered_at", (object?)delivery.DeliveredAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("resolved_at", (object?)delivery.ResolvedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("row_version", delivery.RowVersion);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task SaveAsync(PhysicalPrintDelivery delivery, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE kitchen.physical_print_deliveries
            SET status = @status,
                is_reprint = @is_reprint,
                operator_id = @operator_id,
                operator_reason = @operator_reason,
                crash_window_reason = @crash_window_reason,
                reprint_payload = @reprint_payload,
                delivered_at = @delivered_at,
                resolved_at = @resolved_at,
                state_changed_at = now(),
                row_version = row_version + 1
            WHERE id = @id AND row_version = @row_version;
            """;

        cmd.Parameters.AddWithValue("id", delivery.Id);
        cmd.Parameters.AddWithValue("status", delivery.Status.ToString());
        cmd.Parameters.AddWithValue("is_reprint", delivery.IsReprint);
        cmd.Parameters.AddWithValue("operator_id", (object?)delivery.OperatorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("operator_reason", (object?)delivery.OperatorReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("crash_window_reason", (object?)delivery.CrashWindowReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("reprint_payload", (object?)delivery.ReprintPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("delivered_at", (object?)delivery.DeliveredAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("resolved_at", (object?)delivery.ResolvedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("row_version", delivery.RowVersion);

        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new PhysicalPrintDeliveryConcurrencyException(
                $"Optimistic concurrency check failed for PhysicalPrintDelivery '{delivery.Id}'. Expected RowVersion={delivery.RowVersion}.");
        }

        delivery.RowVersion++;
    }

    private static PhysicalPrintDelivery MapDelivery(NpgsqlDataReader reader)
    {
        var status = Enum.Parse<PhysicalPrintDeliveryStatus>(reader.GetString(4));

        return new PhysicalPrintDelivery(
            id: reader.GetGuid(0),
            printJobId: reader.GetGuid(1),
            ticketId: reader.GetGuid(2),
            printerId: reader.GetGuid(3),
            status: status,
            attemptNumber: reader.GetInt32(5),
            isReprint: reader.GetBoolean(6),
            operatorId: reader.IsDBNull(7) ? null : reader.GetString(7),
            operatorReason: reader.IsDBNull(8) ? null : reader.GetString(8),
            crashWindowReason: reader.IsDBNull(9) ? null : reader.GetString(9),
            payloadSnapshot: reader.GetString(10),
            reprintPayload: reader.IsDBNull(11) ? null : reader.GetString(11),
            createdAt: reader.GetFieldValue<DateTimeOffset>(12),
            deliveredAt: reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
            resolvedAt: reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
            rowVersion: reader.GetInt64(15));
    }
}
