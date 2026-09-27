using System;
using System.IO;
using System.Threading.Tasks;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Production.BatchLifecycle;
using ALKAROS.Production.StockEffects;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Production.StockEffects.Tests;

public sealed class ProductionStockEffectsTestDb : PgTestDatabase
{
    public ProductionStockEffectsTestDb() : base("alkaros_prd_effects_test_") { }

    protected override async Task ApplySqlAsync()
    {
        // 057: Unit conversions
        var sql057 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "057-unit-conversions.up.sql"));
        await RunAsync(DataSource, sql057);

        // 058: Recipe versions
        var sql058 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-recipe-versions.up.sql"));
        await RunAsync(DataSource, sql058);

        // 059: Stock master
        var sql059 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql"));
        await RunAsync(DataSource, sql059);

        // 060: Stock movements ledger
        var sql060 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-stock-movements.up.sql"));
        await RunAsync(DataSource, sql060);

        // 061: Stock balances projection
        var sql061 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "061-stock-balances.up.sql"));
        await RunAsync(DataSource, sql061);

        // Catalog products for daily menu items FK
        const string catalogSql = """
            CREATE SCHEMA IF NOT EXISTS catalog;
            CREATE TABLE IF NOT EXISTS catalog.products (
                product_id UUID PRIMARY KEY,
                code VARCHAR(64) NOT NULL UNIQUE,
                name VARCHAR(255) NOT NULL,
                base_price NUMERIC(18,2) NOT NULL DEFAULT 0,
                category_id UUID NULL,
                is_active BOOLEAN NOT NULL DEFAULT true,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;
        await RunAsync(DataSource, catalogSql);

        // 067: Daily menus
        var sql067 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-daily-menus.up.sql"));
        await RunAsync(DataSource, sql067);

        // 071: Production batches
        var sql071 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "071-production-batches.up.sql"));
        await RunAsync(DataSource, sql071);

        // 072: Production outputs and consumptions
        var sql072 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "072-production-outputs-consumptions.up.sql"));
        await RunAsync(DataSource, sql072);
    }

    public async Task RollbackMigration072Async()
    {
        var downSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "072-production-outputs-consumptions.down.sql"));
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration072Async()
    {
        var upSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "072-production-outputs-consumptions.up.sql"));
        await RunAsync(DataSource, upSql);
    }
}

public sealed class ProductionStockEffectsDatabaseTests : IClassFixture<ProductionStockEffectsTestDb>
{
    private readonly ProductionStockEffectsTestDb _db;
    private readonly ProductionStockEffectService _service;

    public ProductionStockEffectsDatabaseTests(ProductionStockEffectsTestDb db)
    {
        _db = db;
        _service = new ProductionStockEffectService(
            db.DataSource,
            new PostgresStockBalanceRepository(db.DataSource),
            new PostgresStockMovementRepository(db.DataSource),
            new ALKAROS.Measurements.UnitConverter());
    }

    [Fact]
    public async Task RollbackAndReapplyMigration072SucceedsWithoutErrors()
    {
        await _db.RollbackMigration072Async();
        await _db.ReapplyMigration072Async();
    }

    private async Task<(Guid recipeId, Guid versionId, Guid item1Id, Guid item2Id, Guid locationId)> SeedRecipeAndStockAsync()
    {
        var locationId = Guid.NewGuid();
        var item1Id = Guid.NewGuid(); // Tomatoes: tracked in kg
        var item2Id = Guid.NewGuid(); // Bay leaves: tracked in piece
        var recipeId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using var conn = await _db.DataSource.OpenConnectionAsync();

        // 1. Location
        const string locSql = """
            INSERT INTO inventory.stock_locations (id, code, name, location_type, is_active, row_version)
            VALUES (@id, @code, 'Main Kitchen', 'Kitchen', true, 1);
            """;
        await using var lCmd = new NpgsqlCommand(locSql, conn);
        lCmd.Parameters.AddWithValue("id", locationId);
        lCmd.Parameters.AddWithValue("code", "LOC-" + Guid.NewGuid().ToString("N")[..8]);
        await lCmd.ExecuteNonQueryAsync();

        // 2. Stock items
        const string itemSql = """
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
            VALUES
                (@id1, @code1, 'Tomatoes', 'RawMaterial', 'kg', true, 1),
                (@id2, @code2, 'Bay Leaves', 'RawMaterial', 'piece', true, 1);
            """;
        await using var iCmd = new NpgsqlCommand(itemSql, conn);
        iCmd.Parameters.AddWithValue("id1", item1Id);
        iCmd.Parameters.AddWithValue("code1", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
        iCmd.Parameters.AddWithValue("id2", item2Id);
        iCmd.Parameters.AddWithValue("code2", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
        await iCmd.ExecuteNonQueryAsync();

        // 3. Stock balances: 50 kg of tomatoes, 100 pieces of bay leaves
        const string balSql = """
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity, updated_at, row_version)
            VALUES
                (@b1, @id1, @loc, 50.0000, 0, 50.0000, NOW(), 1),
                (@b2, @id2, @loc, 100.0000, 0, 100.0000, NOW(), 1);
            """;
        await using var bCmd = new NpgsqlCommand(balSql, conn);
        bCmd.Parameters.AddWithValue("b1", Guid.NewGuid());
        bCmd.Parameters.AddWithValue("b2", Guid.NewGuid());
        bCmd.Parameters.AddWithValue("id1", item1Id);
        bCmd.Parameters.AddWithValue("id2", item2Id);
        bCmd.Parameters.AddWithValue("loc", locationId);
        await bCmd.ExecuteNonQueryAsync();

        // 4. Recipe and Version (Yield: 4 portions)
        const string rcpSql = """
            INSERT INTO recipe.recipes (id, code, name, created_at, row_version)
            VALUES (@rId, @rCode, 'Tomato Soup', NOW(), 1);

            INSERT INTO recipe.recipe_versions (id, recipe_id, version_number, status, yield_quantity, yield_unit_code, created_at, row_version)
            VALUES (@vId, @rId, 1, 'Active', 4.0000, 'portion', NOW(), 1);

            INSERT INTO recipe.recipe_ingredients (id, recipe_version_id, ingredient_item_id, quantity, unit_code, loss_percentage, created_at)
            VALUES
                (@ing1, @vId, @id1, 200.0000, 'g', 5.00, NOW()),
                (@ing2, @vId, @id2, 1.0000, 'piece', 0.00, NOW());
            """;
        await using var rCmd = new NpgsqlCommand(rcpSql, conn);
        rCmd.Parameters.AddWithValue("rId", recipeId);
        rCmd.Parameters.AddWithValue("rCode", "RCP-" + Guid.NewGuid().ToString("N")[..8]);
        rCmd.Parameters.AddWithValue("vId", versionId);
        rCmd.Parameters.AddWithValue("ing1", Guid.NewGuid());
        rCmd.Parameters.AddWithValue("ing2", Guid.NewGuid());
        rCmd.Parameters.AddWithValue("id1", item1Id);
        rCmd.Parameters.AddWithValue("id2", item2Id);
        await rCmd.ExecuteNonQueryAsync();

        return (recipeId, versionId, item1Id, item2Id, locationId);
    }

    private async Task<Guid> CreateBatchAsync(Guid recipeVersionId, Guid locationId, decimal plannedQuantity)
    {
        var batchId = Guid.NewGuid();
        var batchNumber = "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO production.production_batches (
                production_batch_id, batch_number, recipe_version_id, status,
                planned_quantity, actual_quantity, portion_unit_code, destination_location_id,
                created_at, updated_at, row_version
            ) VALUES (
                @id, @number, @recipeVersionId, 'Planned',
                @plannedQty, 0, 'portion', @locationId,
                NOW(), NOW(), 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", batchId);
        cmd.Parameters.AddWithValue("number", batchNumber);
        cmd.Parameters.AddWithValue("recipeVersionId", recipeVersionId);
        cmd.Parameters.AddWithValue("plannedQty", plannedQuantity);
        cmd.Parameters.AddWithValue("locationId", locationId);
        await cmd.ExecuteNonQueryAsync();

        return batchId;
    }

    [Fact]
    public async Task ExecuteBatchStockEffectsCreatesTraceableConsumptionsAndMovements()
    {
        var (_, versionId, tomatoId, bayLeafId, locationId) = await SeedRecipeAndStockAsync();
        var batchId = await CreateBatchAsync(versionId, locationId, plannedQuantity: 8.0m);

        // Actual production: 8 portions (scale = 8 / 4 = 2)
        // Tomato: 200 g * 2 = 400 g. Waste 5%: 400 * 1.05 = 420 g. In kg: 0.42 kg.
        // Bay leaf: 1 piece * 2 = 2 pieces. Waste 0%: 2 pieces.
        var cmd = new ExecuteBatchStockEffectsCommand(
            BatchId: batchId,
            ActualQuantity: 8.0m,
            SourceLocationId: locationId);

        var result = await _service.ExecuteBatchStockEffectsAsync(cmd);

        result.BatchId.Should().Be(batchId);
        result.ActualQuantity.Should().Be(8.0m);
        result.WasAlreadyExecuted.Should().BeFalse();
        result.Consumptions.Should().HaveCount(2);

        var tomatoCons = result.Consumptions.Should().ContainSingle(c => c.StockItemId == tomatoId).Subject;
        tomatoCons.NativeQuantity.Should().Be(420.0m);
        tomatoCons.NativeUnitCode.Should().Be("g");
        tomatoCons.Quantity.Should().Be(0.4200m);
        tomatoCons.UnitCode.Should().Be("kg");
        tomatoCons.StockMovementId.Should().NotBeNull();

        var bayCons = result.Consumptions.Should().ContainSingle(c => c.StockItemId == bayLeafId).Subject;
        bayCons.Quantity.Should().Be(2.0m);
        bayCons.UnitCode.Should().Be("piece");

        // Verify stock balances:
        // Tomatoes: 50 - 0.42 = 49.58 kg
        // Bay leaves: 100 - 2 = 98 pieces
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string checkBalSql = "SELECT available_quantity FROM inventory.stock_balances WHERE stock_item_id = $1 AND stock_location_id = $2;";

        await using (var cmd1 = new NpgsqlCommand(checkBalSql, conn))
        {
            cmd1.Parameters.AddWithValue(tomatoId);
            cmd1.Parameters.AddWithValue(locationId);
            var tomBal = Convert.ToDecimal(await cmd1.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            tomBal.Should().Be(49.5800m);
        }

        await using (var cmd2 = new NpgsqlCommand(checkBalSql, conn))
        {
            cmd2.Parameters.AddWithValue(bayLeafId);
            cmd2.Parameters.AddWithValue(locationId);
            var bayBal = Convert.ToDecimal(await cmd2.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            bayBal.Should().Be(98.0000m);
        }

        // Verify batch status completed
        const string checkBatchSql = "SELECT status, actual_quantity FROM production.production_batches WHERE production_batch_id = $1;";
        await using var bCmd = new NpgsqlCommand(checkBatchSql, conn);
        bCmd.Parameters.AddWithValue(batchId);
        await using var reader = await bCmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        reader.GetString(0).Should().Be("Completed");
        reader.GetDecimal(1).Should().Be(8.0m);
    }

    [Fact]
    public async Task ExecuteBatchStockEffectsWithOutputStockItemCreatesOutputMovementAndIncrementsStock()
    {
        var (_, versionId, _, _, locationId) = await SeedRecipeAndStockAsync();
        var batchId = await CreateBatchAsync(versionId, locationId, plannedQuantity: 12.0m);

        // Finished soup item tracked in stock
        var finishedSoupId = Guid.NewGuid();
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string soupItemSql = """
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
            VALUES (@id, @code, 'Prepared Tomato Soup Bowl', 'PreparedItem', 'portion', true, 1);
            """;
        await using var sCmd = new NpgsqlCommand(soupItemSql, conn);
        sCmd.Parameters.AddWithValue("id", finishedSoupId);
        sCmd.Parameters.AddWithValue("code", "SKU-SOUP-" + Guid.NewGuid().ToString("N")[..6]);
        await sCmd.ExecuteNonQueryAsync();

        var cmd = new ExecuteBatchStockEffectsCommand(
            BatchId: batchId,
            ActualQuantity: 12.0m,
            SourceLocationId: locationId,
            OutputStockItemId: finishedSoupId);

        var result = await _service.ExecuteBatchStockEffectsAsync(cmd);

        result.Output.Should().NotBeNull();
        result.Output!.StockItemId.Should().Be(finishedSoupId);
        result.Output.Quantity.Should().Be(12.0m);
        result.Output.StockMovementId.Should().NotBeNull();

        // Verify stock balance for finished soup was incremented by 12 portions
        const string checkBalSql = "SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = $1 AND stock_location_id = $2;";
        await using var balCmd = new NpgsqlCommand(checkBalSql, conn);
        balCmd.Parameters.AddWithValue(finishedSoupId);
        balCmd.Parameters.AddWithValue(locationId);
        var soupBal = Convert.ToDecimal(await balCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        soupBal.Should().Be(12.0000m);
    }

    [Fact]
    public async Task ReplayExecutionIsIdempotentAndCreatesZeroAdditionalMovements()
    {
        var (_, versionId, _, _, locationId) = await SeedRecipeAndStockAsync();
        var batchId = await CreateBatchAsync(versionId, locationId, plannedQuantity: 4.0m);

        var cmd = new ExecuteBatchStockEffectsCommand(
            BatchId: batchId,
            ActualQuantity: 4.0m,
            SourceLocationId: locationId);

        // First execution
        var firstResult = await _service.ExecuteBatchStockEffectsAsync(cmd);
        firstResult.WasAlreadyExecuted.Should().BeFalse();

        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string countMovementsSql = "SELECT count(*) FROM inventory.stock_movements WHERE source_reference_id = $1;";
        await using var cntCmd1 = new NpgsqlCommand(countMovementsSql, conn);
        cntCmd1.Parameters.AddWithValue(batchId);
        var movementCountFirst = Convert.ToInt64(await cntCmd1.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        movementCountFirst.Should().Be(2); // 2 ingredient consumptions

        // Second execution (replay / duplicate completion)
        var secondResult = await _service.ExecuteBatchStockEffectsAsync(cmd);
        secondResult.WasAlreadyExecuted.Should().BeTrue();
        secondResult.Consumptions.Should().HaveCount(2);

        // Verify count of movements did not increase!
        await using var cntCmd2 = new NpgsqlCommand(countMovementsSql, conn);
        cntCmd2.Parameters.AddWithValue(batchId);
        var movementCountSecond = Convert.ToInt64(await cntCmd2.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        movementCountSecond.Should().Be(movementCountFirst);
    }

    [Fact]
    public async Task InsufficientStockFailsAtomicallyWithoutSideEffects()
    {
        var (_, versionId, tomatoId, bayLeafId, locationId) = await SeedRecipeAndStockAsync();
        var batchId = await CreateBatchAsync(versionId, locationId, plannedQuantity: 1000.0m);

        // Ensure bay leaves has enough stock (500 pieces) so only tomatoes (needs 52.5 kg, has 50 kg) is insufficient
        await using (var upConn = await _db.DataSource.OpenConnectionAsync())
        {
            await using var upCmd = new NpgsqlCommand("UPDATE inventory.stock_balances SET on_hand_quantity = 500, available_quantity = 500 WHERE stock_item_id = $1;", upConn);
            upCmd.Parameters.AddWithValue(bayLeafId);
            await upCmd.ExecuteNonQueryAsync();
        }

        // Huge batch: 1000 portions
        // Requires: 200 g * 250 * 1.05 = 52.5 kg tomatoes (we only seeded 50 kg)
        var cmd = new ExecuteBatchStockEffectsCommand(
            BatchId: batchId,
            ActualQuantity: 1000.0m,
            SourceLocationId: locationId);

        var act = () => _service.ExecuteBatchStockEffectsAsync(cmd);

        await act.Should().ThrowAsync<InsufficientProductionStockException>()
            .Where(e => e.StockItemId == tomatoId
                     && e.StockLocationId == locationId
                     && e.RequiredQuantity == 52.5000m
                     && e.AvailableQuantity == 50.0000m);

        // Verify atomic rollback: no movements, no consumptions, stock untouched!
        await using var conn = await _db.DataSource.OpenConnectionAsync();

        const string countConsSql = "SELECT count(*) FROM production.production_consumptions WHERE production_batch_id = $1;";
        await using var cCmd = new NpgsqlCommand(countConsSql, conn);
        cCmd.Parameters.AddWithValue(batchId);
        var consCount = Convert.ToInt64(await cCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        consCount.Should().Be(0);

        const string countMovementsSql = "SELECT count(*) FROM inventory.stock_movements WHERE source_reference_id = $1;";
        await using var mCmd = new NpgsqlCommand(countMovementsSql, conn);
        mCmd.Parameters.AddWithValue(batchId);
        var movCount = Convert.ToInt64(await mCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        movCount.Should().Be(0);

        // Tomatoes balance must still be 50.0000 kg!
        const string checkBalSql = "SELECT available_quantity FROM inventory.stock_balances WHERE stock_item_id = $1 AND stock_location_id = $2;";
        await using var balCmd = new NpgsqlCommand(checkBalSql, conn);
        balCmd.Parameters.AddWithValue(tomatoId);
        balCmd.Parameters.AddWithValue(locationId);
        var tomBal = Convert.ToDecimal(await balCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        tomBal.Should().Be(50.0000m);

        // Batch status still Planned!
        const string checkBatchSql = "SELECT status FROM production.production_batches WHERE production_batch_id = $1;";
        await using var bCmd = new NpgsqlCommand(checkBatchSql, conn);
        bCmd.Parameters.AddWithValue(batchId);
        var status = (string)(await bCmd.ExecuteScalarAsync())!;
        status.Should().Be("Planned");
    }

    [Fact]
    public async Task ExecutionFailsForCancelledBatch()
    {
        var (_, versionId, _, _, locationId) = await SeedRecipeAndStockAsync();
        var batchId = await CreateBatchAsync(versionId, locationId, plannedQuantity: 4.0m);

        // Cancel batch
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string cancelSql = "UPDATE production.production_batches SET status = 'Cancelled' WHERE production_batch_id = $1;";
        await using var cCmd = new NpgsqlCommand(cancelSql, conn);
        cCmd.Parameters.AddWithValue(batchId);
        await cCmd.ExecuteNonQueryAsync();

        var cmd = new ExecuteBatchStockEffectsCommand(
            BatchId: batchId,
            ActualQuantity: 4.0m,
            SourceLocationId: locationId);

        var act = () => _service.ExecuteBatchStockEffectsAsync(cmd);
        await act.Should().ThrowAsync<InvalidProductionStockEffectException>()
            .WithMessage("*cancelled*");
    }

    /// <summary>
    /// V1-RMD-344 (independent 2026-09-26 audit, orta seviye bulgu): this module's own
    /// hardcoded ResolveConversionFactorAsync only knew metric mass/volume pairs directly -
    /// anything else (including two different Count-dimension units, like "adet" vs "piece")
    /// fell through to a raw recipe.unit_conversions lookup that would throw
    /// InvalidProductionStockEffectException with no matching row. The shared IUnitConverter
    /// this now delegates to already knows Count-dimension units natively (StandardUnits.All),
    /// so this succeeds with ZERO custom conversion row seeded - proving the fix actually
    /// replaced the duplicated logic, not just kept it working by accident.
    /// </summary>
    [Fact]
    public async Task ACountDimensionCrossUnitIngredientConvertsThroughTheSharedUnitConverterWithNoCustomDbRow()
    {
        var locationId = Guid.NewGuid();
        var eggId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using var conn = await _db.DataSource.OpenConnectionAsync();
        await using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type, is_active, row_version)
            VALUES (@loc, @locCode, 'Count Unit Test Kitchen', 'Kitchen', true, 1);

            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
            VALUES (@item, @itemCode, 'Eggs', 'RawMaterial', 'piece', true, 1);

            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity, updated_at, row_version)
            VALUES (@bal, @item, @loc, 100.0000, 0, 100.0000, NOW(), 1);

            INSERT INTO recipe.recipes (id, code, name, created_at, row_version)
            VALUES (@recipeId, @recipeCode, 'Egg Dish', NOW(), 1);

            INSERT INTO recipe.recipe_versions (id, recipe_id, version_number, status, yield_quantity, yield_unit_code, created_at, row_version)
            VALUES (@versionId, @recipeId, 1, 'Active', 1.0000, 'portion', NOW(), 1);

            INSERT INTO recipe.recipe_ingredients (id, recipe_version_id, ingredient_item_id, quantity, unit_code, loss_percentage, created_at)
            VALUES (@ing, @versionId, @item, 2.0000, 'adet', 0.00, NOW());
            """, conn))
        {
            cmd.Parameters.AddWithValue("loc", locationId);
            cmd.Parameters.AddWithValue("locCode", "LOC-" + Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("item", eggId);
            cmd.Parameters.AddWithValue("itemCode", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("bal", Guid.NewGuid());
            cmd.Parameters.AddWithValue("recipeId", recipeId);
            cmd.Parameters.AddWithValue("recipeCode", "RCP-" + Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("versionId", versionId);
            cmd.Parameters.AddWithValue("ing", Guid.NewGuid());
            await cmd.ExecuteNonQueryAsync();
        }

        var batchId = await CreateBatchAsync(versionId, locationId, plannedQuantity: 1.0m);
        var result = await _service.ExecuteBatchStockEffectsAsync(new ExecuteBatchStockEffectsCommand(
            BatchId: batchId, ActualQuantity: 1.0m, SourceLocationId: locationId));

        var consumption = result.Consumptions.Should().ContainSingle(c => c.StockItemId == eggId).Subject;
        consumption.NativeQuantity.Should().Be(2.0m);
        consumption.NativeUnitCode.Should().Be("adet");
        consumption.Quantity.Should().Be(2.0000m);
        consumption.UnitCode.Should().Be("piece");
    }
}
