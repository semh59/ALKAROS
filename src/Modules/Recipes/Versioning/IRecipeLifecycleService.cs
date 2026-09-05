using ALKAROS.Recipes.Units;

namespace ALKAROS.Recipes.Versioning;

public interface IRecipeLifecycleService
{
    Task<Recipe> CreateRecipeAsync(string code, string name, string? description = null, CancellationToken ct = default);

    Task<RecipeVersion> CreateInitialDraftVersionAsync(
        Guid recipeId,
        decimal yieldQuantity,
        string yieldUnitCode,
        int preparationMinutes = 0,
        string? instructions = null,
        CancellationToken ct = default);

    Task<RecipeVersion> CreateNextVersionDraftAsync(Guid recipeId, CancellationToken ct = default);

    Task<RecipeVersion> AddIngredientToDraftAsync(
        Guid versionId,
        Guid ingredientItemId,
        decimal quantity,
        string unitCode,
        decimal lossPercentage = 0.00m,
        int sortOrder = 0,
        string? notes = null,
        CancellationToken ct = default);

    Task<RecipeVersion> RemoveIngredientFromDraftAsync(
        Guid versionId,
        Guid ingredientItemId,
        CancellationToken ct = default);

    Task ActivateVersionAsync(
        Guid recipeId,
        int versionNumber,
        DateTimeOffset? activatedAt = null,
        CancellationToken ct = default);

    Task LockVersionForOperationalUseAsync(
        Guid versionId,
        CancellationToken ct = default);
}
