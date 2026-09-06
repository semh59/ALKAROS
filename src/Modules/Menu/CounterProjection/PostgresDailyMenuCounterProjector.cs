using System.Diagnostics;
using System.Globalization;
using Npgsql;

namespace ALKAROS.Menu.CounterProjection;

public sealed class PostgresDailyMenuCounterProjector : IDailyMenuCounterProjector
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDailyMenuCounterProjector(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<DailyMenuItemCounters?> GetCountersAsync(Guid dailyMenuItemId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT daily_menu_item_id, planned_portions, prepared_portions, available_portions,
                   reserved_portions, consumed_portions, waste_portions, out_of_stock
            FROM menu.daily_menu_items
            WHERE daily_menu_item_id = $1;
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(dailyMenuItemId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<ApplyCounterResult> ApplyProductionOutputAsync(
        ProductionOutputCounterEvent evt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.DailyMenuItemId == Guid.Empty)
            throw new ArgumentException("DailyMenuItemId cannot be empty.", nameof(evt));
        if (evt.ProductionOutputId == Guid.Empty)
            throw new ArgumentException("ProductionOutputId cannot be empty.", nameof(evt));
        if (evt.Quantity <= 0m)
            throw new InvalidCounterDeltaException($"ProductionOutput quantity must be strictly positive, got {evt.Quantity}.");

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Record applied event for idempotency
            const string insertEventSql = """
                INSERT INTO menu.daily_menu_counter_applied_events (
                    id, daily_menu_item_id, event_type, source_event_id, terminal_status, quantity, applied_at
                ) VALUES (
                    $1, $2, 'ProductionOutput', $3, NULL, $4, $5
                )
                ON CONFLICT (daily_menu_item_id, event_type, source_event_id) DO NOTHING
                RETURNING id;
                """;

            await using var evtCmd = new NpgsqlCommand(insertEventSql, conn, tx);
            evtCmd.Parameters.AddWithValue(Guid.NewGuid());
            evtCmd.Parameters.AddWithValue(evt.DailyMenuItemId);
            evtCmd.Parameters.AddWithValue(evt.ProductionOutputId);
            evtCmd.Parameters.AddWithValue(evt.Quantity);
            evtCmd.Parameters.AddWithValue(evt.OccurredAt ?? DateTimeOffset.UtcNow);

            var eventInserted = await evtCmd.ExecuteScalarAsync(ct);
            if (eventInserted == null)
            {
                // Idempotent replay: already applied
                await tx.RollbackAsync(ct);
                var existing = await GetCountersAsync(evt.DailyMenuItemId, ct)
                    ?? throw new DailyMenuItemNotFoundException(evt.DailyMenuItemId);
                return new ApplyCounterResult(existing, IsIdempotentReplay: true);
            }

            // 2. Atomically update prepared_portions and available_portions
            const string updateSql = """
                UPDATE menu.daily_menu_items
                SET prepared_portions = prepared_portions + $1,
                    available_portions = (prepared_portions + $1) - reserved_portions - consumed_portions - waste_portions,
                    out_of_stock = (((prepared_portions + $1) - reserved_portions - consumed_portions - waste_portions) <= 0),
                    updated_at = NOW()
                WHERE daily_menu_item_id = $2
                RETURNING daily_menu_item_id, planned_portions, prepared_portions, available_portions,
                          reserved_portions, consumed_portions, waste_portions, out_of_stock;
                """;

            await using var updateCmd = new NpgsqlCommand(updateSql, conn, tx);
            updateCmd.Parameters.AddWithValue(evt.Quantity);
            updateCmd.Parameters.AddWithValue(evt.DailyMenuItemId);

            await using var reader = await updateCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await reader.CloseAsync();
                await tx.RollbackAsync(ct);
                throw new DailyMenuItemNotFoundException(evt.DailyMenuItemId);
            }

            var counters = MapRow(reader);
            await reader.CloseAsync();

            await tx.CommitAsync(ct);
            return new ApplyCounterResult(counters, IsIdempotentReplay: false);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ApplyCounterResult> ApplyReservationReservedAsync(
        ReservationReservedCounterEvent evt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.DailyMenuItemId == Guid.Empty)
            throw new ArgumentException("DailyMenuItemId cannot be empty.", nameof(evt));
        if (evt.ReservationId == Guid.Empty)
            throw new ArgumentException("ReservationId cannot be empty.", nameof(evt));
        if (evt.Quantity <= 0m)
            throw new InvalidCounterDeltaException($"Reservation quantity must be strictly positive, got {evt.Quantity}.");

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Record applied event for idempotency
            const string insertEventSql = """
                INSERT INTO menu.daily_menu_counter_applied_events (
                    id, daily_menu_item_id, event_type, source_event_id, terminal_status, quantity, applied_at
                ) VALUES (
                    $1, $2, 'Reserved', $3, NULL, $4, $5
                )
                ON CONFLICT (daily_menu_item_id, event_type, source_event_id) DO NOTHING
                RETURNING id;
                """;

            await using var evtCmd = new NpgsqlCommand(insertEventSql, conn, tx);
            evtCmd.Parameters.AddWithValue(Guid.NewGuid());
            evtCmd.Parameters.AddWithValue(evt.DailyMenuItemId);
            evtCmd.Parameters.AddWithValue(evt.ReservationId);
            evtCmd.Parameters.AddWithValue(evt.Quantity);
            evtCmd.Parameters.AddWithValue(evt.OccurredAt ?? DateTimeOffset.UtcNow);

            var eventInserted = await evtCmd.ExecuteScalarAsync(ct);
            if (eventInserted == null)
            {
                // Idempotent replay: already applied
                await tx.RollbackAsync(ct);
                var existing = await GetCountersAsync(evt.DailyMenuItemId, ct)
                    ?? throw new DailyMenuItemNotFoundException(evt.DailyMenuItemId);
                return new ApplyCounterResult(existing, IsIdempotentReplay: true);
            }

            // 2. Atomically update reserved_portions and available_portions
            const string updateSql = """
                UPDATE menu.daily_menu_items
                SET reserved_portions = reserved_portions + $1,
                    available_portions = prepared_portions - (reserved_portions + $1) - consumed_portions - waste_portions,
                    out_of_stock = ((prepared_portions - (reserved_portions + $1) - consumed_portions - waste_portions) <= 0),
                    updated_at = NOW()
                WHERE daily_menu_item_id = $2
                RETURNING daily_menu_item_id, planned_portions, prepared_portions, available_portions,
                          reserved_portions, consumed_portions, waste_portions, out_of_stock;
                """;

            await using var updateCmd = new NpgsqlCommand(updateSql, conn, tx);
            updateCmd.Parameters.AddWithValue(evt.Quantity);
            updateCmd.Parameters.AddWithValue(evt.DailyMenuItemId);

            await using var reader = await updateCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await reader.CloseAsync();
                await tx.RollbackAsync(ct);
                throw new DailyMenuItemNotFoundException(evt.DailyMenuItemId);
            }

            var counters = MapRow(reader);
            await reader.CloseAsync();

            await tx.CommitAsync(ct);
            return new ApplyCounterResult(counters, IsIdempotentReplay: false);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ApplyCounterResult> ApplyReservationTerminalAsync(
        ReservationTerminalCounterEvent evt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.DailyMenuItemId == Guid.Empty)
            throw new ArgumentException("DailyMenuItemId cannot be empty.", nameof(evt));
        if (evt.ReservationId == Guid.Empty)
            throw new ArgumentException("ReservationId cannot be empty.", nameof(evt));
        if (evt.Quantity <= 0m)
            throw new InvalidCounterDeltaException($"Reservation quantity must be strictly positive, got {evt.Quantity}.");

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Record applied terminal event for idempotency
            const string insertEventSql = """
                INSERT INTO menu.daily_menu_counter_applied_events (
                    id, daily_menu_item_id, event_type, source_event_id, terminal_status, quantity, applied_at
                ) VALUES (
                    $1, $2, 'Terminal', $3, $4, $5, $6
                )
                ON CONFLICT (daily_menu_item_id, event_type, source_event_id) DO NOTHING
                RETURNING id;
                """;

            await using var evtCmd = new NpgsqlCommand(insertEventSql, conn, tx);
            evtCmd.Parameters.AddWithValue(Guid.NewGuid());
            evtCmd.Parameters.AddWithValue(evt.DailyMenuItemId);
            evtCmd.Parameters.AddWithValue(evt.ReservationId);
            evtCmd.Parameters.AddWithValue(evt.TerminalStatus.ToString());
            evtCmd.Parameters.AddWithValue(evt.Quantity);
            evtCmd.Parameters.AddWithValue(evt.OccurredAt ?? DateTimeOffset.UtcNow);

            var eventInserted = await evtCmd.ExecuteScalarAsync(ct);
            if (eventInserted == null)
            {
                // Idempotent replay: already applied
                await tx.RollbackAsync(ct);
                var existing = await GetCountersAsync(evt.DailyMenuItemId, ct)
                    ?? throw new DailyMenuItemNotFoundException(evt.DailyMenuItemId);
                return new ApplyCounterResult(existing, IsIdempotentReplay: true);
            }

            // 2. Adjust counters based on terminal status:
            // Released: reserved_portions -= qty, consumed and waste unchanged. Available increases by qty.
            // Consumed: reserved_portions -= qty, consumed_portions += qty. Available unchanged.
            // Waste: reserved_portions -= qty, waste_portions += qty. Available unchanged.
            string updateSql;
            if (evt.TerminalStatus == ReservationCounterTerminalStatus.Released)
            {
                updateSql = """
                    UPDATE menu.daily_menu_items
                    SET reserved_portions = GREATEST(0, reserved_portions - $1),
                        available_portions = prepared_portions - GREATEST(0, reserved_portions - $1) - consumed_portions - waste_portions,
                        out_of_stock = ((prepared_portions - GREATEST(0, reserved_portions - $1) - consumed_portions - waste_portions) <= 0),
                        updated_at = NOW()
                    WHERE daily_menu_item_id = $2
                    RETURNING daily_menu_item_id, planned_portions, prepared_portions, available_portions,
                              reserved_portions, consumed_portions, waste_portions, out_of_stock;
                    """;
            }
            else if (evt.TerminalStatus == ReservationCounterTerminalStatus.Consumed)
            {
                updateSql = """
                    UPDATE menu.daily_menu_items
                    SET reserved_portions = GREATEST(0, reserved_portions - $1),
                        consumed_portions = consumed_portions + $1,
                        available_portions = prepared_portions - GREATEST(0, reserved_portions - $1) - (consumed_portions + $1) - waste_portions,
                        out_of_stock = ((prepared_portions - GREATEST(0, reserved_portions - $1) - (consumed_portions + $1) - waste_portions) <= 0),
                        updated_at = NOW()
                    WHERE daily_menu_item_id = $2
                    RETURNING daily_menu_item_id, planned_portions, prepared_portions, available_portions,
                              reserved_portions, consumed_portions, waste_portions, out_of_stock;
                    """;
            }
            else // Waste
            {
                updateSql = """
                    UPDATE menu.daily_menu_items
                    SET reserved_portions = GREATEST(0, reserved_portions - $1),
                        waste_portions = waste_portions + $1,
                        available_portions = prepared_portions - GREATEST(0, reserved_portions - $1) - consumed_portions - (waste_portions + $1),
                        out_of_stock = ((prepared_portions - GREATEST(0, reserved_portions - $1) - consumed_portions - (waste_portions + $1)) <= 0),
                        updated_at = NOW()
                    WHERE daily_menu_item_id = $2
                    RETURNING daily_menu_item_id, planned_portions, prepared_portions, available_portions,
                              reserved_portions, consumed_portions, waste_portions, out_of_stock;
                    """;
            }

            await using var updateCmd = new NpgsqlCommand(updateSql, conn, tx);
            updateCmd.Parameters.AddWithValue(evt.Quantity);
            updateCmd.Parameters.AddWithValue(evt.DailyMenuItemId);

            await using var reader = await updateCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await reader.CloseAsync();
                await tx.RollbackAsync(ct);
                throw new DailyMenuItemNotFoundException(evt.DailyMenuItemId);
            }

            var counters = MapRow(reader);
            await reader.CloseAsync();

            await tx.CommitAsync(ct);
            return new ApplyCounterResult(counters, IsIdempotentReplay: false);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<DailyMenuCounterRebuildReport> RebuildDailyMenuCountersAsync(
        Guid dailyMenuId,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Get all items for this daily menu
            const string getItemsSql = """
                SELECT daily_menu_item_id
                FROM menu.daily_menu_items
                WHERE daily_menu_id = $1;
                """;

            var itemIds = new List<Guid>();
            await using (var cmd = new NpgsqlCommand(getItemsSql, conn, tx))
            {
                cmd.Parameters.AddWithValue(dailyMenuId);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    itemIds.Add(reader.GetGuid(0));
                }
            }

            if (itemIds.Count == 0)
            {
                await tx.CommitAsync(ct);
                sw.Stop();
                return new DailyMenuCounterRebuildReport(0, sw.Elapsed);
            }

            // 2. Clear projection applied events for these items
            const string clearEventsSql = """
                DELETE FROM menu.daily_menu_counter_applied_events
                WHERE daily_menu_item_id = ANY($1);
                """;

            await using (var clearCmd = new NpgsqlCommand(clearEventsSql, conn, tx))
            {
                clearCmd.Parameters.AddWithValue(itemIds.ToArray());
                await clearCmd.ExecuteNonQueryAsync(ct);
            }

            // 3. For each item, compute authoritative figures from production outputs and portion reservations
            foreach (var itemId in itemIds)
            {
                // Authoritative prepared: sum of production_outputs for batches linked to this daily_menu_item_id
                const string prepSql = """
                    SELECT COALESCE(SUM(po.quantity), 0)
                    FROM production.production_outputs po
                    JOIN production.production_batches pb ON po.production_batch_id = pb.production_batch_id
                    WHERE pb.daily_menu_item_id = $1;
                    """;

                decimal authPrepared = 0m;
                await using (var pCmd = new NpgsqlCommand(prepSql, conn, tx))
                {
                    pCmd.Parameters.AddWithValue(itemId);
                    var val = await pCmd.ExecuteScalarAsync(ct);
                    if (val != null && val != DBNull.Value)
                        authPrepared = Convert.ToDecimal(val, CultureInfo.InvariantCulture);
                }

                // Also record applied events for these production outputs to re-establish idempotency baseline
                const string recordPrepEventsSql = """
                    INSERT INTO menu.daily_menu_counter_applied_events (
                        id, daily_menu_item_id, event_type, source_event_id, terminal_status, quantity, applied_at
                    )
                    SELECT gen_random_uuid(), $1, 'ProductionOutput', po.production_output_id, NULL, po.quantity, po.created_at
                    FROM production.production_outputs po
                    JOIN production.production_batches pb ON po.production_batch_id = pb.production_batch_id
                    WHERE pb.daily_menu_item_id = $1
                    ON CONFLICT (daily_menu_item_id, event_type, source_event_id) DO NOTHING;
                    """;
                await using (var repCmd = new NpgsqlCommand(recordPrepEventsSql, conn, tx))
                {
                    repCmd.Parameters.AddWithValue(itemId);
                    await repCmd.ExecuteNonQueryAsync(ct);
                }

                // Authoritative reservations by status:
                // Look up reservations where metadata->>'daily_menu_item_id' matches this item
                const string rsvSql = """
                    SELECT status, COALESCE(SUM(quantity), 0)
                    FROM inventory.portion_reservations
                    WHERE metadata->>'daily_menu_item_id' = $1
                    GROUP BY status;
                    """;

                decimal authReserved = 0m;
                decimal authConsumed = 0m;
                decimal authWaste = 0m;

                await using (var rCmd = new NpgsqlCommand(rsvSql, conn, tx))
                {
                    rCmd.Parameters.AddWithValue(itemId.ToString());
                    await using var rReader = await rCmd.ExecuteReaderAsync(ct);
                    while (await rReader.ReadAsync(ct))
                    {
                        var status = rReader.GetString(0);
                        var qty = rReader.GetDecimal(1);
                        if (string.Equals(status, "Reserved", StringComparison.OrdinalIgnoreCase))
                            authReserved += qty;
                        else if (string.Equals(status, "Consumed", StringComparison.OrdinalIgnoreCase))
                            authConsumed += qty;
                        else if (string.Equals(status, "Waste", StringComparison.OrdinalIgnoreCase))
                            authWaste += qty;
                    }
                }

                // Re-record reservation applied events
                const string recordRsvCreatedSql = """
                    INSERT INTO menu.daily_menu_counter_applied_events (
                        id, daily_menu_item_id, event_type, source_event_id, terminal_status, quantity, applied_at
                    )
                    SELECT gen_random_uuid(), $1, 'Reserved', id, NULL, quantity, reserved_at
                    FROM inventory.portion_reservations
                    WHERE metadata->>'daily_menu_item_id' = $2
                    ON CONFLICT (daily_menu_item_id, event_type, source_event_id) DO NOTHING;
                    """;
                await using (var repRsvCmd1 = new NpgsqlCommand(recordRsvCreatedSql, conn, tx))
                {
                    repRsvCmd1.Parameters.AddWithValue(itemId);
                    repRsvCmd1.Parameters.AddWithValue(itemId.ToString());
                    await repRsvCmd1.ExecuteNonQueryAsync(ct);
                }

                const string recordRsvTerminalSql = """
                    INSERT INTO menu.daily_menu_counter_applied_events (
                        id, daily_menu_item_id, event_type, source_event_id, terminal_status, quantity, applied_at
                    )
                    SELECT gen_random_uuid(), $1, 'Terminal', id, status, quantity, COALESCE(transitioned_at, NOW())
                    FROM inventory.portion_reservations
                    WHERE metadata->>'daily_menu_item_id' = $2 AND status IN ('Released', 'Consumed', 'Waste')
                    ON CONFLICT (daily_menu_item_id, event_type, source_event_id) DO NOTHING;
                    """;
                await using (var repRsvCmd2 = new NpgsqlCommand(recordRsvTerminalSql, conn, tx))
                {
                    repRsvCmd2.Parameters.AddWithValue(itemId);
                    repRsvCmd2.Parameters.AddWithValue(itemId.ToString());
                    await repRsvCmd2.ExecuteNonQueryAsync(ct);
                }

                var authAvailable = authPrepared - authReserved - authConsumed - authWaste;
                var isOutOfStock = authAvailable <= 0m;

                // 4. Update projection on menu.daily_menu_items
                const string updateItemSql = """
                    UPDATE menu.daily_menu_items
                    SET prepared_portions = $1,
                        reserved_portions = $2,
                        consumed_portions = $3,
                        waste_portions = $4,
                        available_portions = $5,
                        out_of_stock = $6,
                        updated_at = NOW()
                    WHERE daily_menu_item_id = $7;
                    """;

                await using (var uCmd = new NpgsqlCommand(updateItemSql, conn, tx))
                {
                    uCmd.Parameters.AddWithValue(authPrepared);
                    uCmd.Parameters.AddWithValue(authReserved);
                    uCmd.Parameters.AddWithValue(authConsumed);
                    uCmd.Parameters.AddWithValue(authWaste);
                    uCmd.Parameters.AddWithValue(authAvailable);
                    uCmd.Parameters.AddWithValue(isOutOfStock);
                    uCmd.Parameters.AddWithValue(itemId);
                    await uCmd.ExecuteNonQueryAsync(ct);
                }
            }

            await tx.CommitAsync(ct);
            sw.Stop();
            return new DailyMenuCounterRebuildReport(itemIds.Count, sw.Elapsed);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<DailyMenuCounterDriftReport> DetectDriftAsync(
        Guid dailyMenuId,
        CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        const string sql = """
            SELECT
                dmi.daily_menu_item_id,
                dmi.prepared_portions AS proj_prep,
                COALESCE(auth_prep.qty, 0) AS auth_prep,
                dmi.reserved_portions AS proj_rsv,
                COALESCE(auth_rsv.qty, 0) AS auth_rsv,
                dmi.consumed_portions AS proj_cons,
                COALESCE(auth_cons.qty, 0) AS auth_cons,
                dmi.waste_portions AS proj_waste,
                COALESCE(auth_waste.qty, 0) AS auth_waste,
                dmi.available_portions AS proj_avail
            FROM menu.daily_menu_items dmi
            LEFT JOIN (
                SELECT pb.daily_menu_item_id, SUM(po.quantity) AS qty
                FROM production.production_outputs po
                JOIN production.production_batches pb ON po.production_batch_id = pb.production_batch_id
                WHERE pb.daily_menu_item_id IS NOT NULL
                GROUP BY pb.daily_menu_item_id
            ) auth_prep ON auth_prep.daily_menu_item_id = dmi.daily_menu_item_id
            LEFT JOIN (
                SELECT (metadata->>'daily_menu_item_id')::uuid AS item_id, SUM(quantity) AS qty
                FROM inventory.portion_reservations
                WHERE status = 'Reserved' AND metadata->>'daily_menu_item_id' IS NOT NULL
                GROUP BY (metadata->>'daily_menu_item_id')::uuid
            ) auth_rsv ON auth_rsv.item_id = dmi.daily_menu_item_id
            LEFT JOIN (
                SELECT (metadata->>'daily_menu_item_id')::uuid AS item_id, SUM(quantity) AS qty
                FROM inventory.portion_reservations
                WHERE status = 'Consumed' AND metadata->>'daily_menu_item_id' IS NOT NULL
                GROUP BY (metadata->>'daily_menu_item_id')::uuid
            ) auth_cons ON auth_cons.item_id = dmi.daily_menu_item_id
            LEFT JOIN (
                SELECT (metadata->>'daily_menu_item_id')::uuid AS item_id, SUM(quantity) AS qty
                FROM inventory.portion_reservations
                WHERE status = 'Waste' AND metadata->>'daily_menu_item_id' IS NOT NULL
                GROUP BY (metadata->>'daily_menu_item_id')::uuid
            ) auth_waste ON auth_waste.item_id = dmi.daily_menu_item_id
            WHERE dmi.daily_menu_id = $1;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(dailyMenuId);

        var drifts = new List<DailyMenuCounterDrift>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var itemId = reader.GetGuid(0);
            var projPrep = reader.GetDecimal(1);
            var authPrep = reader.GetDecimal(2);
            var projRsv = reader.GetDecimal(3);
            var authRsv = reader.GetDecimal(4);
            var projCons = reader.GetDecimal(5);
            var authCons = reader.GetDecimal(6);
            var projWaste = reader.GetDecimal(7);
            var authWaste = reader.GetDecimal(8);
            var projAvail = reader.GetDecimal(9);

            var expectedAvail = authPrep - authRsv - authCons - authWaste;

            var prepDrift = projPrep - authPrep;
            var rsvDrift = projRsv - authRsv;
            var consDrift = projCons - authCons;
            var wasteDrift = projWaste - authWaste;
            var availDrift = projAvail - expectedAvail;

            if (prepDrift != 0 || rsvDrift != 0 || consDrift != 0 || wasteDrift != 0 || availDrift != 0)
            {
                drifts.Add(new DailyMenuCounterDrift(
                    DailyMenuItemId: itemId,
                    ProjectedPrepared: projPrep,
                    AuthoritativePrepared: authPrep,
                    PreparedDrift: prepDrift,
                    ProjectedReserved: projRsv,
                    AuthoritativeReserved: authRsv,
                    ReservedDrift: rsvDrift,
                    ProjectedConsumed: projCons,
                    AuthoritativeConsumed: authCons,
                    ConsumedDrift: consDrift,
                    ProjectedWaste: projWaste,
                    AuthoritativeWaste: authWaste,
                    WasteDrift: wasteDrift,
                    ProjectedAvailable: projAvail,
                    AuthoritativeAvailable: expectedAvail,
                    AvailableDrift: availDrift));
            }
        }

        return new DailyMenuCounterDriftReport(drifts.Count > 0, drifts);
    }

    private static DailyMenuItemCounters MapRow(NpgsqlDataReader reader)
    {
        return new DailyMenuItemCounters(
            DailyMenuItemId: reader.GetGuid(0),
            PlannedPortions: reader.GetDecimal(1),
            PreparedPortions: reader.GetDecimal(2),
            AvailablePortions: reader.GetDecimal(3),
            ReservedPortions: reader.GetDecimal(4),
            ConsumedPortions: reader.GetDecimal(5),
            WastePortions: reader.GetDecimal(6),
            IsOutOfStock: reader.GetBoolean(7));
    }
}
