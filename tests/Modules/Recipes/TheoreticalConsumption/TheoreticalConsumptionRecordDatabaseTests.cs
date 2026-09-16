using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Recipes.TheoreticalConsumption.Tests;

public sealed class TheoreticalConsumptionTestDb : PgTestDatabase
{
    public TheoreticalConsumptionTestDb() : base("alkaros_rcp004_") { }

    protected override async Task ApplySqlAsync()
    {
        foreach (var file in new[]
        {
            "057-unit-conversions.up.sql",
            "058-recipe-versions.up.sql",
            "116-theoretical-consumption-records.up.sql",
        })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file);
            var sql = await File.ReadAllTextAsync(path);
            await RunAsync(DataSource, sql);
        }
    }

    public async Task<(Guid RecipeId, Guid VersionId)> InsertRecipeWithVersionAsync(string code)
    {
        var recipeId = Guid.NewGuid();
        await using (var cmd = DataSource.CreateCommand(
            "INSERT INTO recipe.recipes (id, code, name) VALUES ($1, $2, $3);"))
        {
            cmd.Parameters.AddWithValue(recipeId);
            cmd.Parameters.AddWithValue(code);
            cmd.Parameters.AddWithValue("Recipe " + code);
            await cmd.ExecuteNonQueryAsync();
        }

        var versionId = Guid.NewGuid();
        await using (var cmd = DataSource.CreateCommand(
            """
            INSERT INTO recipe.recipe_versions
                (id, recipe_id, version_number, status, yield_quantity, yield_unit_code)
            VALUES ($1, $2, 1, 'Active', 1, 'piece');
            """))
        {
            cmd.Parameters.AddWithValue(versionId);
            cmd.Parameters.AddWithValue(recipeId);
            await cmd.ExecuteNonQueryAsync();
        }

        return (recipeId, versionId);
    }

    public async Task RollbackMigration116Async()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "116-theoretical-consumption-records.down.sql");
        await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    public async Task ReapplyMigration116Async()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "116-theoretical-consumption-records.up.sql");
        await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }
}

public sealed class TheoreticalConsumptionRecordDatabaseTests : IClassFixture<TheoreticalConsumptionTestDb>
{
    private readonly TheoreticalConsumptionTestDb _db;
    private readonly PostgresTheoreticalConsumptionRecordRepository _repo;

    public TheoreticalConsumptionRecordDatabaseTests(TheoreticalConsumptionTestDb db)
    {
        _db = db;
        _repo = new PostgresTheoreticalConsumptionRecordRepository(db.DataSource);
    }

    [Fact]
    public async Task AppendPersistsARecordVisibleToTotals()
    {
        var (recipeId, versionId) = await _db.InsertRecipeWithVersionAsync("RCP-" + Guid.NewGuid().ToString("N")[..8]);
        var stockItemId = Guid.NewGuid();
        var record = new TheoreticalConsumptionRecord(
            id: Guid.NewGuid(),
            orderItemId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            recipeId: recipeId,
            recipeVersionId: versionId,
            stockItemId: stockItemId,
            quantity: 2.5m,
            unitCode: "kg");

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repo.AppendAsync(record, connection, transaction);
            await transaction.CommitAsync();
        }

        var totals = await _repo.GetTotalsByStockItemAsync(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
        totals.Should().ContainSingle(t => t.StockItemId == stockItemId && t.TotalQuantity == 2.5m && t.UnitCode == "kg");
    }

    [Fact]
    public async Task TotalsSumMultipleRecordsForTheSameStockItem()
    {
        var (recipeId, versionId) = await _db.InsertRecipeWithVersionAsync("RCP-" + Guid.NewGuid().ToString("N")[..8]);
        var stockItemId = Guid.NewGuid();

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repo.AppendAsync(
                new TheoreticalConsumptionRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), recipeId, versionId, stockItemId, 1m, "kg"),
                connection, transaction);
            await _repo.AppendAsync(
                new TheoreticalConsumptionRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), recipeId, versionId, stockItemId, 1.5m, "kg"),
                connection, transaction);
            await transaction.CommitAsync();
        }

        var totals = await _repo.GetTotalsByStockItemAsync(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
        totals.Should().ContainSingle(t => t.StockItemId == stockItemId);
        totals.Single(t => t.StockItemId == stockItemId).TotalQuantity.Should().Be(2.5m);
    }

    [Fact]
    public async Task RecordsOutsideTheRequestedWindowAreExcluded()
    {
        var (recipeId, versionId) = await _db.InsertRecipeWithVersionAsync("RCP-" + Guid.NewGuid().ToString("N")[..8]);
        var stockItemId = Guid.NewGuid();

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repo.AppendAsync(
                new TheoreticalConsumptionRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), recipeId, versionId, stockItemId, 3m, "kg"),
                connection, transaction);
            await transaction.CommitAsync();
        }

        var totals = await _repo.GetTotalsByStockItemAsync(
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1));
        totals.Should().NotContain(t => t.StockItemId == stockItemId);
    }

    [Fact]
    public async Task RecordsAreImmutableOnceAppended()
    {
        var (recipeId, versionId) = await _db.InsertRecipeWithVersionAsync("RCP-" + Guid.NewGuid().ToString("N")[..8]);
        var id = Guid.NewGuid();

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repo.AppendAsync(
                new TheoreticalConsumptionRecord(id, Guid.NewGuid(), Guid.NewGuid(), recipeId, versionId, Guid.NewGuid(), 1m, "kg"),
                connection, transaction);
            await transaction.CommitAsync();
        }

        await using var updateCmd = _db.DataSource.CreateCommand(
            "UPDATE recipe.theoretical_consumption_records SET quantity = 99 WHERE id = $1;");
        updateCmd.Parameters.AddWithValue(id);
        var act = () => updateCmd.ExecuteNonQueryAsync();
        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact]
    public async Task Migration116RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration116Async();
        await _db.ReapplyMigration116Async();

        await using var cmd = _db.DataSource.CreateCommand("SELECT count(*) FROM recipe.theoretical_consumption_records;");
        var count = await cmd.ExecuteScalarAsync();
        count.Should().NotBeNull();
    }
}
