namespace ALKAROS.Recipes.Versioning;

/// <summary>
/// Immutable or draft version of a recipe.
/// Once activated or referenced in operational production, the version is strictly immutable.
/// Changes must be made by creating a new version draft.
/// </summary>
public sealed class RecipeVersion
{
    private readonly List<RecipeIngredientItem> _ingredients = new();

    public RecipeVersion(
        Guid id,
        Guid recipeId,
        int versionNumber,
        RecipeVersionStatus status,
        decimal yieldQuantity,
        string yieldUnitCode,
        int preparationMinutes = 0,
        string? instructions = null,
        bool isLocked = false,
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? effectiveTo = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? activatedAt = null,
        int rowVersion = 1,
        IEnumerable<RecipeIngredientItem>? ingredients = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        if (recipeId == Guid.Empty)
            throw new ArgumentException("RecipeId cannot be empty.", nameof(recipeId));

        if (versionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be positive (>= 1).");

        if (yieldQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(yieldQuantity), "Yield quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(yieldUnitCode))
            throw new ArgumentException("Yield unit code cannot be empty.", nameof(yieldUnitCode));

        if (preparationMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(preparationMinutes), "Preparation minutes cannot be negative.");

        Id = id;
        RecipeId = recipeId;
        VersionNumber = versionNumber;
        Status = status;
        YieldQuantity = yieldQuantity;
        YieldUnitCode = yieldUnitCode.Trim().ToLowerInvariant();
        PreparationMinutes = preparationMinutes;
        Instructions = instructions?.Trim();
        IsLocked = isLocked || status != RecipeVersionStatus.Draft;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        ActivatedAt = activatedAt;
        RowVersion = rowVersion;

        if (ingredients != null)
        {
            _ingredients.AddRange(ingredients);
        }
    }

    public Guid Id { get; }
    public Guid RecipeId { get; }
    public int VersionNumber { get; }
    public RecipeVersionStatus Status { get; private set; }
    public decimal YieldQuantity { get; private set; }
    public string YieldUnitCode { get; private set; }
    public int PreparationMinutes { get; private set; }
    public string? Instructions { get; private set; }
    public bool IsLocked { get; private set; }
    public DateTimeOffset? EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public int RowVersion { get; internal set; }

    public IReadOnlyList<RecipeIngredientItem> Ingredients => _ingredients.AsReadOnly();

    public static RecipeVersion CreateDraft(
        Guid recipeId,
        int versionNumber,
        decimal yieldQuantity,
        string yieldUnitCode,
        int preparationMinutes = 0,
        string? instructions = null,
        DateTimeOffset? createdAt = null)
    {
        return new RecipeVersion(
            id: Guid.NewGuid(),
            recipeId: recipeId,
            versionNumber: versionNumber,
            status: RecipeVersionStatus.Draft,
            yieldQuantity: yieldQuantity,
            yieldUnitCode: yieldUnitCode,
            preparationMinutes: preparationMinutes,
            instructions: instructions,
            isLocked: false,
            effectiveFrom: null,
            effectiveTo: null,
            createdAt: createdAt ?? DateTimeOffset.UtcNow,
            activatedAt: null,
            rowVersion: 1);
    }

    public void AddIngredient(
        Guid ingredientItemId,
        decimal quantity,
        string unitCode,
        decimal lossPercentage = 0.00m,
        int sortOrder = 0,
        string? notes = null)
    {
        EnsureMutable("adding ingredients");

        if (_ingredients.Any(i => i.IngredientItemId == ingredientItemId))
        {
            throw new InvalidRecipeVersionException(
                $"Ingredient {ingredientItemId} already exists in recipe version {VersionNumber}.");
        }

        var item = RecipeIngredientItem.Create(
            recipeVersionId: Id,
            ingredientItemId: ingredientItemId,
            quantity: quantity,
            unitCode: unitCode,
            lossPercentage: lossPercentage,
            sortOrder: sortOrder,
            notes: notes);

        _ingredients.Add(item);
    }

    public void UpdateIngredient(
        Guid ingredientItemId,
        decimal quantity,
        string unitCode,
        decimal lossPercentage = 0.00m,
        int sortOrder = 0,
        string? notes = null)
    {
        EnsureMutable("modifying ingredients");

        var existing = _ingredients.FirstOrDefault(i => i.IngredientItemId == ingredientItemId);
        if (existing == null)
        {
            throw new InvalidRecipeVersionException(
                $"Ingredient {ingredientItemId} was not found in recipe version {VersionNumber}.");
        }

        existing.Update(quantity, unitCode, lossPercentage, sortOrder, notes);
    }

    public void RemoveIngredient(Guid ingredientItemId)
    {
        EnsureMutable("removing ingredients");

        var existing = _ingredients.FirstOrDefault(i => i.IngredientItemId == ingredientItemId);
        if (existing == null)
        {
            throw new InvalidRecipeVersionException(
                $"Ingredient {ingredientItemId} was not found in recipe version {VersionNumber}.");
        }

        _ingredients.Remove(existing);
    }

    public void UpdateYieldAndPreparation(
        decimal yieldQuantity,
        string yieldUnitCode,
        int preparationMinutes,
        string? instructions)
    {
        EnsureMutable("updating yield and preparation details");

        if (yieldQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(yieldQuantity), "Yield quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(yieldUnitCode))
            throw new ArgumentException("Yield unit code cannot be empty.", nameof(yieldUnitCode));

        if (preparationMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(preparationMinutes), "Preparation minutes cannot be negative.");

        YieldQuantity = yieldQuantity;
        YieldUnitCode = yieldUnitCode.Trim().ToLowerInvariant();
        PreparationMinutes = preparationMinutes;
        Instructions = instructions?.Trim();
    }

    public void Activate(DateTimeOffset activatedAt)
    {
        if (Status == RecipeVersionStatus.Active)
        {
            return; // Already active (idempotent)
        }

        if (Status != RecipeVersionStatus.Draft)
        {
            throw new InvalidRecipeVersionException(
                $"Cannot activate recipe version {VersionNumber} because it is in '{Status}' state. Only Draft versions can be activated.");
        }

        if (_ingredients.Count == 0)
        {
            throw new InvalidRecipeVersionException(
                $"Cannot activate recipe version {VersionNumber} with zero ingredients.");
        }

        if (YieldQuantity <= 0)
        {
            throw new InvalidRecipeVersionException(
                $"Cannot activate recipe version {VersionNumber} with non-positive yield ({YieldQuantity}).");
        }

        Status = RecipeVersionStatus.Active;
        EffectiveFrom = activatedAt;
        ActivatedAt = activatedAt;
        IsLocked = true;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (Status != RecipeVersionStatus.Active)
        {
            throw new InvalidRecipeVersionException(
                $"Cannot archive recipe version {VersionNumber} because it is in '{Status}' state. Only Active versions can be archived.");
        }

        Status = RecipeVersionStatus.Archived;
        EffectiveTo = archivedAt;
        IsLocked = true;
    }

    public void Deprecate()
    {
        Status = RecipeVersionStatus.Deprecated;
        EffectiveTo = DateTimeOffset.UtcNow;
        IsLocked = true;
    }

    public void LockForOperationalUse()
    {
        IsLocked = true;
    }

    public RecipeVersion CloneAsDraft(int nextVersionNumber, DateTimeOffset? createdAt = null)
    {
        if (nextVersionNumber <= VersionNumber)
        {
            throw new InvalidRecipeVersionException(
                $"Next version number ({nextVersionNumber}) must be strictly greater than current version ({VersionNumber}).");
        }

        var newDraft = new RecipeVersion(
            id: Guid.NewGuid(),
            recipeId: RecipeId,
            versionNumber: nextVersionNumber,
            status: RecipeVersionStatus.Draft,
            yieldQuantity: YieldQuantity,
            yieldUnitCode: YieldUnitCode,
            preparationMinutes: PreparationMinutes,
            instructions: Instructions,
            isLocked: false,
            effectiveFrom: null,
            effectiveTo: null,
            createdAt: createdAt ?? DateTimeOffset.UtcNow,
            activatedAt: null,
            rowVersion: 1);

        foreach (var ingredient in _ingredients)
        {
            newDraft._ingredients.Add(ingredient.CloneForNewVersion(newDraft.Id));
        }

        return newDraft;
    }

    private void EnsureMutable(string operation)
    {
        if (IsLocked || Status != RecipeVersionStatus.Draft)
        {
            throw new RecipeVersionImmutableException(
                RecipeId,
                VersionNumber,
                $"cannot perform '{operation}' on status '{Status}' (IsLocked={IsLocked})");
        }
    }
}
