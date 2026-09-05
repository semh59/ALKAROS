namespace ALKAROS.Recipes.Versioning;

/// <summary>
/// Thrown when an operation attempts to mutate or delete an immutable recipe version.
/// </summary>
public sealed class RecipeVersionImmutableException : InvalidOperationException
{
    public RecipeVersionImmutableException(Guid recipeId, int versionNumber, string reason)
        : base($"Recipe version {versionNumber} of recipe {recipeId} is immutable: {reason}.")
    {
        RecipeId = recipeId;
        VersionNumber = versionNumber;
        Reason = reason;
    }

    public Guid RecipeId { get; }
    public int VersionNumber { get; }
    public string Reason { get; }
}

/// <summary>
/// Thrown when recipe version parameters or state transitions violate domain rules.
/// </summary>
public sealed class InvalidRecipeVersionException : InvalidOperationException
{
    public InvalidRecipeVersionException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Thrown when a recipe is not found.
/// </summary>
public sealed class RecipeNotFoundException : InvalidOperationException
{
    public RecipeNotFoundException(Guid recipeId)
        : base($"Recipe with ID {recipeId} was not found.")
    {
        RecipeId = recipeId;
    }

    public RecipeNotFoundException(string code)
        : base($"Recipe with code '{code}' was not found.")
    {
        Code = code;
    }

    public Guid? RecipeId { get; }
    public string? Code { get; }
}

/// <summary>
/// Thrown when a recipe version conflict occurs (e.g. version number already exists or concurrent update conflict).
/// </summary>
public sealed class RecipeVersionConflictException : InvalidOperationException
{
    public RecipeVersionConflictException(string message)
        : base(message)
    {
    }
}
