using System;

namespace ALKAROS.Recipes.CostSnapshots;

public abstract class RecipeCostSnapshotException : Exception
{
    protected RecipeCostSnapshotException(string message) : base(message) { }
    protected RecipeCostSnapshotException(string message, Exception inner) : base(message, inner) { }
}

public sealed class RecipeCostSnapshotNotFoundException : RecipeCostSnapshotException
{
    public RecipeCostSnapshotNotFoundException(Guid id) : base($"Recipe cost snapshot '{id}' was not found.") { }
    public RecipeCostSnapshotNotFoundException(Guid recipeVersionId, DateOnly date)
        : base($"No effective cost snapshot found for recipe version '{recipeVersionId}' on or before {date:yyyy-MM-dd}.") { }
}

public sealed class RecipeVersionNotFoundException : RecipeCostSnapshotException
{
    public RecipeVersionNotFoundException(Guid id) : base($"Recipe version '{id}' was not found.") { }
}

public sealed class DuplicateCostSnapshotException : RecipeCostSnapshotException
{
    public DuplicateCostSnapshotException(Guid recipeVersionId, DateOnly date)
        : base($"Cost snapshot already exists for recipe version '{recipeVersionId}' on {date:yyyy-MM-dd}.") { }
}

public sealed class InvalidCostSnapshotException : RecipeCostSnapshotException
{
    public InvalidCostSnapshotException(string message) : base(message) { }
}

public sealed class MissingCostBasisException : RecipeCostSnapshotException
{
    public MissingCostBasisException(Guid stockItemId, DateOnly date)
        : base($"Cost basis could not be resolved for stock item '{stockItemId}' on or before {date:yyyy-MM-dd}.") { }
}

/// <summary>
/// V1-RMD-333 (independent 2026-09-26 audit, orta seviye bulgu): before this, an ingredient
/// missing from CreateSnapshotCommand.StockItemUnits silently defaulted to the recipe's own
/// native unit as the STOCK unit too — RecipeCostSnapshotEndpoints.cs's own doc comment
/// deliberately keeps this service without a direct Inventory dependency (V0-ARC-001), so it
/// cannot look the real tracking unit up itself. When the native unit and the real stock
/// tracking unit differ by a conversion factor (e.g. "kg" vs "g"), that silent default computed
/// a snapshot cost off by exactly that factor with no error at all. Failing loud here, instead
/// of computing a wrong number with total confidence, is the only safe option within the
/// module's own approved boundary.
/// </summary>
public sealed class MissingStockUnitMappingException : RecipeCostSnapshotException
{
    public MissingStockUnitMappingException(Guid stockItemId)
        : base($"No stock tracking unit was supplied for stock item '{stockItemId}'; refusing to assume it matches the recipe's own native unit.") { }
}
