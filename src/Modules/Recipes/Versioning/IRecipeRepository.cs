namespace ALKAROS.Recipes.Versioning;

/// <summary>
/// Repository interface for recipe metadata and identity.
/// </summary>
public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Recipe?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Recipe>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Recipe recipe, CancellationToken ct = default);
    Task UpdateAsync(Recipe recipe, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
