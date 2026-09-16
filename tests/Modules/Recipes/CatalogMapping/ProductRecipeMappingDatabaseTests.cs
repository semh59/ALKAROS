using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Recipes.CatalogMapping.Tests;

public sealed class ProductRecipeMappingTestDb : PgTestDatabase
{
    public ProductRecipeMappingTestDb() : base("alkaros_rcp_catmap_test_") { }

    protected override async Task ApplySqlAsync()
    {
        foreach (var file in new[] { "057-unit-conversions.up.sql", "058-recipe-versions.up.sql", "115-product-recipe-mappings.up.sql" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file);
            var sql = await File.ReadAllTextAsync(path);
            await RunAsync(DataSource, sql);
        }
    }

    public async Task<Guid> InsertRecipeAsync(string code, string name)
    {
        var id = Guid.NewGuid();
        await using var cmd = DataSource.CreateCommand(
            "INSERT INTO recipe.recipes (id, code, name) VALUES ($1, $2, $3);");
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(code);
        cmd.Parameters.AddWithValue(name);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    public async Task RollbackMigration115Async()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "115-product-recipe-mappings.down.sql");
        await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    public async Task ReapplyMigration115Async()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "115-product-recipe-mappings.up.sql");
        await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }
}

public sealed class ProductRecipeMappingDatabaseTests : IClassFixture<ProductRecipeMappingTestDb>
{
    private readonly ProductRecipeMappingTestDb _db;
    private readonly PostgresProductRecipeMappingRepository _repo;

    public ProductRecipeMappingDatabaseTests(ProductRecipeMappingTestDb db)
    {
        _db = db;
        _repo = new PostgresProductRecipeMappingRepository(db.DataSource);
    }

    [Fact]
    public async Task AddOrUpdateThenGetByProductIdRoundTripsCorrectly()
    {
        var recipeId = await _db.InsertRecipeAsync("RCP-" + Guid.NewGuid().ToString("N")[..8], "Test Recipe");
        var productId = Guid.NewGuid();
        var mapping = new ProductRecipeMapping(productId, recipeId, isActive: true, notes: "note");

        await _repo.AddOrUpdateAsync(mapping);

        var loaded = await _repo.GetByProductIdAsync(productId);
        loaded.Should().NotBeNull();
        loaded!.RecipeId.Should().Be(recipeId);
        loaded.IsActive.Should().BeTrue();
        loaded.Notes.Should().Be("note");
    }

    [Fact]
    public async Task AddOrUpdateOnExistingProductUpsertsRatherThanDuplicating()
    {
        var recipeA = await _db.InsertRecipeAsync("RCP-" + Guid.NewGuid().ToString("N")[..8], "Recipe A");
        var recipeB = await _db.InsertRecipeAsync("RCP-" + Guid.NewGuid().ToString("N")[..8], "Recipe B");
        var productId = Guid.NewGuid();

        await _repo.AddOrUpdateAsync(new ProductRecipeMapping(productId, recipeA));
        await _repo.AddOrUpdateAsync(new ProductRecipeMapping(productId, recipeB, isActive: false, notes: "switched"));

        var loaded = await _repo.GetByProductIdAsync(productId);
        loaded!.RecipeId.Should().Be(recipeB);
        loaded.IsActive.Should().BeFalse();
        loaded.Notes.Should().Be("switched");
    }

    [Fact]
    public async Task GetByProductIdsReturnsOnlyMatchingRows()
    {
        var recipeId = await _db.InsertRecipeAsync("RCP-" + Guid.NewGuid().ToString("N")[..8], "Shared Recipe");
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var productUnrelated = Guid.NewGuid();

        await _repo.AddOrUpdateAsync(new ProductRecipeMapping(productA, recipeId));
        await _repo.AddOrUpdateAsync(new ProductRecipeMapping(productB, recipeId));

        var loaded = await _repo.GetByProductIdsAsync([productA, productB, productUnrelated]);
        loaded.Should().HaveCount(2);
        loaded.Select(m => m.ProductId).Should().BeEquivalentTo([productA, productB]);
    }

    [Fact]
    public async Task RemoveDeletesTheMapping()
    {
        var recipeId = await _db.InsertRecipeAsync("RCP-" + Guid.NewGuid().ToString("N")[..8], "Doomed Recipe");
        var productId = Guid.NewGuid();
        await _repo.AddOrUpdateAsync(new ProductRecipeMapping(productId, recipeId));

        await _repo.RemoveAsync(productId);

        (await _repo.GetByProductIdAsync(productId)).Should().BeNull();
    }

    [Fact]
    public async Task MappingToNonExistentRecipeThrowsForeignKeyViolation()
    {
        var productId = Guid.NewGuid();
        var mapping = new ProductRecipeMapping(productId, Guid.NewGuid());

        var act = () => _repo.AddOrUpdateAsync(mapping);
        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23503");
    }

    [Fact]
    public async Task Migration115RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration115Async();
        await _db.ReapplyMigration115Async();

        await using var cmd = _db.DataSource.CreateCommand("SELECT count(*) FROM recipe.product_recipe_mappings;");
        var count = await cmd.ExecuteScalarAsync();
        count.Should().NotBeNull();
    }
}
