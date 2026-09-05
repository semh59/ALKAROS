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
