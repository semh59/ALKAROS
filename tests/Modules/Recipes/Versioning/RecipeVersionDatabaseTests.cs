using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Recipes.Versioning.Tests;

public sealed class RecipeVersioningTestDb : PgTestDatabase
{
    public RecipeVersioningTestDb() : base("alkaros_rcp_version_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var migration057 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "057-unit-conversions.up.sql");
        var sql057 = await File.ReadAllTextAsync(migration057);
        await RunAsync(DataSource, sql057);

        var migration058 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-recipe-versions.up.sql");
        var sql058 = await File.ReadAllTextAsync(migration058);
        await RunAsync(DataSource, sql058);
    }

    public async Task RollbackMigration058Async()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-recipe-versions.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration058Async()
    {
        var migration058 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-recipe-versions.up.sql");
        var sql058 = await File.ReadAllTextAsync(migration058);
        await RunAsync(DataSource, sql058);
    }
}

public sealed class RecipeVersionDatabaseTests : IClassFixture<RecipeVersioningTestDb>
{
    private readonly RecipeVersioningTestDb _db;
    private readonly PostgresRecipeRepository _recipeRepo;
    private readonly PostgresRecipeVersionRepository _versionRepo;
    private readonly RecipeLifecycleService _lifecycleService;

    public RecipeVersionDatabaseTests(RecipeVersioningTestDb db)
    {
        _db = db;
        _recipeRepo = new PostgresRecipeRepository(db.DataSource);
        _versionRepo = new PostgresRecipeVersionRepository(db.DataSource);
        _lifecycleService = new RecipeLifecycleService(_recipeRepo, _versionRepo, new UnitConverter());
    }

    [Fact]
    public async Task AddAndRetrieveRecipeWithVersionsPersistsAndLoadsCorrectly()
    {
        var code = "RECIPE-" + Guid.NewGuid().ToString("N")[..8];
        var recipe = await _lifecycleService.CreateRecipeAsync(code, "Margherita Pizza", "Classic tomato and mozzarella");

        var draft = await _lifecycleService.CreateInitialDraftVersionAsync(
            recipeId: recipe.Id,
            yieldQuantity: 1m,
            yieldUnitCode: "piece",
            preparationMinutes: 15,
            instructions: "Bake at 250C for 10 min");

        var flourId = Guid.NewGuid();
        var cheeseId = Guid.NewGuid();

        await _lifecycleService.AddIngredientToDraftAsync(draft.Id, flourId, 250m, "g", lossPercentage: 2m, sortOrder: 1, notes: "Type 00 flour");
        await _lifecycleService.AddIngredientToDraftAsync(draft.Id, cheeseId, 150m, "g", lossPercentage: 0m, sortOrder: 2, notes: "Fresh mozzarella");

        var loadedVersion = await _versionRepo.GetByIdAsync(draft.Id);
        loadedVersion.Should().NotBeNull();
        loadedVersion!.RecipeId.Should().Be(recipe.Id);
        loadedVersion.VersionNumber.Should().Be(1);
        loadedVersion.Status.Should().Be(RecipeVersionStatus.Draft);
        loadedVersion.YieldQuantity.Should().Be(1m);
        loadedVersion.YieldUnitCode.Should().Be("piece");
        loadedVersion.PreparationMinutes.Should().Be(15);
        loadedVersion.Ingredients.Should().HaveCount(2);

        var firstIng = loadedVersion.Ingredients.First(i => i.IngredientItemId == flourId);
        firstIng.Quantity.Should().Be(250m);
        firstIng.UnitCode.Should().Be("g");
        firstIng.LossPercentage.Should().Be(2m);
    }

    [Fact]
    public async Task UpdateDraftVersionPersistsIngredientChanges()
    {
        var code = "RECIPE-" + Guid.NewGuid().ToString("N")[..8];
        var recipe = await _lifecycleService.CreateRecipeAsync(code, "Pasta Bolognese");
        var draft = await _lifecycleService.CreateInitialDraftVersionAsync(recipe.Id, 2m, "portion");

        var meatId = Guid.NewGuid();
        var onionId = Guid.NewGuid();

        await _lifecycleService.AddIngredientToDraftAsync(draft.Id, meatId, 300m, "g");
        await _lifecycleService.AddIngredientToDraftAsync(draft.Id, onionId, 50m, "g");

        // Remove onion, update meat
        await _lifecycleService.RemoveIngredientFromDraftAsync(draft.Id, onionId);

        var loaded = await _versionRepo.GetByIdAsync(draft.Id);
        loaded!.Ingredients.Should().HaveCount(1);
        loaded.Ingredients[0].IngredientItemId.Should().Be(meatId);
    }

