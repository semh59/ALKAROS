using System.IO;
using System.Text.Json;
using ALKAROS.Menu.CounterProjection;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Menu.CounterProjection.Tests;

public sealed class CounterProjectionTestDb : PgTestDatabase
{
    public CounterProjectionTestDb() : base("alkaros_mnu_proj_test_") { }

    protected override async Task ApplySqlAsync()
    {
        // 1. Catalog schema & products table for daily menu items foreign key
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

        // 6. Portion reservations
        var sql064 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "064-portion-reservations.up.sql"));
        await RunAsync(DataSource, sql064);

        // 7. Daily menus
        var sql067 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-daily-menus.up.sql"));
        await RunAsync(DataSource, sql067);

        // 8. Production batches
        var sql071 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "071-production-batches.up.sql"));
        await RunAsync(DataSource, sql071);

        // 9. Production outputs and consumptions
        var sql072 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "072-production-outputs-consumptions.up.sql"));
        await RunAsync(DataSource, sql072);

        // 10. Migration 073: daily menu counter applied events
        var sql073 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "073-daily-menu-counter-applied-events.up.sql"));
        await RunAsync(DataSource, sql073);
    }

    public async Task RollbackMigration073Async()
    {
        var downSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "073-daily-menu-counter-applied-events.down.sql"));
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration073Async()
    {
        var upSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "073-daily-menu-counter-applied-events.up.sql"));
        await RunAsync(DataSource, upSql);
    }
}

public sealed class DailyMenuCounterProjectionDatabaseTests : IClassFixture<CounterProjectionTestDb>
{
    private readonly CounterProjectionTestDb _db;
    private readonly PostgresDailyMenuCounterProjector _projector;

    public DailyMenuCounterProjectionDatabaseTests(CounterProjectionTestDb db)
    {
        _db = db;
        _projector = new PostgresDailyMenuCounterProjector(db.DataSource);
    }

    [Fact]
    public async Task RollbackAndReapplyMigration073SucceedsWithoutErrors()
    {
        await _db.RollbackMigration073Async();
        await _db.ReapplyMigration073Async();
    }

