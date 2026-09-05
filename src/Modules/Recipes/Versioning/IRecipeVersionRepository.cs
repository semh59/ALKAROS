namespace ALKAROS.Recipes.Versioning;

/// <summary>
/// Repository interface for versioned recipes and their constituent ingredient lists.
/// </summary>
public interface IRecipeVersionRepository
{
    Task<RecipeVersion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RecipeVersion?> GetByRecipeAndVersionAsync(Guid recipeId, int versionNumber, CancellationToken ct = default);
    Task<RecipeVersion?> GetActiveVersionAsync(Guid recipeId, CancellationToken ct = default);
    Task<IReadOnlyList<RecipeVersion>> GetAllVersionsAsync(Guid recipeId, CancellationToken ct = default);
    Task<int> GetMaxVersionNumberAsync(Guid recipeId, CancellationToken ct = default);
    Task AddAsync(RecipeVersion version, CancellationToken ct = default);
    Task UpdateAsync(RecipeVersion version, CancellationToken ct = default);
    Task ActivateVersionAsync(Guid recipeId, int versionNumber, DateTimeOffset activatedAt, CancellationToken ct = default);
    Task LockVersionAsync(Guid versionId, CancellationToken ct = default);
}
