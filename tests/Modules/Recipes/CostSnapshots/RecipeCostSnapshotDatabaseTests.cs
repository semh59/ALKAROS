using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ALKAROS.Recipes.Units;
using ALKAROS.Recipes.Versioning;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Recipes.CostSnapshots.Tests;

public sealed class CostSnapshotsTestDb : PgTestDatabase
{
    public CostSnapshotsTestDb() : base("alkaros_rcp_cost_test_") { }

    protected override async Task ApplySqlAsync()
    {
        // 054: unit conversions
        var sql054 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "054-unit-conversions.up.sql"));
        await RunAsync(DataSource, sql054);

        // 055: recipe versions
        var sql055 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "055-recipe-versions.up.sql"));
        await RunAsync(DataSource, sql055);

        // 056: stock master
        var sql056 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "056-stock-master.up.sql"));
        await RunAsync(DataSource, sql056);

        // 057: stock movements
        var sql057 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "057-stock-movements.up.sql"));
        await RunAsync(DataSource, sql057);

        // 065: suppliers
        var sql065 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "065-suppliers.up.sql"));
        await RunAsync(DataSource, sql065);

        // 066: purchase orders and goods receipts
        var sql066 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "066-purchase-orders-receipts.up.sql"));
        await RunAsync(DataSource, sql066);

        // 067: recipe cost snapshots
        var sql067 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-recipe-cost-snapshots.up.sql"));
        await RunAsync(DataSource, sql067);
    }

    public async Task RollbackMigration067Async()
    {
        var downSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-recipe-cost-snapshots.down.sql"));
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration067Async()
    {
        var sql067 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-recipe-cost-snapshots.up.sql"));
        await RunAsync(DataSource, sql067);
    }
}

public sealed class RecipeCostSnapshotDatabaseTests : IClassFixture<CostSnapshotsTestDb>
{
    private readonly CostSnapshotsTestDb _db;
    private readonly PostgresRecipeCostSnapshotRepository _snapshotRepo;
    private readonly PostgresRecipeVersionRepository _versionRepo;
    private readonly PostgresRecipeRepository _recipeRepo;
    private readonly PostgresStockCostResolver _costResolver;
    private readonly UnitConverter _unitConverter;
    private readonly RecipeCostSnapshotService _service;

    public RecipeCostSnapshotDatabaseTests(CostSnapshotsTestDb db)
    {
        _db = db;
        _snapshotRepo = new PostgresRecipeCostSnapshotRepository(db.DataSource);
        _versionRepo = new PostgresRecipeVersionRepository(db.DataSource);
        _recipeRepo = new PostgresRecipeRepository(db.DataSource);
        _costResolver = new PostgresStockCostResolver(db.DataSource);
        _unitConverter = new UnitConverter();
        _service = new RecipeCostSnapshotService(_snapshotRepo, _versionRepo, _costResolver, _unitConverter);
    }

    private async Task<(Guid recipeId, Guid versionId, Guid stockItemId)> SeedRecipeAndItemAsync()
    {
        var recipeId = Guid.NewGuid();
        var recipeCode = "RCP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var recipe = Recipe.Create(recipeCode, "Tomato Soup");
        await _recipeRepo.AddAsync(recipe);

        var stockItemId = Guid.NewGuid();
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string itemSql = @"
INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
VALUES ($1, $2, 'Fresh Tomatoes', 'RawMaterial', 'kg', true, 1);";
        await using var itemCmd = new NpgsqlCommand(itemSql, conn);
        itemCmd.Parameters.AddWithValue(stockItemId);
        itemCmd.Parameters.AddWithValue("SKU-" + Guid.NewGuid().ToString("N")[..8]);
        await itemCmd.ExecuteNonQueryAsync();

        var version = RecipeVersion.CreateDraft(
            recipe.Id,
            versionNumber: 1,
            yieldQuantity: 4m,
            yieldUnitCode: "portion");
        version.AddIngredient(stockItemId, 200m, "g", lossPercentage: 5.00m);

        await _versionRepo.AddAsync(version);
        return (recipe.Id, version.Id, stockItemId);
    }

