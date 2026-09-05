using ALKAROS.Recipes.Units;

namespace ALKAROS.Recipes.Versioning;

public sealed class RecipeLifecycleService : IRecipeLifecycleService
{
    private readonly IRecipeRepository _recipeRepo;
    private readonly IRecipeVersionRepository _versionRepo;
    private readonly IUnitConverter _unitConverter;

    public RecipeLifecycleService(
        IRecipeRepository recipeRepo,
        IRecipeVersionRepository versionRepo,
        IUnitConverter unitConverter)
    {
        _recipeRepo = recipeRepo ?? throw new ArgumentNullException(nameof(recipeRepo));
        _versionRepo = versionRepo ?? throw new ArgumentNullException(nameof(versionRepo));
        _unitConverter = unitConverter ?? throw new ArgumentNullException(nameof(unitConverter));
    }

    public async Task<Recipe> CreateRecipeAsync(string code, string name, string? description = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Recipe code cannot be empty.", nameof(code));

        var existing = await _recipeRepo.GetByCodeAsync(code, ct);
        if (existing != null)
        {
            throw new RecipeVersionConflictException($"Recipe with code '{code.Trim().ToUpperInvariant()}' already exists.");
        }

        var recipe = Recipe.Create(code, name, description);
        await _recipeRepo.AddAsync(recipe, ct);
        return recipe;
    }

    public async Task<RecipeVersion> CreateInitialDraftVersionAsync(
        Guid recipeId,
        decimal yieldQuantity,
        string yieldUnitCode,
        int preparationMinutes = 0,
        string? instructions = null,
        CancellationToken ct = default)
    {
        var recipe = await _recipeRepo.GetByIdAsync(recipeId, ct)
            ?? throw new RecipeNotFoundException(recipeId);

        // Validate unit with converter
        _ = _unitConverter.GetDimension(yieldUnitCode);

        var maxVersion = await _versionRepo.GetMaxVersionNumberAsync(recipeId, ct);
        if (maxVersion > 0)
        {
            throw new InvalidRecipeVersionException(
                $"Cannot create initial draft: recipe {recipeId} already has versions up to v{maxVersion}. Use CreateNextVersionDraftAsync instead.");
        }

        var draft = RecipeVersion.CreateDraft(
            recipeId: recipe.Id,
            versionNumber: 1,
            yieldQuantity: yieldQuantity,
            yieldUnitCode: yieldUnitCode,
            preparationMinutes: preparationMinutes,
            instructions: instructions);

        await _versionRepo.AddAsync(draft, ct);
        return draft;
    }

    public async Task<RecipeVersion> CreateNextVersionDraftAsync(Guid recipeId, CancellationToken ct = default)
    {
        _ = await _recipeRepo.GetByIdAsync(recipeId, ct)
            ?? throw new RecipeNotFoundException(recipeId);

        var maxVersion = await _versionRepo.GetMaxVersionNumberAsync(recipeId, ct);
        if (maxVersion == 0)
        {
            throw new InvalidRecipeVersionException(
                $"Recipe {recipeId} has no existing versions to base a new version on. Create an initial version first.");
        }

        var latestVersion = await _versionRepo.GetByRecipeAndVersionAsync(recipeId, maxVersion, ct)
            ?? throw new InvalidRecipeVersionException($"Could not load latest version {maxVersion} for recipe {recipeId}.");

        var nextVersionNumber = maxVersion + 1;
        var newDraft = latestVersion.CloneAsDraft(nextVersionNumber);

        await _versionRepo.AddAsync(newDraft, ct);
        return newDraft;
    }

    public async Task<RecipeVersion> AddIngredientToDraftAsync(
        Guid versionId,
        Guid ingredientItemId,
        decimal quantity,
        string unitCode,
        decimal lossPercentage = 0.00m,
        int sortOrder = 0,
        string? notes = null,
        CancellationToken ct = default)
    {
        var version = await _versionRepo.GetByIdAsync(versionId, ct)
            ?? throw new InvalidRecipeVersionException($"RecipeVersion {versionId} not found.");

        // Validate unit definition
        _ = _unitConverter.GetDimension(unitCode);

        version.AddIngredient(
            ingredientItemId: ingredientItemId,
            quantity: quantity,
            unitCode: unitCode,
            lossPercentage: lossPercentage,
            sortOrder: sortOrder,
            notes: notes);

        await _versionRepo.UpdateAsync(version, ct);
        return version;
    }

    public async Task<RecipeVersion> RemoveIngredientFromDraftAsync(
        Guid versionId,
        Guid ingredientItemId,
        CancellationToken ct = default)
    {
        var version = await _versionRepo.GetByIdAsync(versionId, ct)
            ?? throw new InvalidRecipeVersionException($"RecipeVersion {versionId} not found.");

        version.RemoveIngredient(ingredientItemId);

        await _versionRepo.UpdateAsync(version, ct);
        return version;
    }

    public async Task ActivateVersionAsync(
        Guid recipeId,
        int versionNumber,
        DateTimeOffset? activatedAt = null,
        CancellationToken ct = default)
    {
        _ = await _recipeRepo.GetByIdAsync(recipeId, ct)
            ?? throw new RecipeNotFoundException(recipeId);

        await _versionRepo.ActivateVersionAsync(recipeId, versionNumber, activatedAt ?? DateTimeOffset.UtcNow, ct);
    }

    public async Task LockVersionForOperationalUseAsync(
        Guid versionId,
        CancellationToken ct = default)
    {
        var version = await _versionRepo.GetByIdAsync(versionId, ct)
            ?? throw new InvalidRecipeVersionException($"RecipeVersion {versionId} not found.");

        version.LockForOperationalUse();
        await _versionRepo.LockVersionAsync(versionId, ct);
    }
}
