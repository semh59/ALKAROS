using System.Data.Common;
using System.Globalization;

namespace ALKAROS.Reporting.MenuInventory;

public sealed class PostgresMenuInventoryReportingService : IMenuInventoryReportingService
{
    private readonly DbDataSource _dataSource;

    public PostgresMenuInventoryReportingService(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<PortionConsumptionReport> GetPortionConsumptionReportAsync(
        PortionConsumptionReportQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        const string sql = """
            SELECT
                dmi.daily_menu_item_id,
                dm.business_date,
                dmi.product_name_snapshot,
                dmi.recipe_version_id,
                dmi.planned_portions,
                dmi.prepared_portions,
                dmi.reserved_portions,
                dmi.consumed_portions,
                dmi.waste_portions,
                dmi.available_portions,
                COALESCE(auth_prep.qty, 0) AS auth_prep,
                COALESCE(auth_rsv.qty, 0) AS auth_rsv,
                COALESCE(auth_cons.qty, 0) AS auth_cons,
                COALESCE(auth_waste.qty, 0) AS auth_waste
            FROM menu.daily_menu_items dmi
            JOIN menu.daily_menus dm ON dmi.daily_menu_id = dm.daily_menu_id
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
            WHERE (@fromDate::date IS NULL OR dm.business_date >= @fromDate::date)
              AND (@toDate::date IS NULL OR dm.business_date <= @toDate::date)
              AND (@recipeVersionId::uuid IS NULL OR dmi.recipe_version_id = @recipeVersionId::uuid)
            ORDER BY dm.business_date DESC, dmi.product_name_snapshot;
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        AddParameter(cmd, "fromDate", query.FromDate.HasValue ? query.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        AddParameter(cmd, "toDate", query.ToDate.HasValue ? query.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        AddParameter(cmd, "recipeVersionId", query.RecipeVersionId.HasValue ? query.RecipeVersionId.Value : DBNull.Value);

        var items = new List<PortionConsumptionReportItem>();
        decimal sumPlanned = 0m;
        decimal sumPrepared = 0m;
        decimal sumConsumed = 0m;
        decimal sumWaste = 0m;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var bDate = DateOnly.FromDateTime(reader.GetDateTime(1));
            var pName = reader.GetString(2);
            Guid? rcpVerId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
            var planned = reader.GetDecimal(4);
            var prepared = reader.GetDecimal(5);
            var reserved = reader.GetDecimal(6);
            var consumed = reader.GetDecimal(7);
            var waste = reader.GetDecimal(8);
            var available = reader.GetDecimal(9);

            var authPrep = reader.GetDecimal(10);
            var authRsv = reader.GetDecimal(11);
            var authCons = reader.GetDecimal(12);
            var authWaste = reader.GetDecimal(13);

            var isReconciled = (prepared == authPrep) && (reserved == authRsv) &&
                               (consumed == authCons) && (waste == authWaste);

            var sellThrough = prepared > 0m
                ? Math.Round(consumed / prepared, 4, MidpointRounding.AwayFromZero)
                : 0m;

            items.Add(new PortionConsumptionReportItem(
                DailyMenuItemId: id,
                BusinessDate: bDate,
                ProductName: pName,
                RecipeVersionId: rcpVerId,
                PlannedPortions: planned,
                PreparedPortions: prepared,
                ReservedPortions: reserved,
                ConsumedPortions: consumed,
                WastePortions: waste,
                AvailablePortions: available,
                SellThroughRate: sellThrough,
                IsReconciled: isReconciled));

            sumPlanned += planned;
            sumPrepared += prepared;
            sumConsumed += consumed;
            sumWaste += waste;
        }

        var overallSellThrough = sumPrepared > 0m
            ? Math.Round(sumConsumed / sumPrepared, 4, MidpointRounding.AwayFromZero)
            : 0m;

        return new PortionConsumptionReport(
            Items: items,
            TotalPlannedPortions: sumPlanned,
            TotalPreparedPortions: sumPrepared,
            TotalConsumedPortions: sumConsumed,
            TotalWastePortions: sumWaste,
            OverallSellThroughRate: overallSellThrough);
    }

    public async Task<ProductionYieldReport> GetProductionYieldReportAsync(
        ProductionYieldReportQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        const string sql = """
            SELECT
                pb.production_batch_id,
                pb.batch_number,
                pb.recipe_version_id,
                r.name AS recipe_name,
                pb.destination_location_id,
                loc.name AS loc_name,
                pb.status,
                pb.planned_quantity,
                pb.actual_quantity,
                pb.produced_at,
                COALESCE(po_sum.total_output, 0) AS outputs_sum
            FROM production.production_batches pb
            JOIN recipe.recipe_versions rv ON pb.recipe_version_id = rv.id
            JOIN recipe.recipes r ON rv.recipe_id = r.id
            LEFT JOIN inventory.stock_locations loc ON pb.destination_location_id = loc.id
            LEFT JOIN (
                SELECT production_batch_id, SUM(quantity) AS total_output
                FROM production.production_outputs
                GROUP BY production_batch_id
            ) po_sum ON po_sum.production_batch_id = pb.production_batch_id
            WHERE (@fromDate::date IS NULL OR DATE(pb.created_at) >= @fromDate::date)
              AND (@toDate::date IS NULL OR DATE(pb.created_at) <= @toDate::date)
              AND (@locId::uuid IS NULL OR pb.destination_location_id = @locId::uuid)
              AND (@recipeVersionId::uuid IS NULL OR pb.recipe_version_id = @recipeVersionId::uuid)
            ORDER BY pb.created_at DESC;
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        AddParameter(cmd, "fromDate", query.FromDate.HasValue ? query.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        AddParameter(cmd, "toDate", query.ToDate.HasValue ? query.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
        AddParameter(cmd, "locId", query.LocationId.HasValue ? query.LocationId.Value : DBNull.Value);
        AddParameter(cmd, "recipeVersionId", query.RecipeVersionId.HasValue ? query.RecipeVersionId.Value : DBNull.Value);

        var items = new List<ProductionYieldReportItem>();
        decimal sumPlanned = 0m;
        decimal sumActual = 0m;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var batchId = reader.GetGuid(0);
            var batchNum = reader.GetString(1);
            var rcpVerId = reader.GetGuid(2);
            var rcpName = reader.GetString(3);
            Guid? locId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
            string? locName = reader.IsDBNull(5) ? null : reader.GetString(5);
            var status = reader.GetString(6);
            var plannedQty = reader.GetDecimal(7);
            var actualQty = reader.GetDecimal(8);
            DateTimeOffset? producedAt = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9);
            var outputSum = reader.GetDecimal(10);

            // Reconcile: If batch is completed with actual quantity, outputs must match
            var isReconciled = status != "Completed" || (actualQty == outputSum);

            var yieldRate = plannedQty > 0m
                ? Math.Round(actualQty / plannedQty, 4, MidpointRounding.AwayFromZero)
                : 0m;

            items.Add(new ProductionYieldReportItem(
                BatchId: batchId,
                BatchNumber: batchNum,
                RecipeVersionId: rcpVerId,
                RecipeName: rcpName,
                DestinationLocationId: locId,
                DestinationLocationName: locName,
                Status: status,
                PlannedQuantity: plannedQty,
                ActualQuantity: actualQty,
                YieldRate: yieldRate,
                ProducedAt: producedAt,
                IsReconciled: isReconciled));

            sumPlanned += plannedQty;
            sumActual += actualQty;
        }

        var overallYield = sumPlanned > 0m
            ? Math.Round(sumActual / sumPlanned, 4, MidpointRounding.AwayFromZero)
            : 0m;

        return new ProductionYieldReport(
            Items: items,
            TotalPlannedQuantity: sumPlanned,
            TotalActualQuantity: sumActual,
            OverallYieldRate: overallYield);
    }

    public async Task<WasteReport> GetWasteReportAsync(
        WasteReportQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        var items = new List<WasteReportItem>();

        // 1. Portion Waste from inventory.portion_reservations
        if (!query.Category.HasValue || query.Category == WasteReportCategory.PortionWaste)
        {
            const string rsvSql = """
                SELECT
                    pr.id,
                    pr.stock_item_id,
                    COALESCE(si.name, 'Portion'),
                    pr.stock_location_id,
                    COALESCE(loc.name, 'Unknown Location'),
                    pr.quantity,
                    pr.unit_code,
                    pr.transition_reason,
                    COALESCE(pr.transitioned_at, pr.reserved_at) AS recorded_at
                FROM inventory.portion_reservations pr
                LEFT JOIN inventory.stock_items si ON pr.stock_item_id = si.id
                LEFT JOIN inventory.stock_locations loc ON pr.stock_location_id = loc.id
                WHERE pr.status = 'Waste'
                  AND (@fromTime::timestamptz IS NULL OR COALESCE(pr.transitioned_at, pr.reserved_at) >= @fromTime::timestamptz)
                  AND (@toTime::timestamptz IS NULL OR COALESCE(pr.transitioned_at, pr.reserved_at) <= @toTime::timestamptz)
                  AND (@locId::uuid IS NULL OR pr.stock_location_id = @locId::uuid);
                """;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = rsvSql;
            AddParameter(cmd, "fromTime", query.FromTime.HasValue ? query.FromTime.Value : DBNull.Value);
            AddParameter(cmd, "toTime", query.ToTime.HasValue ? query.ToTime.Value : DBNull.Value);
            AddParameter(cmd, "locId", query.LocationId.HasValue ? query.LocationId.Value : DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new WasteReportItem(
                    WasteId: reader.GetGuid(0),
                    Category: WasteReportCategory.PortionWaste,
                    ItemId: reader.GetGuid(1),
                    ItemName: reader.GetString(2),
                    LocationId: reader.GetGuid(3),
                    LocationName: reader.GetString(4),
                    Quantity: reader.GetDecimal(5),
                    UnitCode: reader.GetString(6),
                    Reason: reader.IsDBNull(7) ? null : reader.GetString(7),
                    RecordedAt: reader.GetFieldValue<DateTimeOffset>(8)));
            }
        }

        // 2. Inventory Waste from inventory.waste_records
        if (!query.Category.HasValue || query.Category == WasteReportCategory.InventoryWaste)
        {
            const string invSql = """
                SELECT
                    w.id,
                    w.stock_item_id,
                    si.name,
                    w.stock_location_id,
                    loc.name,
                    w.quantity,
                    w.unit_code,
                    w.waste_reason,
                    w.recorded_at
                FROM inventory.waste_records w
                JOIN inventory.stock_items si ON w.stock_item_id = si.id
                JOIN inventory.stock_locations loc ON w.stock_location_id = loc.id
                WHERE (@fromTime::timestamptz IS NULL OR w.recorded_at >= @fromTime::timestamptz)
                  AND (@toTime::timestamptz IS NULL OR w.recorded_at <= @toTime::timestamptz)
                  AND (@locId::uuid IS NULL OR w.stock_location_id = @locId::uuid);
                """;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = invSql;
            AddParameter(cmd, "fromTime", query.FromTime.HasValue ? query.FromTime.Value : DBNull.Value);
            AddParameter(cmd, "toTime", query.ToTime.HasValue ? query.ToTime.Value : DBNull.Value);
            AddParameter(cmd, "locId", query.LocationId.HasValue ? query.LocationId.Value : DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new WasteReportItem(
                    WasteId: reader.GetGuid(0),
                    Category: WasteReportCategory.InventoryWaste,
                    ItemId: reader.GetGuid(1),
                    ItemName: reader.GetString(2),
                    LocationId: reader.GetGuid(3),
                    LocationName: reader.GetString(4),
                    Quantity: reader.GetDecimal(5),
                    UnitCode: reader.GetString(6),
                    Reason: reader.IsDBNull(7) ? null : reader.GetString(7),
                    RecordedAt: reader.GetFieldValue<DateTimeOffset>(8)));
            }
        }