    [Fact]
    public async Task CreateAndRetrieveSnapshotPersistsCorrectly()
    {
        var (_, versionId, stockItemId) = await SeedRecipeAndItemAsync();
        var date = new DateOnly(2026, 8, 10);

        var snapshot = await _service.CreateSnapshotAsync(new CreateSnapshotCommand(
            RecipeVersionId: versionId,
            CostBasisDate: date,
            StockItemUnits: new Dictionary<Guid, string> { [stockItemId] = "kg" },
            FallbackItemCosts: new Dictionary<Guid, decimal> { [stockItemId] = 35.00m }));

        snapshot.Should().NotBeNull();
        snapshot.CalculatedCost.Should().Be(7.35m);
        snapshot.Items.Should().HaveCount(1);
        snapshot.Items[0].EffectiveNativeQuantity.Should().Be(210m);
        snapshot.Items[0].StockQuantity.Should().Be(0.21m);
        snapshot.Items[0].UnitCost.Should().Be(35.00m);
        snapshot.Items[0].LineCost.Should().Be(7.35m);

        var loaded = await _snapshotRepo.GetByIdAsync(snapshot.Id);
        loaded.Should().NotBeNull();
        loaded!.CalculatedCost.Should().Be(7.35m);
        loaded.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task DuplicateSnapshotForSameVersionAndDateThrowsDuplicateCostSnapshotException()
    {
        var (_, versionId, stockItemId) = await SeedRecipeAndItemAsync();
        var date = new DateOnly(2026, 8, 15);

        var cmd = new CreateSnapshotCommand(
            RecipeVersionId: versionId,
            CostBasisDate: date,
            StockItemUnits: new Dictionary<Guid, string> { [stockItemId] = "kg" },
            FallbackItemCosts: new Dictionary<Guid, decimal> { [stockItemId] = 20.00m });

        await _service.CreateSnapshotAsync(cmd);

        var act = async () => await _service.CreateSnapshotAsync(cmd);
        await act.Should().ThrowAsync<DuplicateCostSnapshotException>();
    }

    [Fact]
    public async Task EffectiveSnapshotReturnsLastSnapshotOnOrBeforeRequestedDate()
    {
        var (_, versionId, stockItemId) = await SeedRecipeAndItemAsync();

        // Snapshot 1: on 2026-08-01 @ 30.00 TRY/kg -> cost 6.30
        await _service.CreateSnapshotAsync(new CreateSnapshotCommand(
            RecipeVersionId: versionId,
            CostBasisDate: new DateOnly(2026, 8, 1),
            StockItemUnits: new Dictionary<Guid, string> { [stockItemId] = "kg" },
            FallbackItemCosts: new Dictionary<Guid, decimal> { [stockItemId] = 30.00m }));

        // Snapshot 2: on 2026-08-15 @ 40.00 TRY/kg -> cost 8.40
        await _service.CreateSnapshotAsync(new CreateSnapshotCommand(
            RecipeVersionId: versionId,
            CostBasisDate: new DateOnly(2026, 8, 15),
            StockItemUnits: new Dictionary<Guid, string> { [stockItemId] = "kg" },
            FallbackItemCosts: new Dictionary<Guid, decimal> { [stockItemId] = 40.00m }));

        // Query on 2026-08-10 -> should return Snapshot 1 (cost 6.30)
        var snapAt10 = await _service.GetRequiredEffectiveSnapshotAsync(versionId, new DateOnly(2026, 8, 10));
        snapAt10.CostBasisDate.Should().Be(new DateOnly(2026, 8, 1));
        snapAt10.CalculatedCost.Should().Be(6.30m);

        // Query on 2026-08-20 -> should return Snapshot 2 (cost 8.40)
        var snapAt20 = await _service.GetRequiredEffectiveSnapshotAsync(versionId, new DateOnly(2026, 8, 20));
        snapAt20.CostBasisDate.Should().Be(new DateOnly(2026, 8, 15));
        snapAt20.CalculatedCost.Should().Be(8.40m);

        // Query before first snapshot (2026-07-25) -> should throw RecipeCostSnapshotNotFoundException
        var actBefore = async () => await _service.GetRequiredEffectiveSnapshotAsync(versionId, new DateOnly(2026, 7, 25));
        await actBefore.Should().ThrowAsync<RecipeCostSnapshotNotFoundException>();
    }

    [Fact]
    public async Task MissingCostBasisWithoutFallbackThrowsMissingCostBasisException()
    {
        var (_, versionId, _) = await SeedRecipeAndItemAsync();

        var act = async () => await _service.CreateSnapshotAsync(new CreateSnapshotCommand(
            RecipeVersionId: versionId,
            CostBasisDate: new DateOnly(2026, 8, 1)));

        await act.Should().ThrowAsync<MissingCostBasisException>();
    }

    [Fact]
    public async Task MovingAverageCostDerivedFromPurchasingGoodsReceiptsCorrectlyCalculatesSnapshot()
    {
        var (_, versionId, stockItemId) = await SeedRecipeAndItemAsync();

        // Seed Supplier and Location
        var supplierId = Guid.NewGuid();
        var locId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var poLineId = Guid.NewGuid();

        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string suppSql = @"
INSERT INTO purchasing.suppliers (supplier_id, code, name, active)
VALUES ($1, $2, 'Main Farm', true);";
        await using var sCmd = new NpgsqlCommand(suppSql, conn);
        sCmd.Parameters.AddWithValue(supplierId);
        sCmd.Parameters.AddWithValue("SUP-" + Guid.NewGuid().ToString("N")[..8]);
        await sCmd.ExecuteNonQueryAsync();

        const string locSql = @"
INSERT INTO inventory.stock_locations (id, code, name, location_type, is_active, row_version)
VALUES ($1, $2, 'Main Warehouse', 'Warehouse', true, 1);";
        await using var lCmd = new NpgsqlCommand(locSql, conn);
        lCmd.Parameters.AddWithValue(locId);
        lCmd.Parameters.AddWithValue("LOC-" + Guid.NewGuid().ToString("N")[..8]);
        await lCmd.ExecuteNonQueryAsync();

        const string poSql = @"
INSERT INTO purchasing.purchase_orders (order_id, order_number, supplier_id, status, destination_location_id, total_amount, currency)
VALUES ($1, $2, $3, 'Submitted', $4, 700.00, 'TRY');";
        await using var poCmd = new NpgsqlCommand(poSql, conn);
        poCmd.Parameters.AddWithValue(poId);
        poCmd.Parameters.AddWithValue("PO-" + Guid.NewGuid().ToString("N")[..8]);
        poCmd.Parameters.AddWithValue(supplierId);
        poCmd.Parameters.AddWithValue(locId);
        await poCmd.ExecuteNonQueryAsync();

        const string poLineSql = @"
INSERT INTO purchasing.purchase_order_lines (line_id, order_id, stock_item_id, ordered_quantity, received_quantity, unit_code, unit_price, total_price, status)
VALUES ($1, $2, $3, 20.00, 20.00, 'kg', 35.00, 700.00, 'Completed');";
        await using var poLineCmd = new NpgsqlCommand(poLineSql, conn);
        poLineCmd.Parameters.AddWithValue(poLineId);
        poLineCmd.Parameters.AddWithValue(poId);
        poLineCmd.Parameters.AddWithValue(stockItemId);
        await poLineCmd.ExecuteNonQueryAsync();

        // Receipt 1: 2026-08-05 -> 10 kg @ 30.00 TRY/kg
        var gr1Id = Guid.NewGuid();
        const string gr1Sql = @"
INSERT INTO purchasing.goods_receipts (receipt_id, receipt_number, order_id, supplier_id, destination_location_id, received_at, received_by)
VALUES ($1, $2, $3, $4, $5, '2026-08-05T10:00:00Z', 'Clerk');";
        await using var gr1Cmd = new NpgsqlCommand(gr1Sql, conn);
        gr1Cmd.Parameters.AddWithValue(gr1Id);
        gr1Cmd.Parameters.AddWithValue("GR-" + Guid.NewGuid().ToString("N")[..8]);
        gr1Cmd.Parameters.AddWithValue(poId);
        gr1Cmd.Parameters.AddWithValue(supplierId);
        gr1Cmd.Parameters.AddWithValue(locId);
        await gr1Cmd.ExecuteNonQueryAsync();

        const string gr1ItemSql = @"
INSERT INTO purchasing.goods_receipt_items (item_id, receipt_id, order_line_id, stock_item_id, delivered_quantity, accepted_quantity, unit_code, unit_price)
VALUES ($1, $2, $3, $4, 10.00, 10.00, 'kg', 30.00);";
        await using var gr1ItemCmd = new NpgsqlCommand(gr1ItemSql, conn);
        gr1ItemCmd.Parameters.AddWithValue(Guid.NewGuid());
        gr1ItemCmd.Parameters.AddWithValue(gr1Id);
        gr1ItemCmd.Parameters.AddWithValue(poLineId);
        gr1ItemCmd.Parameters.AddWithValue(stockItemId);
        await gr1ItemCmd.ExecuteNonQueryAsync();

        // Receipt 2: 2026-08-08 -> 10 kg @ 40.00 TRY/kg
        var gr2Id = Guid.NewGuid();
        const string gr2Sql = @"
INSERT INTO purchasing.goods_receipts (receipt_id, receipt_number, order_id, supplier_id, destination_location_id, received_at, received_by)
VALUES ($1, $2, $3, $4, $5, '2026-08-08T10:00:00Z', 'Clerk');";
        await using var gr2Cmd = new NpgsqlCommand(gr2Sql, conn);
        gr2Cmd.Parameters.AddWithValue(gr2Id);
        gr2Cmd.Parameters.AddWithValue("GR-" + Guid.NewGuid().ToString("N")[..8]);
        gr2Cmd.Parameters.AddWithValue(poId);
        gr2Cmd.Parameters.AddWithValue(supplierId);
        gr2Cmd.Parameters.AddWithValue(locId);
        await gr2Cmd.ExecuteNonQueryAsync();

        const string gr2ItemSql = @"
INSERT INTO purchasing.goods_receipt_items (item_id, receipt_id, order_line_id, stock_item_id, delivered_quantity, accepted_quantity, unit_code, unit_price)
VALUES ($1, $2, $3, $4, 10.00, 10.00, 'kg', 40.00);";
        await using var gr2ItemCmd = new NpgsqlCommand(gr2ItemSql, conn);
        gr2ItemCmd.Parameters.AddWithValue(Guid.NewGuid());
        gr2ItemCmd.Parameters.AddWithValue(gr2Id);
        gr2ItemCmd.Parameters.AddWithValue(poLineId);
        gr2ItemCmd.Parameters.AddWithValue(stockItemId);
        await gr2ItemCmd.ExecuteNonQueryAsync();

        // Calculate snapshot on 2026-08-10
        // Moving average = (10*30 + 10*40) / 20 = 35.00 TRY/kg
        // Recipe ingredient: 200g with 5% waste = 210g = 0.21 kg
        // Line cost = 0.21 * 35.00 = 7.35 TRY
        var snapshot = await _service.CreateSnapshotAsync(new CreateSnapshotCommand(
            RecipeVersionId: versionId,
            CostBasisDate: new DateOnly(2026, 8, 10),
            StockItemUnits: new Dictionary<Guid, string> { [stockItemId] = "kg" }));

        snapshot.Should().NotBeNull();
        snapshot.CalculatedCost.Should().Be(7.35m);
        snapshot.Items[0].UnitCost.Should().Be(35.00m);
        snapshot.Items[0].LineCost.Should().Be(7.35m);

        // Later receipt on 2026-08-12 at 100.00 TRY/kg
        var gr3Id = Guid.NewGuid();
        const string gr3Sql = @"
INSERT INTO purchasing.goods_receipts (receipt_id, receipt_number, order_id, supplier_id, destination_location_id, received_at, received_by)
VALUES ($1, $2, $3, $4, $5, '2026-08-12T10:00:00Z', 'Clerk');";
        await using var gr3Cmd = new NpgsqlCommand(gr3Sql, conn);
        gr3Cmd.Parameters.AddWithValue(gr3Id);
        gr3Cmd.Parameters.AddWithValue("GR-" + Guid.NewGuid().ToString("N")[..8]);
        gr3Cmd.Parameters.AddWithValue(poId);
        gr3Cmd.Parameters.AddWithValue(supplierId);
        gr3Cmd.Parameters.AddWithValue(locId);
        await gr3Cmd.ExecuteNonQueryAsync();

        const string gr3ItemSql = @"
INSERT INTO purchasing.goods_receipt_items (item_id, receipt_id, order_line_id, stock_item_id, delivered_quantity, accepted_quantity, unit_code, unit_price)
VALUES ($1, $2, $3, $4, 10.00, 10.00, 'kg', 100.00);";
        await using var gr3ItemCmd = new NpgsqlCommand(gr3ItemSql, conn);
        gr3ItemCmd.Parameters.AddWithValue(Guid.NewGuid());
        gr3ItemCmd.Parameters.AddWithValue(gr3Id);
        gr3ItemCmd.Parameters.AddWithValue(poLineId);
        gr3ItemCmd.Parameters.AddWithValue(stockItemId);
        await gr3ItemCmd.ExecuteNonQueryAsync();

        // Historical snapshot on 2026-08-10 remains unchanged (7.35 TRY)
        var historicalSnapshot = await _service.GetRequiredEffectiveSnapshotAsync(versionId, new DateOnly(2026, 8, 10));
        historicalSnapshot.CalculatedCost.Should().Be(7.35m);
        historicalSnapshot.Items[0].UnitCost.Should().Be(35.00m);
    }

    [Fact]
    public async Task Migration067RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration067Async();
        await _db.ReapplyMigration067Async();

        var (_, versionId, stockItemId) = await SeedRecipeAndItemAsync();
        var snapshot = await _service.CreateSnapshotAsync(new CreateSnapshotCommand(
            RecipeVersionId: versionId,
            CostBasisDate: new DateOnly(2026, 8, 1),
            StockItemUnits: new Dictionary<Guid, string> { [stockItemId] = "kg" },
            FallbackItemCosts: new Dictionary<Guid, decimal> { [stockItemId] = 50.00m }));

        snapshot.Should().NotBeNull();
    }
}