    private async Task<(Guid menuId, Guid itemId, Guid productId, Guid locationId, Guid recipeVersionId)> SeedDailyMenuItemAsync()
    {
        var menuId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var recipeVersionId = Guid.NewGuid();

        await using var conn = await _db.DataSource.OpenConnectionAsync();

        // 1. Catalog product
        const string prodSql = """
            INSERT INTO catalog.products (product_id, sku, name, base_price, status, created_at, updated_at)
            VALUES (@pId, @sku, 'Lentil Soup', 50.00, 'active', NOW(), NOW());
            """;
        await using var pCmd = new NpgsqlCommand(prodSql, conn);
        pCmd.Parameters.AddWithValue("pId", productId);
        pCmd.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
        await pCmd.ExecuteNonQueryAsync();

        // 2. Location
        const string locSql = """
            INSERT INTO inventory.stock_locations (id, code, name, location_type, is_active, row_version)
            VALUES (@lId, @lCode, 'Kitchen', 'Kitchen', true, 1);
            """;
        await using var lCmd = new NpgsqlCommand(locSql, conn);
        lCmd.Parameters.AddWithValue("lId", locationId);
        lCmd.Parameters.AddWithValue("lCode", "LOC-" + Guid.NewGuid().ToString("N")[..8]);
        await lCmd.ExecuteNonQueryAsync();

        // 3. Recipe & Version
        const string rcpSql = """
            INSERT INTO recipe.recipes (id, code, name, created_at, row_version)
            VALUES (@rId, @rCode, 'Lentil Soup Recipe', NOW(), 1);

            INSERT INTO recipe.recipe_versions (id, recipe_id, version_number, status, yield_quantity, yield_unit_code, created_at, row_version)
            VALUES (@vId, @rId, 1, 'Active', 10.0000, 'portion', NOW(), 1);
            """;
        await using var rCmd = new NpgsqlCommand(rcpSql, conn);
        rCmd.Parameters.AddWithValue("rId", recipeId);
        rCmd.Parameters.AddWithValue("rCode", "RCP-" + Guid.NewGuid().ToString("N")[..8]);
        rCmd.Parameters.AddWithValue("vId", recipeVersionId);
        await rCmd.ExecuteNonQueryAsync();

        // 4. Daily menu
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Random.Shared.Next(-1000, 1000)));
        const string menuSql = """
            INSERT INTO menu.daily_menus (daily_menu_id, business_date, status, row_version, created_at, updated_at)
            VALUES (@mId, @bDate, 'Open', 1, NOW(), NOW());
            """;
        await using var mCmd = new NpgsqlCommand(menuSql, conn);
        mCmd.Parameters.AddWithValue("mId", menuId);
        mCmd.Parameters.AddWithValue("bDate", businessDate);
        await mCmd.ExecuteNonQueryAsync();

        // 5. Daily menu item (initially 0 prepared, 0 available, out_of_stock = true)
        const string itemSql = """
            INSERT INTO menu.daily_menu_items (
                daily_menu_item_id, daily_menu_id, product_id, product_name_snapshot,
                recipe_version_id, price, planned_portions, prepared_portions,
                available_portions, reserved_portions, consumed_portions, waste_portions,
                out_of_stock, active, created_at, updated_at
            ) VALUES (
                @iId, @mId, @pId, 'Lentil Soup',
                @vId, 50.00, 20.000, 0.000,
                0.000, 0.000, 0.000, 0.000,
                true, true, NOW(), NOW()
            );
            """;
        await using var iCmd = new NpgsqlCommand(itemSql, conn);
        iCmd.Parameters.AddWithValue("iId", itemId);
        iCmd.Parameters.AddWithValue("mId", menuId);
        iCmd.Parameters.AddWithValue("pId", productId);
        iCmd.Parameters.AddWithValue("vId", recipeVersionId);
        await iCmd.ExecuteNonQueryAsync();

        return (menuId, itemId, productId, locationId, recipeVersionId);
    }

    [Fact]
    public async Task ProductionOutputIncrementsPreparedAndAvailablePortions()
    {
        var (_, itemId, _, _, _) = await SeedDailyMenuItemAsync();

        var outputId = Guid.NewGuid();
        var evt = new ProductionOutputCounterEvent(
            DailyMenuItemId: itemId,
            ProductionOutputId: outputId,
            Quantity: 15.000m);

        var result = await _projector.ApplyProductionOutputAsync(evt);

        result.IsIdempotentReplay.Should().BeFalse();
        result.Counters.PreparedPortions.Should().Be(15.000m);
        result.Counters.AvailablePortions.Should().Be(15.000m);
        result.Counters.ReservedPortions.Should().Be(0.000m);
        result.Counters.ConsumedPortions.Should().Be(0.000m);
        result.Counters.WastePortions.Should().Be(0.000m);
        result.Counters.IsOutOfStock.Should().BeFalse();
    }

    [Fact]
    public async Task ProductionOutputIdempotencyReplayGeneratesZeroAdditionalEffects()
    {
        var (_, itemId, _, _, _) = await SeedDailyMenuItemAsync();

        var outputId = Guid.NewGuid();
        var evt = new ProductionOutputCounterEvent(
            DailyMenuItemId: itemId,
            ProductionOutputId: outputId,
            Quantity: 10.000m);

        var first = await _projector.ApplyProductionOutputAsync(evt);
        first.IsIdempotentReplay.Should().BeFalse();
        first.Counters.PreparedPortions.Should().Be(10.000m);

        // Replay same event
        var replay = await _projector.ApplyProductionOutputAsync(evt);
        replay.IsIdempotentReplay.Should().BeTrue();
        replay.Counters.PreparedPortions.Should().Be(10.000m);
        replay.Counters.AvailablePortions.Should().Be(10.000m);
    }

    [Fact]
    public async Task ReservationLifecycleTransitionsUpdateCountersAtomically()
    {
        var (_, itemId, _, _, _) = await SeedDailyMenuItemAsync();

        // 1. Produce 20 portions
        var outputId = Guid.NewGuid();
        await _projector.ApplyProductionOutputAsync(new ProductionOutputCounterEvent(itemId, outputId, 20.000m));

        // 2. Guest reserves 5 portions
        var rsv1 = Guid.NewGuid();
        var resRsv = await _projector.ApplyReservationReservedAsync(new ReservationReservedCounterEvent(itemId, rsv1, 5.000m));
        resRsv.IsIdempotentReplay.Should().BeFalse();
        resRsv.Counters.PreparedPortions.Should().Be(20.000m);
        resRsv.Counters.ReservedPortions.Should().Be(5.000m);
        resRsv.Counters.AvailablePortions.Should().Be(15.000m);
        resRsv.Counters.IsOutOfStock.Should().BeFalse();

        // Replay reservation creation
        var replayRsv = await _projector.ApplyReservationReservedAsync(new ReservationReservedCounterEvent(itemId, rsv1, 5.000m));
        replayRsv.IsIdempotentReplay.Should().BeTrue();
        replayRsv.Counters.ReservedPortions.Should().Be(5.000m);

        // 3. Guest cancels 1 portion, released back to available
        var termRelease = await _projector.ApplyReservationTerminalAsync(new ReservationTerminalCounterEvent(
            itemId, rsv1, ReservationCounterTerminalStatus.Released, 1.000m));
        termRelease.IsIdempotentReplay.Should().BeFalse();
        termRelease.Counters.ReservedPortions.Should().Be(4.000m);
        termRelease.Counters.AvailablePortions.Should().Be(16.000m);

        // 4. Guest consumes 2 portions
        var rsv2 = Guid.NewGuid();
        await _projector.ApplyReservationReservedAsync(new ReservationReservedCounterEvent(itemId, rsv2, 2.000m));
        var termConsume = await _projector.ApplyReservationTerminalAsync(new ReservationTerminalCounterEvent(
            itemId, rsv2, ReservationCounterTerminalStatus.Consumed, 2.000m));
        termConsume.IsIdempotentReplay.Should().BeFalse();
        termConsume.Counters.ConsumedPortions.Should().Be(2.000m);
        termConsume.Counters.ReservedPortions.Should().Be(4.000m);
        termConsume.Counters.AvailablePortions.Should().Be(14.000m);

        // 5. Another reservation wasted (e.g. prepared portion dropped on the floor)
        var rsv3 = Guid.NewGuid();
        await _projector.ApplyReservationReservedAsync(new ReservationReservedCounterEvent(itemId, rsv3, 1.000m));
        var termWaste = await _projector.ApplyReservationTerminalAsync(new ReservationTerminalCounterEvent(
            itemId, rsv3, ReservationCounterTerminalStatus.Waste, 1.000m));
        termWaste.IsIdempotentReplay.Should().BeFalse();
        termWaste.Counters.WastePortions.Should().Be(1.000m);
        termWaste.Counters.ReservedPortions.Should().Be(4.000m);
        termWaste.Counters.ConsumedPortions.Should().Be(2.000m);
        termWaste.Counters.AvailablePortions.Should().Be(13.000m);

        // Replay terminal waste event
        var replayWaste = await _projector.ApplyReservationTerminalAsync(new ReservationTerminalCounterEvent(
            itemId, rsv3, ReservationCounterTerminalStatus.Waste, 1.000m));
        replayWaste.IsIdempotentReplay.Should().BeTrue();
        replayWaste.Counters.WastePortions.Should().Be(1.000m);
        replayWaste.Counters.AvailablePortions.Should().Be(13.000m);
    }

    [Fact]
    public async Task FullRebuildReconstructsCountersFromAuthoritativeSources()
    {
        var (menuId, itemId, _, locationId, versionId) = await SeedDailyMenuItemAsync();

        // Create authoritative production batch and output in database
        var batchId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            const string bSql = """
                INSERT INTO production.production_batches (
                    production_batch_id, batch_number, recipe_version_id, daily_menu_item_id,
                    status, planned_quantity, actual_quantity, destination_location_id, row_version
                ) VALUES (@bId, @bNum, @vId, @iId, 'Completed', 12.0000, 12.0000, @loc, 1);

                INSERT INTO production.production_outputs (
                    production_output_id, production_batch_id, stock_location_id, quantity, unit_code
                ) VALUES (@oId, @bId, @loc, 12.0000, 'portion');
                """;
            await using var cmd = new NpgsqlCommand(bSql, conn);
            cmd.Parameters.AddWithValue("bId", batchId);
            cmd.Parameters.AddWithValue("bNum", "PB-" + Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("vId", versionId);
            cmd.Parameters.AddWithValue("iId", itemId);
            cmd.Parameters.AddWithValue("loc", locationId);
            cmd.Parameters.AddWithValue("oId", outputId);
            await cmd.ExecuteNonQueryAsync();

            // Create authoritative reservations in inventory.portion_reservations
            var metaJson = JsonSerializer.Serialize(new { daily_menu_item_id = itemId.ToString() });

            // 1 active Reserved (2 portions)
            // 1 Consumed (3 portions)
            // 1 Waste (1 portion)
            const string rSql = """
                INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
                VALUES (@sId, @sCode, 'Soup Stock Item', 'FinishedProduct', 'portion', true, 1);

                INSERT INTO inventory.portion_reservations (
                    id, order_id, order_item_id, stock_item_id, stock_location_id,
                    quantity, unit_code, status, version, created_by, metadata
                ) VALUES
                    (@r1, @o1, @oi1, @sId, @loc, 2.0000, 'portion', 'Reserved', 1, @usr, @meta::jsonb),
                    (@r2, @o2, @oi2, @sId, @loc, 3.0000, 'portion', 'Consumed', 2, @usr, @meta::jsonb),
                    (@r3, @o3, @oi3, @sId, @loc, 1.0000, 'portion', 'Waste', 2, @usr, @meta::jsonb);
                """;
            await using var rCmd = new NpgsqlCommand(rSql, conn);
            var stockItemId = Guid.NewGuid();
            rCmd.Parameters.AddWithValue("sId", stockItemId);
            rCmd.Parameters.AddWithValue("sCode", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            rCmd.Parameters.AddWithValue("r1", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("r2", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("r3", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("o1", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("o2", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("o3", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("oi1", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("oi2", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("oi3", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("loc", locationId);
            rCmd.Parameters.AddWithValue("usr", Guid.NewGuid());
            rCmd.Parameters.AddWithValue("meta", metaJson);
            await rCmd.ExecuteNonQueryAsync();
        }

        // Before rebuild, daily_menu_item has 0 prepared, 0 reserved, etc.
        var initial = await _projector.GetCountersAsync(itemId);
        initial!.PreparedPortions.Should().Be(0m);

        // Run Full Rebuild
        var report = await _projector.RebuildDailyMenuCountersAsync(menuId);
        report.RebuiltItemsCount.Should().Be(1);

        // Verify rebuilt state matches authoritative data:
        // prepared: 12, reserved: 2, consumed: 3, waste: 1, available: 12 - 2 - 3 - 1 = 6
        var rebuilt = await _projector.GetCountersAsync(itemId);
        rebuilt!.PreparedPortions.Should().Be(12.0000m);
        rebuilt.ReservedPortions.Should().Be(2.0000m);
        rebuilt.ConsumedPortions.Should().Be(3.0000m);
        rebuilt.WastePortions.Should().Be(1.0000m);
        rebuilt.AvailablePortions.Should().Be(6.0000m);
        rebuilt.IsOutOfStock.Should().BeFalse();

        // Drift check should report NO drift!
        var driftReport = await _projector.DetectDriftAsync(menuId);
        driftReport.HasDrift.Should().BeFalse();
        driftReport.Drifts.Should().BeEmpty();
    }

    [Fact]
    public async Task DriftDetectionDetectsMismatchAcrossProducerClasses()
    {
        var (menuId, itemId, _, locationId, versionId) = await SeedDailyMenuItemAsync();

        // Rebuild clean baseline
        await _projector.RebuildDailyMenuCountersAsync(menuId);

        // Baseline has no drift
        var initialDrift = await _projector.DetectDriftAsync(menuId);
        initialDrift.HasDrift.Should().BeFalse();

        // Manually tamper counters on menu.daily_menu_items to simulate drift
        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        {
            const string tamperSql = """
                UPDATE menu.daily_menu_items
                SET prepared_portions = 50.000,
                    reserved_portions = 10.000,
                    consumed_portions = 5.000,
                    waste_portions = 2.000,
                    available_portions = 33.000
                WHERE daily_menu_item_id = $1;
                """;
            await using var tCmd = new NpgsqlCommand(tamperSql, conn);
            tCmd.Parameters.AddWithValue(itemId);
            await tCmd.ExecuteNonQueryAsync();
        }

        // Drift detection should detect all differences!
        var driftReport = await _projector.DetectDriftAsync(menuId);
        driftReport.HasDrift.Should().BeTrue();
        driftReport.Drifts.Should().HaveCount(1);

        var d = driftReport.Drifts[0];
        d.DailyMenuItemId.Should().Be(itemId);
        d.ProjectedPrepared.Should().Be(50.000m);
        d.AuthoritativePrepared.Should().Be(0m);
        d.PreparedDrift.Should().Be(50.000m);

        d.ProjectedReserved.Should().Be(10.000m);
        d.AuthoritativeReserved.Should().Be(0m);
        d.ReservedDrift.Should().Be(10.000m);

        d.ProjectedConsumed.Should().Be(5.000m);
        d.AuthoritativeConsumed.Should().Be(0m);
        d.ConsumedDrift.Should().Be(5.000m);

        d.ProjectedWaste.Should().Be(2.000m);
        d.AuthoritativeWaste.Should().Be(0m);
        d.WasteDrift.Should().Be(2.000m);

        d.ProjectedAvailable.Should().Be(33.000m);
        d.AuthoritativeAvailable.Should().Be(0m);
        d.AvailableDrift.Should().Be(33.000m);

        // Rebuild restores sync and clears drift
        await _projector.RebuildDailyMenuCountersAsync(menuId);
        var clearedDrift = await _projector.DetectDriftAsync(menuId);
        clearedDrift.HasDrift.Should().BeFalse();
        clearedDrift.Drifts.Should().BeEmpty();
    }
}
