using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Recipes.Units.Tests;

public sealed class RecipeTestDb : PgTestDatabase
{
    public RecipeTestDb() : base("alkaros_recipe_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var upSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "054-unit-conversions.up.sql");
        var upSql = await File.ReadAllTextAsync(upSqlPath);
        await RunAsync(DataSource, upSql);
    }

    public async Task RollbackSqlAsync()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "054-unit-conversions.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplySqlAsync()
    {
        await ApplySqlAsync();
    }
}

public sealed class UnitConversionDatabaseTests : IClassFixture<RecipeTestDb>
{
    private readonly RecipeTestDb _db;
    private readonly PostgresUnitConversionRepository _repo;

    public UnitConversionDatabaseTests(RecipeTestDb db)
    {
        _db = db;
        _repo = new PostgresUnitConversionRepository(db.DataSource);
    }

    [Fact]
    public async Task AddAndRetrieveActiveUnitConversionPersistsAndLoadsCorrectly()
    {
        var id = Guid.NewGuid();
        var conversion = new UnitConversion(id, "can", "ml", 330.0m);

        await _repo.AddConversionAsync(conversion);

        var retrieved = await _repo.FindConversionAsync("can", "ml");
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(id);
        retrieved.FromUnitCode.Should().Be("can");
        retrieved.ToUnitCode.Should().Be("ml");
        retrieved.Factor.Should().Be(330.0m);
        retrieved.Active.Should().BeTrue();
    }

    [Fact]
    public async Task AddDuplicateFromAndToUnitUpsertsFactor()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await _repo.AddConversionAsync(new UnitConversion(id1, "bottle", "cl", 70.0m));

        // Update with new factor
        await _repo.AddConversionAsync(new UnitConversion(id2, "bottle", "cl", 75.0m));

        var retrieved = await _repo.FindConversionAsync("bottle", "cl");
        retrieved.Should().NotBeNull();
        retrieved!.Factor.Should().Be(75.0m);
    }

    [Fact]
    public async Task GetActiveConversionsReturnsAllActiveConversions()
    {
        await _repo.AddConversionAsync(new UnitConversion(Guid.NewGuid(), "keg", "l", 50.0m, active: true));
        await _repo.AddConversionAsync(new UnitConversion(Guid.NewGuid(), "pinch", "g", 0.5m, active: true));

        var list = await _repo.GetActiveConversionsAsync();
        list.Should().Contain(c => c.FromUnitCode == "keg" && c.ToUnitCode == "l");
        list.Should().Contain(c => c.FromUnitCode == "pinch" && c.ToUnitCode == "g");
    }

}

public sealed class RecipeMigrationTestDb : PgTestDatabase
{
    public RecipeMigrationTestDb() : base("alkaros_recipe_mig_") { }

    protected override async Task ApplySqlAsync()
    {
        var upSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "054-unit-conversions.up.sql");
        var upSql = await File.ReadAllTextAsync(upSqlPath);
        await RunAsync(DataSource, upSql);
    }

    public async Task RollbackSqlAsync()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "054-unit-conversions.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplySqlAsync()
    {
        await ApplySqlAsync();
    }
}

public sealed class UnitConversionMigrationTests : IClassFixture<RecipeMigrationTestDb>
{
    private readonly RecipeMigrationTestDb _db;

    public UnitConversionMigrationTests(RecipeMigrationTestDb db)
    {
        _db = db;
    }

    [Fact]
    public async Task MigrationRollbackAndReapplyWorksCleanly()
    {
        const string checkSql = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'recipe' AND table_name = 'unit_conversions');";

        // Down migration
        await _db.RollbackSqlAsync();

        // Verify table is dropped
        await using (var cmd = _db.DataSource.CreateCommand(checkSql))
        {
            var exists = (bool)(await cmd.ExecuteScalarAsync())!;
            exists.Should().BeFalse();
        }

        // Reapply up migration
        await _db.ReapplySqlAsync();

        // Verify table exists again
        await using (var cmd2 = _db.DataSource.CreateCommand(checkSql))
        {
            var existsAfterReapply = (bool)(await cmd2.ExecuteScalarAsync())!;
            existsAfterReapply.Should().BeTrue();
        }
    }
}