        items.Sort((a, b) => b.RecordedAt.CompareTo(a.RecordedAt));
        var totalQty = items.Sum(i => i.Quantity);

        return new WasteReport(
            Items: items,
            TotalWasteOccurrences: items.Count,
            TotalWastedQuantity: totalQty);
    }

    public async Task<CriticalStockReport> GetCriticalStockReportAsync(
        CriticalStockReportQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        var fallbackThreshold = query.CriticalThreshold ?? 0m;

        // V1-RMD-243: this used to CROSS JOIN every active item with every
        // active location, so an item that is only ever stocked in one
        // location (the overwhelmingly common case — StockLocationType
        // itself models Warehouse/Kitchen/Bar/etc. as distinct places) got
        // a fabricated "0 available" row (and therefore a false "critical"
        // alarm) at every OTHER location it was never assigned to. Now only
        // pairs the item actually has a real relationship with: its own
        // default_location_id, or any location a stock_balances row already
        // exists for (it was received/moved there at some point).
        const string sql = """
            SELECT
                si.id,
                si.code,
                si.name,
                si.item_type,
                si.tracking_unit_code,
                loc.id,
                loc.name,
                COALESCE(sb.on_hand_quantity, 0) AS on_hand,
                COALESCE(sb.reserved_quantity, 0) AS reserved,
                COALESCE(sb.available_quantity, 0) AS available,
                COALESCE(auth_rsv.qty, 0) AS auth_reserved,
                si.reorder_point
            FROM inventory.stock_items si
            JOIN inventory.stock_locations loc
                ON loc.is_active = true
               AND (loc.id = si.default_location_id
                    OR EXISTS (
                        SELECT 1 FROM inventory.stock_balances existing
                        WHERE existing.stock_item_id = si.id AND existing.stock_location_id = loc.id
                    ))
            LEFT JOIN inventory.stock_balances sb
                ON sb.stock_item_id = si.id AND sb.stock_location_id = loc.id
            LEFT JOIN (
                SELECT stock_item_id, stock_location_id, SUM(quantity) AS qty
                FROM inventory.portion_reservations
                WHERE status = 'Reserved'
                GROUP BY stock_item_id, stock_location_id
            ) auth_rsv ON auth_rsv.stock_item_id = si.id AND auth_rsv.stock_location_id = loc.id
            WHERE si.is_active = true
              AND (@locId::uuid IS NULL OR loc.id = @locId::uuid)
            ORDER BY si.name, loc.name;
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "locId", query.LocationId.HasValue ? query.LocationId.Value : DBNull.Value);

        var items = new List<CriticalStockReportItem>();
        int criticalCount = 0;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var itemId = reader.GetGuid(0);
            var itemCode = reader.GetString(1);
            var itemName = reader.GetString(2);
            var itemType = reader.GetString(3);
            var unitCode = reader.GetString(4);
            var locId = reader.GetGuid(5);
            var locName = reader.GetString(6);
            var onHand = reader.GetDecimal(7);
            var reserved = reader.GetDecimal(8);
            var available = reader.GetDecimal(9);
            var authReserved = reader.GetDecimal(10);
            // V11-INV-009: a persisted per-item threshold now takes priority
            // over the caller-supplied one — backward compatible, since a
            // null ReorderPoint (every item before this task, and any item
            // still unconfigured) falls back to the exact old behavior.
            var persistedReorderPoint = reader.IsDBNull(11) ? (decimal?)null : reader.GetDecimal(11);
            var threshold = persistedReorderPoint ?? fallbackThreshold;

            // Reconciled check: available == on_hand - reserved AND reserved == authReserved
            var isReconciled = (available == (onHand - reserved)) && (reserved == authReserved);
            var isCritical = available <= threshold;

            if (isCritical)
                criticalCount++;

            items.Add(new CriticalStockReportItem(
                StockItemId: itemId,
                StockItemCode: itemCode,
                StockItemName: itemName,
                ItemType: itemType,
                TrackingUnitCode: unitCode,
                StockLocationId: locId,
                LocationName: locName,
                OnHandQuantity: onHand,
                ReservedQuantity: reserved,
                AvailableQuantity: available,
                CriticalThreshold: threshold,
                IsCritical: isCritical,
                IsReconciled: isReconciled));
        }

        return new CriticalStockReport(
            Items: items,
            TotalCriticalItemsCount: criticalCount);
    }

    public async Task<ActualVsTheoreticalReport> GetActualVsTheoreticalReportAsync(
        ActualVsTheoreticalReportQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        // Candidate (item, location) pairs: anything with at least one
        // physical count on record at or before the period's end — the
        // opening/closing LATERAL joins below then resolve the nearest
        // count on each side, and a pair missing either side is excluded
        // in the C# loop (never estimated).
        const string sql = """
            WITH candidate_pairs AS (
                SELECT DISTINCT stock_item_id, stock_location_id
                FROM inventory.stock_physical_counts
                WHERE counted_at <= @to
                  AND (@locId::uuid IS NULL OR stock_location_id = @locId::uuid)
            )
            SELECT
                si.id,
                si.code,
                si.name,
                si.tracking_unit_code,
                cp.stock_location_id,
                loc.name,
                opening.counted_quantity,
                closing.counted_quantity,
                COALESCE(receipts.qty, 0),
                COALESCE(theoretical.qty, 0)
            FROM candidate_pairs cp
            JOIN inventory.stock_items si ON si.id = cp.stock_item_id
            JOIN inventory.stock_locations loc ON loc.id = cp.stock_location_id
            LEFT JOIN LATERAL (
                SELECT c.counted_quantity
                FROM inventory.stock_physical_counts c
                WHERE c.stock_item_id = cp.stock_item_id AND c.stock_location_id = cp.stock_location_id AND c.counted_at <= @from
                ORDER BY c.counted_at DESC LIMIT 1
            ) opening ON true
            LEFT JOIN LATERAL (
                SELECT c.counted_quantity
                FROM inventory.stock_physical_counts c
                WHERE c.stock_item_id = cp.stock_item_id AND c.stock_location_id = cp.stock_location_id AND c.counted_at <= @to
                ORDER BY c.counted_at DESC LIMIT 1
            ) closing ON true
            LEFT JOIN (
                SELECT stock_item_id, stock_location_id, SUM(quantity) AS qty
                FROM inventory.stock_movements
                WHERE source_type IN ('PurchaseOrder', 'GoodsReceipt') AND created_at > @from AND created_at <= @to
                GROUP BY stock_item_id, stock_location_id
            ) receipts ON receipts.stock_item_id = cp.stock_item_id AND receipts.stock_location_id = cp.stock_location_id
            LEFT JOIN (
                -- V1-RMD-418 (V1-RMD-398 G-02): an order item voided before the kitchen started had its stock
                -- given back (its Order Consumption movement reversed); the immutable theoretical ledger still
                -- holds its row, which would count uneaten food as expected usage.
                SELECT tc.stock_item_id, SUM(tc.quantity) AS qty
                FROM recipe.theoretical_consumption_records tc
                WHERE tc.recorded_at > @from AND tc.recorded_at <= @to
                  AND NOT EXISTS (
                      SELECT 1
                      FROM inventory.stock_movements consumed
                      JOIN inventory.stock_movements reversal
                        ON reversal.source_type = 'StockMovement'
                       AND reversal.source_reference_id = consumed.stock_movement_id
                       AND reversal.movement_type = 'Reversal'
                      WHERE consumed.source_type = 'Order'
                        AND consumed.source_reference_id = tc.order_item_id
                        AND consumed.movement_type = 'Consumption')
                GROUP BY tc.stock_item_id
            ) theoretical ON theoretical.stock_item_id = cp.stock_item_id
            ORDER BY si.name, loc.name;
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "from", query.FromTime);
        AddParameter(cmd, "to", query.ToTime);
        AddParameter(cmd, "locId", query.LocationId.HasValue ? query.LocationId.Value : DBNull.Value);

        var items = new List<ActualVsTheoreticalReportItem>();
        var excluded = 0;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (reader.IsDBNull(6) || reader.IsDBNull(7))
            {
                // Missing an opening or closing count for this period — no
                // false precision, exclude rather than estimate.
                excluded++;
                continue;
            }

            var openingCount = reader.GetDecimal(6);
            var closingCount = reader.GetDecimal(7);
            var purchaseReceipts = reader.GetDecimal(8);
            var theoreticalUsage = reader.GetDecimal(9);

            var actualUsage = openingCount + purchaseReceipts - closingCount;
            var varianceQuantity = actualUsage - theoreticalUsage;
            var variancePercentage = theoreticalUsage == 0m ? (decimal?)null : varianceQuantity / theoreticalUsage;

            items.Add(new ActualVsTheoreticalReportItem(
                StockItemId: reader.GetGuid(0),
                StockItemCode: reader.GetString(1),
                StockItemName: reader.GetString(2),
                TrackingUnitCode: reader.GetString(3),
                StockLocationId: reader.GetGuid(4),
                LocationName: reader.GetString(5),
                OpeningCount: openingCount,
                ClosingCount: closingCount,
                PurchaseReceipts: purchaseReceipts,
                ActualUsage: actualUsage,
                TheoreticalUsage: theoreticalUsage,
                VarianceQuantity: varianceQuantity,
                VariancePercentage: variancePercentage));
        }

        return new ActualVsTheoreticalReport(
            Items: items,
            ExcludedForMissingCountsCount: excluded);
    }

    private static void AddParameter(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
