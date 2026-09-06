using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using Npgsql;

namespace ALKAROS.Inventory.ReservationBalanceProjection;

public sealed class PostgresReservationBalanceRepository : IReservationBalanceRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresReservationBalanceRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<StockBalance?> GetBalanceAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            WHERE stock_item_id = $1 AND stock_location_id = $2;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<StockBalance>> GetAllBalancesAsync(CancellationToken ct = default)
    {
        string sql = $@"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            ORDER BY stock_item_id, stock_location_id
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockBalance>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetAllBalancesAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task<ApplyReservationResult> ApplyReservationCreatedAtomicAsync(PortionReservation reservation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Try to record applied creation event
            const string insertEventSql = @"
                INSERT INTO inventory.reservation_balance_applied_events (
                    id, reservation_id, event_type, terminal_status, stock_item_id, stock_location_id, quantity, applied_at
                ) VALUES (
                    $1, $2, 'Reserved', NULL, $3, $4, $5, NOW()
                )
                ON CONFLICT (reservation_id, event_type) DO NOTHING
                RETURNING id;";

            await using var eventCmd = new NpgsqlCommand(insertEventSql, conn, tx);
            eventCmd.Parameters.AddWithValue(Guid.NewGuid());
            eventCmd.Parameters.AddWithValue(reservation.Id);
            eventCmd.Parameters.AddWithValue(reservation.StockItemId);
            eventCmd.Parameters.AddWithValue(reservation.StockLocationId);
            eventCmd.Parameters.AddWithValue(reservation.Quantity);

            var eventInserted = await eventCmd.ExecuteScalarAsync(ct);
            if (eventInserted == null)
            {
                // Already applied creation event -> Idempotent replay!
                await tx.RollbackAsync(ct);
                var existingBalance = await GetBalanceAsync(reservation.StockItemId, reservation.StockLocationId, ct);
                return new ApplyReservationResult(existingBalance ?? StockBalance.Create(reservation.StockItemId, reservation.StockLocationId), IsIdempotentReplay: true);
            }

            // 2. Event newly recorded -> Update stock_balances atomically
            const string upsertBalanceSql = @"
                INSERT INTO inventory.stock_balances (
                    stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                    reserved_quantity, available_quantity, updated_at, row_version
                ) VALUES (
                    $1, $2, $3, 0, $4, -$4, NOW(), 1
                )
                ON CONFLICT (stock_item_id, stock_location_id) DO UPDATE
                SET reserved_quantity = inventory.stock_balances.reserved_quantity + EXCLUDED.reserved_quantity,
                    available_quantity = inventory.stock_balances.on_hand_quantity - (inventory.stock_balances.reserved_quantity + EXCLUDED.reserved_quantity),
                    updated_at = NOW(),
                    row_version = inventory.stock_balances.row_version + 1
                RETURNING stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                          reserved_quantity, available_quantity, updated_at, row_version;";

            await using var balanceCmd = new NpgsqlCommand(upsertBalanceSql, conn, tx);
            balanceCmd.Parameters.AddWithValue(Guid.NewGuid());
            balanceCmd.Parameters.AddWithValue(reservation.StockItemId);
            balanceCmd.Parameters.AddWithValue(reservation.StockLocationId);
            balanceCmd.Parameters.AddWithValue(reservation.Quantity);

            await using var reader = await balanceCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new InvalidOperationException("Failed to update stock balance on reservation creation.");
            }
            var updatedBalance = MapRow(reader);
            await reader.CloseAsync();

            await tx.CommitAsync(ct);
            return new ApplyReservationResult(updatedBalance, IsIdempotentReplay: false);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ApplyReservationResult> ApplyReservationTerminalAtomicAsync(
        PortionReservation reservation,
        PortionReservationStatus terminalStatus,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Try to record applied terminal event
            const string insertEventSql = @"
                INSERT INTO inventory.reservation_balance_applied_events (
                    id, reservation_id, event_type, terminal_status, stock_item_id, stock_location_id, quantity, applied_at
                ) VALUES (
                    $1, $2, 'Terminal', $3, $4, $5, $6, NOW()
                )
                ON CONFLICT (reservation_id, event_type) DO NOTHING
                RETURNING id;";

            await using var eventCmd = new NpgsqlCommand(insertEventSql, conn, tx);
            eventCmd.Parameters.AddWithValue(Guid.NewGuid());
            eventCmd.Parameters.AddWithValue(reservation.Id);
            eventCmd.Parameters.AddWithValue(terminalStatus.ToString());
            eventCmd.Parameters.AddWithValue(reservation.StockItemId);
            eventCmd.Parameters.AddWithValue(reservation.StockLocationId);
            eventCmd.Parameters.AddWithValue(reservation.Quantity);

            var eventInserted = await eventCmd.ExecuteScalarAsync(ct);
            if (eventInserted == null)
            {
                // Terminal transition already applied for this reservation -> Idempotent replay!
                await tx.RollbackAsync(ct);
                var existingBalance = await GetBalanceAsync(reservation.StockItemId, reservation.StockLocationId, ct);
                return new ApplyReservationResult(existingBalance ?? StockBalance.Create(reservation.StockItemId, reservation.StockLocationId), IsIdempotentReplay: true);
            }

            // 2. Terminal event newly recorded -> Decrement reserved_quantity (bounded at 0)
            const string updateBalanceSql = @"
                UPDATE inventory.stock_balances
                SET reserved_quantity = GREATEST(0, reserved_quantity - $3),
                    available_quantity = on_hand_quantity - GREATEST(0, reserved_quantity - $3),
                    updated_at = NOW(),
                    row_version = row_version + 1
                WHERE stock_item_id = $1 AND stock_location_id = $2
                RETURNING stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                          reserved_quantity, available_quantity, updated_at, row_version;";

            await using var balanceCmd = new NpgsqlCommand(updateBalanceSql, conn, tx);
            balanceCmd.Parameters.AddWithValue(reservation.StockItemId);
            balanceCmd.Parameters.AddWithValue(reservation.StockLocationId);
            balanceCmd.Parameters.AddWithValue(reservation.Quantity);

            await using var reader = await balanceCmd.ExecuteReaderAsync(ct);
            StockBalance updatedBalance;
            if (await reader.ReadAsync(ct))
            {
                updatedBalance = MapRow(reader);
            }
            else
            {
                // If balance record didn't exist, create baseline
                updatedBalance = StockBalance.Create(reservation.StockItemId, reservation.StockLocationId);
            }
            await reader.CloseAsync();

            await tx.CommitAsync(ct);
            return new ApplyReservationResult(updatedBalance, IsIdempotentReplay: false);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task SetExactReservedBalanceAsync(
        Guid stockItemId,
        Guid stockLocationId,
        decimal reservedQuantity,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO inventory.stock_balances (
                stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                reserved_quantity, available_quantity, updated_at, row_version
            ) VALUES (
                $1, $2, $3, 0, $4, -$4, NOW(), 1
            )
            ON CONFLICT (stock_item_id, stock_location_id) DO UPDATE
            SET reserved_quantity = EXCLUDED.reserved_quantity,
                available_quantity = inventory.stock_balances.on_hand_quantity - EXCLUDED.reserved_quantity,
                updated_at = NOW(),
                row_version = inventory.stock_balances.row_version + 1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);
        cmd.Parameters.AddWithValue(reservedQuantity);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyDictionary<(Guid StockItemId, Guid StockLocationId), decimal>> AggregateActiveReservationsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT stock_item_id, stock_location_id, COALESCE(SUM(quantity), 0)
            FROM inventory.portion_reservations
            WHERE status = 'Reserved'
            GROUP BY stock_item_id, stock_location_id;";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var dict = new Dictionary<(Guid, Guid), decimal>();
        while (await reader.ReadAsync(ct))
        {
            var item = reader.GetGuid(0);
            var loc = reader.GetGuid(1);
            var qty = reader.GetDecimal(2);
            dict[(item, loc)] = qty;
        }
        return dict;
    }

    public async Task ResetAppliedEventsAsync(CancellationToken ct = default)
    {
        const string sql = "DELETE FROM inventory.reservation_balance_applied_events;";
        await using var cmd = _dataSource.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordAppliedEventsForRebuildAsync(IReadOnlyList<ReservationAppliedEvent> events, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) return;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            const string sql = @"
                INSERT INTO inventory.reservation_balance_applied_events (
                    id, reservation_id, event_type, terminal_status, stock_item_id, stock_location_id, quantity, applied_at
                ) VALUES (
                    $1, $2, $3, $4, $5, $6, $7, $8
                )
                ON CONFLICT (reservation_id, event_type) DO NOTHING;";

            foreach (var ev in events)
            {
                await using var cmd = new NpgsqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue(ev.Id);
                cmd.Parameters.AddWithValue(ev.ReservationId);
                cmd.Parameters.AddWithValue(ev.EventType);
                cmd.Parameters.AddWithValue((object?)ev.TerminalStatus ?? DBNull.Value);
                cmd.Parameters.AddWithValue(ev.StockItemId);
                cmd.Parameters.AddWithValue(ev.StockLocationId);
                cmd.Parameters.AddWithValue(ev.Quantity);
                cmd.Parameters.AddWithValue(ev.AppliedAt);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static StockBalance MapRow(NpgsqlDataReader reader)
    {
        return new StockBalance(
            id: reader.GetGuid(0),
            stockItemId: reader.GetGuid(1),
            stockLocationId: reader.GetGuid(2),
            onHandQuantity: reader.GetDecimal(3),
            reservedQuantity: reader.GetDecimal(4),
            availableQuantity: reader.GetDecimal(5),
            updatedAt: reader.GetFieldValue<DateTimeOffset>(6),
            rowVersion: reader.GetInt32(7));
    }
}
