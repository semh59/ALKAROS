namespace ALKAROS.Recipes.Versioning;

/// <summary>
/// An ingredient component item within a recipe version.
/// </summary>
public sealed class RecipeIngredientItem
{
    public RecipeIngredientItem(
        Guid id,
        Guid recipeVersionId,
        Guid ingredientItemId,
        decimal quantity,
        string unitCode,
        decimal lossPercentage = 0.00m,
        int sortOrder = 0,
        string? notes = null,
        DateTimeOffset? createdAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        if (ingredientItemId == Guid.Empty)
            throw new ArgumentException("IngredientItemId cannot be empty.", nameof(ingredientItemId));

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("UnitCode cannot be empty.", nameof(unitCode));

        if (lossPercentage < 0 || lossPercentage >= 100)
            throw new ArgumentOutOfRangeException(nameof(lossPercentage), "Loss percentage must be between 0 and 99.99.");

        Id = id;
        RecipeVersionId = recipeVersionId;
        IngredientItemId = ingredientItemId;
        Quantity = quantity;
        UnitCode = unitCode.Trim().ToLowerInvariant();
        LossPercentage = lossPercentage;
        SortOrder = sortOrder;
        Notes = notes?.Trim();
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public Guid RecipeVersionId { get; internal set; }
    public Guid IngredientItemId { get; }
    public decimal Quantity { get; private set; }
    public string UnitCode { get; private set; }
    public decimal LossPercentage { get; private set; }
    public int SortOrder { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public static RecipeIngredientItem Create(
        Guid recipeVersionId,
        Guid ingredientItemId,
        decimal quantity,
        string unitCode,
        decimal lossPercentage = 0.00m,
        int sortOrder = 0,
        string? notes = null)
    {
        return new RecipeIngredientItem(
            id: Guid.NewGuid(),
            recipeVersionId: recipeVersionId,
            ingredientItemId: ingredientItemId,
            quantity: quantity,
            unitCode: unitCode,
            lossPercentage: lossPercentage,
            sortOrder: sortOrder,
            notes: notes);
    }

    public RecipeIngredientItem CloneForNewVersion(Guid newRecipeVersionId)
    {
        return new RecipeIngredientItem(
            id: Guid.NewGuid(),
            recipeVersionId: newRecipeVersionId,
            ingredientItemId: IngredientItemId,
            quantity: Quantity,
            unitCode: UnitCode,
            lossPercentage: LossPercentage,
            sortOrder: SortOrder,
            notes: Notes,
            createdAt: DateTimeOffset.UtcNow);
    }

    public void Update(decimal quantity, string unitCode, decimal lossPercentage, int sortOrder, string? notes)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("UnitCode cannot be empty.", nameof(unitCode));

        if (lossPercentage < 0 || lossPercentage >= 100)
            throw new ArgumentOutOfRangeException(nameof(lossPercentage), "Loss percentage must be between 0 and 99.99.");

        Quantity = quantity;
        UnitCode = unitCode.Trim().ToLowerInvariant();
        LossPercentage = lossPercentage;
        SortOrder = sortOrder;
        Notes = notes?.Trim();
    }
}