    [Fact]
    public async Task OptimisticConcurrencyThrowsWhenRowVersionMismatches()
    {
        var code = "RECIPE-" + Guid.NewGuid().ToString("N")[..8];
        var recipe = await _lifecycleService.CreateRecipeAsync(code, "Risotto");
        var draft = await _lifecycleService.CreateInitialDraftVersionAsync(recipe.Id, 2m, "portion");

        var riceId = Guid.NewGuid();
        await _lifecycleService.AddIngredientToDraftAsync(draft.Id, riceId, 200m, "g");

        var staleCopy = await _versionRepo.GetByIdAsync(draft.Id);

        // Update once to advance row_version
        var mushroomId = Guid.NewGuid();
        await _lifecycleService.AddIngredientToDraftAsync(draft.Id, mushroomId, 100m, "g");

        // Attempt update with stale row_version
        var act = () => _versionRepo.UpdateAsync(staleCopy!);
        await act.Should().ThrowAsync<RecipeVersionConflictException>()
            .WithMessage("*Optimistic concurrency violation*");
    }

    [Fact]
    public async Task ActivateVersionTransactionallyArchivesPreviousAndActivatesTarget()
    {
        var code = "RECIPE-" + Guid.NewGuid().ToString("N")[..8];
        var recipe = await _lifecycleService.CreateRecipeAsync(code, "Caesar Salad");
        var v1Draft = await _lifecycleService.CreateInitialDraftVersionAsync(recipe.Id, 1m, "portion");
        await _lifecycleService.AddIngredientToDraftAsync(v1Draft.Id, Guid.NewGuid(), 100m, "g");

        // Activate V1
        var v1ActivatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await _lifecycleService.ActivateVersionAsync(recipe.Id, 1, v1ActivatedAt);

        var v1Active = await _versionRepo.GetActiveVersionAsync(recipe.Id);
        v1Active.Should().NotBeNull();
        v1Active!.VersionNumber.Should().Be(1);
        v1Active.Status.Should().Be(RecipeVersionStatus.Active);
        v1Active.IsLocked.Should().BeTrue();

        // Create V2 draft and add an extra ingredient
        var v2Draft = await _lifecycleService.CreateNextVersionDraftAsync(recipe.Id);
        v2Draft.VersionNumber.Should().Be(2);
        v2Draft.Status.Should().Be(RecipeVersionStatus.Draft);
        await _lifecycleService.AddIngredientToDraftAsync(v2Draft.Id, Guid.NewGuid(), 20m, "g");

        // Activate V2
        var v2ActivatedAt = DateTimeOffset.UtcNow;
        await _lifecycleService.ActivateVersionAsync(recipe.Id, 2, v2ActivatedAt);

        // Now active version must be V2
        var currentActive = await _versionRepo.GetActiveVersionAsync(recipe.Id);
        currentActive.Should().NotBeNull();
        currentActive!.VersionNumber.Should().Be(2);
        currentActive.Status.Should().Be(RecipeVersionStatus.Active);

        // V1 must be archived
        var v1Loaded = await _versionRepo.GetByRecipeAndVersionAsync(recipe.Id, 1);
        v1Loaded!.Status.Should().Be(RecipeVersionStatus.Archived);
        v1Loaded.EffectiveTo.Should().BeCloseTo(v2ActivatedAt, TimeSpan.FromMilliseconds(1));
        v1Loaded.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task DatabaseUniqueConstraintEnforcesSingleActiveVersionPerRecipe()
    {
        var code = "RECIPE-" + Guid.NewGuid().ToString("N")[..8];
        var recipe = await _lifecycleService.CreateRecipeAsync(code, "Burger");

        var v1 = await _lifecycleService.CreateInitialDraftVersionAsync(recipe.Id, 1m, "portion");
        await _lifecycleService.AddIngredientToDraftAsync(v1.Id, Guid.NewGuid(), 150m, "g");
        await _lifecycleService.ActivateVersionAsync(recipe.Id, 1);

        var v2 = await _lifecycleService.CreateNextVersionDraftAsync(recipe.Id);

        // Directly attempt to force second version to 'Active' status without archiving V1 (bypassing service)
        const string directSql = "UPDATE recipe.recipe_versions SET status = 'Active' WHERE id = $1;";
        await using var cmd = _db.DataSource.CreateCommand(directSql);
        cmd.Parameters.AddWithValue(v2.Id);

        var act = () => cmd.ExecuteNonQueryAsync();
        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23505"); // Unique violation on uq_recipe_single_active
    }

    [Fact]
    public async Task DatabaseForeignKeyRestrictsDeletionOfReferencedRecipe()
    {
        var code = "RECIPE-" + Guid.NewGuid().ToString("N")[..8];
        var recipe = await _lifecycleService.CreateRecipeAsync(code, "Soup");
        await _lifecycleService.CreateInitialDraftVersionAsync(recipe.Id, 1m, "portion");

        // Attempt to delete parent recipe while versions exist
        var act = () => _recipeRepo.DeleteAsync(recipe.Id);
        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23503" || e.SqlState == "23001"); // Foreign key violation (RESTRICT: 23001, generic: 23503)
    }

    [Fact]
    public async Task Migration058RollbackAndReapplyWorksCleanly()
    {
        // Rollback migration 058
        await _db.RollbackMigration058Async();

        // Reapply migration 058
        await _db.ReapplyMigration058Async();

        // Check tables exist by executing a simple query
        await using var cmd = _db.DataSource.CreateCommand("SELECT count(*) FROM recipe.recipes;");
        var count = await cmd.ExecuteScalarAsync();
        count.Should().NotBeNull();
    }
}
