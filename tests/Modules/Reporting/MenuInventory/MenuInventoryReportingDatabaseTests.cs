using System.IO;
using System.Text.Json;
using ALKAROS.Reporting.MenuInventory;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Reporting.MenuInventory.Tests;

public sealed class MenuInventoryReportingTestDb : PgTestDatabase
{
    public MenuInventoryReportingTestDb() : base("alkaros_rpt_menu_test_") { }

    protected override async Task ApplySqlAsync()
    {
        // 1. Catalog schema & products table
        const string catalogSql = """
            CREATE SCHEMA IF NOT EXISTS catalog;
            CREATE SCHEMA IF NOT EXISTS recipe;
            CREATE TABLE IF NOT EXISTS catalog.products (
                product_id UUID PRIMARY KEY,
                sku VARCHAR(64) NOT NULL UNIQUE,
                name VARCHAR(255) NOT NULL,
                base_price NUMERIC(18,2) NOT NULL DEFAULT 0,
                status VARCHAR(32) NOT NULL DEFAULT 'active',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;
        await RunAsync(DataSource, catalogSql);

        // 2. Recipe versions
        var sql058 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-recipe-versions.up.sql"));
        await RunAsync(DataSource, sql058);

        // 3. Stock master (items & locations)
        var sql059 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql"));
        await RunAsync(DataSource, sql059);

        // 4. Stock movements
        var sql060 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-stock-movements.up.sql"));
        await RunAsync(DataSource, sql060);

        // 5. Stock balances
        var sql061 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "061-stock-balances.up.sql"));
        await RunAsync(DataSource, sql061);

        // 6. Inventory waste
        var sql063 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "063-waste-records.up.sql"));
        await RunAsync(DataSource, sql063);

        // 7. Portion reservations
        var sql064 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "064-portion-reservations.up.sql"));
        await RunAsync(DataSource, sql064);

        // 8. Daily menus
        var sql067 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-daily-menus.up.sql"));
        await RunAsync(DataSource, sql067);

        // 9. Production batches
        var sql071 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "071-production-batches.up.sql"));
        await RunAsync(DataSource, sql071);

        // 10. Production outputs and consumptions
        var sql072 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "072-production-outputs-consumptions.up.sql"));
        await RunAsync(DataSource, sql072);

        // 11. StockItem.ReorderPoint (V11-INV-009)
        var sql118 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "118-stock-items-reorder-point.up.sql"));
        await RunAsync(DataSource, sql118);

        // 12. Physical counts (V11-INV-008)
        var sql117 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "117-stock-physical-counts.up.sql"));
        await RunAsync(DataSource, sql117);

        // 13. Theoretical consumption records (V11-RCP-004)
        var sql116 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "116-theoretical-consumption-records.up.sql"));
        await RunAsync(DataSource, sql116);
    }
}

public sealed class MenuInventoryReportingDatabaseTests : IClassFixture<MenuInventoryReportingTestDb>
{
    private readonly MenuInventoryReportingTestDb _db;
    private readonly PostgresMenuInventoryReportingService _service;

    public MenuInventoryReportingDatabaseTests(MenuInventoryReportingTestDb db)
    {
        _db = db;
        _service = new PostgresMenuInventoryReportingService(db.DataSource);
    }

    [Fact]
    public async Task PortionConsumptionReportReturnsAccurateMetricsAndReconciliation()
    {
        var (menuId, itemId, productId, versionId, locationId) = await SeedDataAsync();

        // 1. Authoritative production output: 20 portions
        var batchId = Guid.NewGuid();
        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            const string batchSql = """
                INSERT INTO production.production_batches (
                    production_batch_id, batch_number, recipe_version_id, daily_menu_item_id,
                    status, planned_quantity, actual_quantity, destination_location_id, row_version
                ) VALUES (@bId, @bNum, @vId, @iId, 'Completed', 20.000, 20.000, @loc, 1);

                INSERT INTO production.production_outputs (
                    production_output_id, production_batch_id, stock_location_id, quantity, unit_code
                ) VALUES (@oId, @bId, @loc, 20.000, 'portion');
                """;
            await using var cmd = new NpgsqlCommand(batchSql, conn);
            cmd.Parameters.AddWithValue("bId", batchId);
            cmd.Parameters.AddWithValue("bNum", "PB-" + Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("vId", versionId);
            cmd.Parameters.AddWithValue("iId", itemId);
            cmd.Parameters.AddWithValue("loc", locationId);
            cmd.Parameters.AddWithValue("oId", Guid.NewGuid());
            await cmd.ExecuteNonQueryAsync();

            // 2. Authoritative reservations: 3 Reserved, 10 Consumed, 2 Waste
            var metaJson = JsonSerializer.Serialize(new { daily_menu_item_id = itemId.ToString() });
            var stockItemId = Guid.NewGuid();

            const string itemSql = """
                INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
                VALUES (@sId, @code, 'Portion Stock', 'FinishedProduct', 'portion', true, 1);
                """;
            await using var iCmd = new NpgsqlCommand(itemSql, conn);
            iCmd.Parameters.AddWithValue("sId", stockItemId);
            iCmd.Parameters.AddWithValue("code", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            await iCmd.ExecuteNonQueryAsync();

            const string rsvSql = """
                INSERT INTO inventory.portion_reservations (
                    id, order_id, order_item_id, stock_item_id, stock_location_id,
                    quantity, unit_code, status, version, created_by, metadata
                ) VALUES
                    (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @sId, @loc, 3.000, 'portion', 'Reserved', 1, gen_random_uuid(), @meta::jsonb),
                    (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @sId, @loc, 10.000, 'portion', 'Consumed', 2, gen_random_uuid(), @meta::jsonb),
                    (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @sId, @loc, 2.000, 'portion', 'Waste', 2, gen_random_uuid(), @meta::jsonb);
                """;
            await using var rCmd = new NpgsqlCommand(rsvSql, conn);
            rCmd.Parameters.AddWithValue("sId", stockItemId);
            rCmd.Parameters.AddWithValue("loc", locationId);
            rCmd.Parameters.AddWithValue("meta", metaJson);
            await rCmd.ExecuteNonQueryAsync();

            // 3. Update projected counters on daily_menu_items to match authoritative data
            const string updateSql = """
                UPDATE menu.daily_menu_items
                SET prepared_portions = 20.000,
                    reserved_portions = 3.000,
                    consumed_portions = 10.000,
                    waste_portions = 2.000,
                    available_portions = 5.000
                WHERE daily_menu_item_id = @iId;
                """;
            await using var uCmd = new NpgsqlCommand(updateSql, conn);
            uCmd.Parameters.AddWithValue("iId", itemId);
            await uCmd.ExecuteNonQueryAsync();
        }

        // Query report
        var report = await _service.GetPortionConsumptionReportAsync(new PortionConsumptionReportQuery());
        report.Items.Should().ContainSingle(i => i.DailyMenuItemId == itemId);

        var item = report.Items.Single(i => i.DailyMenuItemId == itemId);
        item.PreparedPortions.Should().Be(20.000m);
        item.ReservedPortions.Should().Be(3.000m);
        item.ConsumedPortions.Should().Be(10.000m);
        item.WastePortions.Should().Be(2.000m);
        item.AvailablePortions.Should().Be(5.000m);
        item.SellThroughRate.Should().Be(0.5000m); // 10 consumed / 20 prepared = 50%
        item.IsReconciled.Should().BeTrue();

        report.TotalPreparedPortions.Should().BeGreaterOrEqualTo(20.000m);
        report.TotalConsumedPortions.Should().BeGreaterOrEqualTo(10.000m);
    }

    [Fact]
    public async Task ProductionYieldReportComputesAccurateYieldRates()
    {
        var (_, itemId, _, versionId, locationId) = await SeedDataAsync();

        var batchId = Guid.NewGuid();
        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            const string sql = """
                INSERT INTO production.production_batches (
                    production_batch_id, batch_number, recipe_version_id, daily_menu_item_id,
                    status, planned_quantity, actual_quantity, destination_location_id, produced_at, row_version
                ) VALUES (@bId, @bNum, @vId, @iId, 'Completed', 10.000, 9.500, @loc, NOW(), 1);

                INSERT INTO production.production_outputs (
                    production_output_id, production_batch_id, stock_location_id, quantity, unit_code
                ) VALUES (@oId, @bId, @loc, 9.500, 'portion');
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("bId", batchId);
            cmd.Parameters.AddWithValue("bNum", "PB-" + Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("vId", versionId);
            cmd.Parameters.AddWithValue("iId", itemId);
            cmd.Parameters.AddWithValue("loc", locationId);
            cmd.Parameters.AddWithValue("oId", Guid.NewGuid());
            await cmd.ExecuteNonQueryAsync();
        }

        var report = await _service.GetProductionYieldReportAsync(new ProductionYieldReportQuery(RecipeVersionId: versionId));
        report.Items.Should().Contain(b => b.BatchId == batchId);

        var batchItem = report.Items.Single(b => b.BatchId == batchId);
        batchItem.PlannedQuantity.Should().Be(10.000m);
        batchItem.ActualQuantity.Should().Be(9.500m);
        batchItem.YieldRate.Should().Be(0.9500m); // 9.5 / 10 = 95%
        batchItem.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task WasteReportAggregatesPortionAndInventoryWaste()
    {
        var (_, _, _, _, locationId) = await SeedDataAsync();

        var stockItemId = Guid.NewGuid();
        var wasteRecordId = Guid.NewGuid();
        var portionWasteId = Guid.NewGuid();

        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            const string itemSql = """
                INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
                VALUES (@sId, @code, 'Onion Raw Material', 'RawMaterial', 'kg', true, 1);
                """;
            await using var iCmd = new NpgsqlCommand(itemSql, conn);
            iCmd.Parameters.AddWithValue("sId", stockItemId);
            iCmd.Parameters.AddWithValue("code", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            await iCmd.ExecuteNonQueryAsync();

            // 1. General inventory waste record
            var movementId = Guid.NewGuid();
            const string movSql = """
                INSERT INTO inventory.stock_movements (
                    stock_movement_id, stock_item_id, stock_location_id, movement_type, direction,
                    quantity, unit_code, source_type, created_at, created_by
                ) VALUES (
                    @mId, @sId, @loc, 'Waste', 'Outbound',
                    3.5000, 'kg', 'ManualWaste', NOW(), gen_random_uuid()
                );
                """;
            await using var mCmd = new NpgsqlCommand(movSql, conn);
            mCmd.Parameters.AddWithValue("mId", movementId);
            mCmd.Parameters.AddWithValue("sId", stockItemId);
            mCmd.Parameters.AddWithValue("loc", locationId);
            await mCmd.ExecuteNonQueryAsync();

            const string invWasteSql = """
                INSERT INTO inventory.waste_records (
                    id, stock_movement_id, stock_item_id, stock_location_id, waste_source,
                    quantity, unit_code, normalized_quantity, tracking_unit_code, waste_reason,
                    recorded_by, recorded_at
                ) VALUES (
                    @wId, @mId, @sId, @loc, 'ManualInventory',
                    3.5000, 'kg', 3.5000, 'kg', 'Spoiled due to temperature excursion',
                    gen_random_uuid(), NOW()
                );
                """;
            await using var wCmd = new NpgsqlCommand(invWasteSql, conn);
            wCmd.Parameters.AddWithValue("wId", wasteRecordId);
            wCmd.Parameters.AddWithValue("mId", movementId);
            wCmd.Parameters.AddWithValue("sId", stockItemId);
            wCmd.Parameters.AddWithValue("loc", locationId);
            await wCmd.ExecuteNonQueryAsync();

            // 2. Portion reservation waste
            const string portionWasteSql = """
                INSERT INTO inventory.portion_reservations (
                    id, order_id, order_item_id, stock_item_id, stock_location_id,
                    quantity, unit_code, status, version, created_by, transition_reason, transitioned_at
                ) VALUES (
                    @pId, gen_random_uuid(), gen_random_uuid(), @sId, @loc,
                    2.000, 'portion', 'Waste', 2, gen_random_uuid(), 'Dropped on floor', NOW()
                );
                """;
            await using var pCmd = new NpgsqlCommand(portionWasteSql, conn);
            pCmd.Parameters.AddWithValue("pId", portionWasteId);
            pCmd.Parameters.AddWithValue("sId", stockItemId);
            pCmd.Parameters.AddWithValue("loc", locationId);
            await pCmd.ExecuteNonQueryAsync();
        }

        var report = await _service.GetWasteReportAsync(new WasteReportQuery(LocationId: locationId));
        report.Items.Should().Contain(w => w.WasteId == wasteRecordId && w.Category == WasteReportCategory.InventoryWaste);
        report.Items.Should().Contain(w => w.WasteId == portionWasteId && w.Category == WasteReportCategory.PortionWaste);

        var invW = report.Items.Single(w => w.WasteId == wasteRecordId);
        invW.Quantity.Should().Be(3.5000m);
        invW.Reason.Should().Be("Spoiled due to temperature excursion");

        var prtW = report.Items.Single(w => w.WasteId == portionWasteId);
        prtW.Quantity.Should().Be(2.000m);
        prtW.Reason.Should().Be("Dropped on floor");
    }

    [Fact]
    public async Task CriticalStockReportIdentifiesLowStockWithReconciliation()
    {
        var (_, _, _, _, locationId) = await SeedDataAsync();

        var itemNormalId = Guid.NewGuid();
        var itemCriticalId = Guid.NewGuid();

        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            const string itemSql = """
                INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
                VALUES
                    (@i1, @c1, 'Normal Stock Milk', 'RawMaterial', 'L', true, 1),
                    (@i2, @c2, 'Critical Stock Flour', 'RawMaterial', 'kg', true, 1);
                """;
            await using var iCmd = new NpgsqlCommand(itemSql, conn);
            iCmd.Parameters.AddWithValue("i1", itemNormalId);
            iCmd.Parameters.AddWithValue("c1", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            iCmd.Parameters.AddWithValue("i2", itemCriticalId);
            iCmd.Parameters.AddWithValue("c2", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            await iCmd.ExecuteNonQueryAsync();

            const string balSql = """
                INSERT INTO inventory.stock_balances (
                    stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                    reserved_quantity, available_quantity, updated_at, row_version
                ) VALUES
                    (gen_random_uuid(), @i1, @loc, 50.0000, 5.0000, 45.0000, NOW(), 1),
                    (gen_random_uuid(), @i2, @loc, 5.0000, 3.0000, 2.0000, NOW(), 1);
                """;
            await using var bCmd = new NpgsqlCommand(balSql, conn);
            bCmd.Parameters.AddWithValue("i1", itemNormalId);
            bCmd.Parameters.AddWithValue("i2", itemCriticalId);
            bCmd.Parameters.AddWithValue("loc", locationId);
            await bCmd.ExecuteNonQueryAsync();

            // Authoritative reservation match:
            // item1: 5 L reserved, item2: 3 kg reserved
            const string rsvSql = """
                INSERT INTO inventory.portion_reservations (
                    id, order_id, order_item_id, stock_item_id, stock_location_id,
                    quantity, unit_code, status, version, created_by
                ) VALUES
                    (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @i1, @loc, 5.0000, 'L', 'Reserved', 1, gen_random_uuid()),
                    (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @i2, @loc, 3.0000, 'kg', 'Reserved', 1, gen_random_uuid());
                """;
            await using var rCmd = new NpgsqlCommand(rsvSql, conn);
            rCmd.Parameters.AddWithValue("i1", itemNormalId);
            rCmd.Parameters.AddWithValue("i2", itemCriticalId);
            rCmd.Parameters.AddWithValue("loc", locationId);
            await rCmd.ExecuteNonQueryAsync();
        }

        // Critical threshold: 5.0 units.
        // item1 has 45 available -> NOT critical
        // item2 has 2 available -> CRITICAL
        var report = await _service.GetCriticalStockReportAsync(new CriticalStockReportQuery(
            LocationId: locationId,
            CriticalThreshold: 5.0m));

        report.Items.Should().Contain(i => i.StockItemId == itemNormalId && !i.IsCritical && i.IsReconciled);
        report.Items.Should().Contain(i => i.StockItemId == itemCriticalId && i.IsCritical && i.IsReconciled);
    }

    /// <summary>
    /// V11-INV-009: a persisted StockItem.ReorderPoint takes priority over
    /// the caller-supplied CriticalThreshold, per item — an item with no
    /// ReorderPoint configured still falls back to the old behavior.
    /// </summary>
    [Fact]
    public async Task CriticalStockReportPrefersThePersistedReorderPointOverTheCallerSuppliedThreshold()
    {
        var (_, _, _, _, locationId) = await SeedDataAsync();

        var itemWithHighReorderPointId = Guid.NewGuid();
        var itemWithNoReorderPointId = Guid.NewGuid();

        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            const string itemSql = """
                INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version, reorder_point)
                VALUES
                    (@i1, @c1, 'Configured Threshold Item', 'RawMaterial', 'kg', true, 1, 40.0000),
                    (@i2, @c2, 'Unconfigured Threshold Item', 'RawMaterial', 'kg', true, 1, NULL);
                """;
            await using var iCmd = new NpgsqlCommand(itemSql, conn);
            iCmd.Parameters.AddWithValue("i1", itemWithHighReorderPointId);
            iCmd.Parameters.AddWithValue("c1", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            iCmd.Parameters.AddWithValue("i2", itemWithNoReorderPointId);
            iCmd.Parameters.AddWithValue("c2", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            await iCmd.ExecuteNonQueryAsync();

            // item1's available (10) sits ABOVE the caller-supplied fallback
            // (5) but AT-OR-BELOW its own persisted ReorderPoint (40) — only
            // the persisted-threshold override flags it critical.
            const string balSql = """
                INSERT INTO inventory.stock_balances (
                    stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                    reserved_quantity, available_quantity, updated_at, row_version
                ) VALUES
                    (gen_random_uuid(), @i1, @loc, 10.0000, 0.0000, 10.0000, NOW(), 1),
                    (gen_random_uuid(), @i2, @loc, 45.0000, 0.0000, 45.0000, NOW(), 1);
                """;
            await using var bCmd = new NpgsqlCommand(balSql, conn);
            bCmd.Parameters.AddWithValue("i1", itemWithHighReorderPointId);
            bCmd.Parameters.AddWithValue("i2", itemWithNoReorderPointId);
            bCmd.Parameters.AddWithValue("loc", locationId);
            await bCmd.ExecuteNonQueryAsync();
        }

        // Caller-supplied threshold is 5.0. item1 has 10 available: under
        // the OLD (pre-V11-INV-009) behavior that would NOT be critical
        // (10 > 5), but its persisted ReorderPoint (40) overrides the
        // fallback and flags it critical (10 <= 40). item2 has 45 available
        // and no ReorderPoint, so it keeps the old (not critical) outcome.
        var report = await _service.GetCriticalStockReportAsync(new CriticalStockReportQuery(
            LocationId: locationId,
            CriticalThreshold: 5.0m));

        report.Items.Should().Contain(i => i.StockItemId == itemWithHighReorderPointId && i.IsCritical && i.CriticalThreshold == 40.0m);
        report.Items.Should().Contain(i => i.StockItemId == itemWithNoReorderPointId && !i.IsCritical && i.CriticalThreshold == 5.0m);
    }

    /// <summary>
    /// V11-RPT-003: ActualUsage = OpeningCount + PurchaseReceipts -
    /// ClosingCount, TheoreticalUsage from the shadow ledger, and variance
    /// is their difference — a fully-covered item (opening + closing count
    /// both present) gets a real row.
    /// </summary>
    [Fact]
    public async Task ActualVsTheoreticalReportComputesVarianceForAFullyCoveredItem()
    {
        var (_, _, _, versionId, locationId) = await SeedDataAsync();
        var stockItemId = Guid.NewGuid();
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;

        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            await using (var cmd = new NpgsqlCommand(
                """
                INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
                VALUES (@id, @code, 'AvT Flour', 'RawMaterial', 'kg', true, 1);
                """, conn))
            {
                cmd.Parameters.AddWithValue("id", stockItemId);
                cmd.Parameters.AddWithValue("code", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
                await cmd.ExecuteNonQueryAsync();
            }

            // Opening count (before the period): 100. Closing count (within
            // the period, at or before `to`): 60.
            await using (var cmd = new NpgsqlCommand(
                """
                INSERT INTO inventory.stock_physical_counts
                    (id, stock_item_id, stock_location_id, counted_quantity, previous_on_hand_quantity, counted_by_user_id, counted_at)
                VALUES
                    (gen_random_uuid(), @item, @loc, 100.0000, 100.0000, gen_random_uuid(), @openAt),
                    (gen_random_uuid(), @item, @loc, 60.0000, 60.0000, gen_random_uuid(), @closeAt);
                """, conn))
            {
                cmd.Parameters.AddWithValue("item", stockItemId);
                cmd.Parameters.AddWithValue("loc", locationId);
                cmd.Parameters.AddWithValue("openAt", from.AddDays(-1));
                cmd.Parameters.AddWithValue("closeAt", to.AddHours(-1));
                await cmd.ExecuteNonQueryAsync();
            }

            // A 30kg goods receipt inside the period.
            await using (var cmd = new NpgsqlCommand(
                """
                INSERT INTO inventory.stock_movements
                    (stock_movement_id, stock_item_id, stock_location_id, movement_type, direction, quantity, unit_code, source_type, created_at)
                VALUES
                    (gen_random_uuid(), @item, @loc, 'PurchaseReceipt', 'In', 30.0000, 'kg', 'GoodsReceipt', @receiptAt);
                """, conn))
            {
                cmd.Parameters.AddWithValue("item", stockItemId);
                cmd.Parameters.AddWithValue("loc", locationId);
                cmd.Parameters.AddWithValue("receiptAt", to.AddDays(-2));
                await cmd.ExecuteNonQueryAsync();
            }

            // Theoretical usage: recipe says 65kg should have been used.
            await using (var cmd = new NpgsqlCommand(
                """
                INSERT INTO recipe.theoretical_consumption_records
                    (id, order_item_id, product_id, recipe_id, recipe_version_id, stock_item_id, quantity, unit_code, recorded_at)
                SELECT gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), rv.recipe_id, rv.id, @item, 65.0000, 'kg', @recordedAt
                FROM recipe.recipe_versions rv WHERE rv.id = @versionId;
                """, conn))
            {
                cmd.Parameters.AddWithValue("item", stockItemId);
                cmd.Parameters.AddWithValue("versionId", versionId);
                cmd.Parameters.AddWithValue("recordedAt", to.AddDays(-3));
                await cmd.ExecuteNonQueryAsync();
            }
        }

        var report = await _service.GetActualVsTheoreticalReportAsync(
            new ActualVsTheoreticalReportQuery(from, to, locationId));

        var row = report.Items.Should().ContainSingle(i => i.StockItemId == stockItemId).Subject;
        row.OpeningCount.Should().Be(100m);
        row.ClosingCount.Should().Be(60m);
        row.PurchaseReceipts.Should().Be(30m);
        row.ActualUsage.Should().Be(70m); // 100 + 30 - 60
        row.TheoreticalUsage.Should().Be(65m);
        row.VarianceQuantity.Should().Be(5m); // 70 actual - 65 theoretical
        row.VariancePercentage.Should().BeApproximately(5m / 65m, 0.0001m);
    }

    [Fact]
    public async Task ActualVsTheoreticalReportExcludesAnItemMissingAnOpeningCount()
    {
        var (_, _, _, _, locationId) = await SeedDataAsync();
        var stockItemId = Guid.NewGuid();
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;

        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            await using (var cmd = new NpgsqlCommand(
                """
                INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
                VALUES (@id, @code, 'No Opening Count Item', 'RawMaterial', 'kg', true, 1);
                """, conn))
            {
                cmd.Parameters.AddWithValue("id", stockItemId);
                cmd.Parameters.AddWithValue("code", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
                await cmd.ExecuteNonQueryAsync();
            }

            // Only ONE count, taken inside the period (no count before
            // `from` exists) — the opening side stays unresolved.
            await using (var cmd = new NpgsqlCommand(
                """
                INSERT INTO inventory.stock_physical_counts
                    (id, stock_item_id, stock_location_id, counted_quantity, previous_on_hand_quantity, counted_by_user_id, counted_at)
                VALUES (gen_random_uuid(), @item, @loc, 40.0000, 40.0000, gen_random_uuid(), @countAt);
                """, conn))
            {
                cmd.Parameters.AddWithValue("item", stockItemId);
                cmd.Parameters.AddWithValue("loc", locationId);
                cmd.Parameters.AddWithValue("countAt", to.AddDays(-1));
                await cmd.ExecuteNonQueryAsync();
            }
        }

        var report = await _service.GetActualVsTheoreticalReportAsync(
            new ActualVsTheoreticalReportQuery(from, to, locationId));

        report.Items.Should().NotContain(i => i.StockItemId == stockItemId);
        report.ExcludedForMissingCountsCount.Should().BeGreaterThanOrEqualTo(1);
    }

    private async Task<(Guid menuId, Guid itemId, Guid productId, Guid versionId, Guid locationId)> SeedDataAsync()
    {
        var menuId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using var conn = await _db.DataSource.OpenConnectionAsync();

        // Location
        const string locSql = """
            INSERT INTO inventory.stock_locations (id, code, name, location_type, is_active, row_version)
            VALUES (@lId, @lCode, 'Central Kitchen', 'Kitchen', true, 1);
            """;
        await using var lCmd = new NpgsqlCommand(locSql, conn);
        lCmd.Parameters.AddWithValue("lId", locationId);
        lCmd.Parameters.AddWithValue("lCode", "LOC-" + Guid.NewGuid().ToString("N")[..8]);
        await lCmd.ExecuteNonQueryAsync();

        // Product
        const string prodSql = """
            INSERT INTO catalog.products (product_id, sku, name, base_price, status, created_at, updated_at)
            VALUES (@pId, @sku, 'Organic Chicken Soup', 65.00, 'active', NOW(), NOW());
            """;
        await using var pCmd = new NpgsqlCommand(prodSql, conn);
        pCmd.Parameters.AddWithValue("pId", productId);
        pCmd.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
        await pCmd.ExecuteNonQueryAsync();

        // Recipe & Version
        const string rcpSql = """
            INSERT INTO recipe.recipes (id, code, name, created_at, row_version)
            VALUES (@rId, @rCode, 'Chicken Soup Recipe', NOW(), 1);

            INSERT INTO recipe.recipe_versions (id, recipe_id, version_number, status, yield_quantity, yield_unit_code, created_at, row_version)
            VALUES (@vId, @rId, 1, 'Active', 10.0000, 'portion', NOW(), 1);
            """;
        await using var rCmd = new NpgsqlCommand(rcpSql, conn);
        rCmd.Parameters.AddWithValue("rId", recipeId);
        rCmd.Parameters.AddWithValue("rCode", "RCP-" + Guid.NewGuid().ToString("N")[..8]);
        rCmd.Parameters.AddWithValue("vId", versionId);
        await rCmd.ExecuteNonQueryAsync();

        // Daily Menu
        var bDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Random.Shared.Next(-500, 500)));
        const string menuSql = """
            INSERT INTO menu.daily_menus (daily_menu_id, business_date, status, row_version, created_at, updated_at)
            VALUES (@mId, @bDate, 'Open', 1, NOW(), NOW());
            """;
        await using var mCmd = new NpgsqlCommand(menuSql, conn);
        mCmd.Parameters.AddWithValue("mId", menuId);
        mCmd.Parameters.AddWithValue("bDate", bDate);
        await mCmd.ExecuteNonQueryAsync();

        // Daily Menu Item
        const string itemSql = """
            INSERT INTO menu.daily_menu_items (
                daily_menu_item_id, daily_menu_id, product_id, product_name_snapshot,
                recipe_version_id, price, planned_portions, prepared_portions,
                available_portions, reserved_portions, consumed_portions, waste_portions,
                out_of_stock, active, created_at, updated_at
            ) VALUES (
                @iId, @mId, @pId, 'Organic Chicken Soup',
                @vId, 65.00, 25.000, 0.000,
                0.000, 0.000, 0.000, 0.000,
                true, true, NOW(), NOW()
            );
            """;
        await using var iCmd = new NpgsqlCommand(itemSql, conn);
        iCmd.Parameters.AddWithValue("iId", itemId);
        iCmd.Parameters.AddWithValue("mId", menuId);
        iCmd.Parameters.AddWithValue("pId", productId);
        iCmd.Parameters.AddWithValue("vId", versionId);
        await iCmd.ExecuteNonQueryAsync();

        return (menuId, itemId, productId, versionId, locationId);
    }
}
