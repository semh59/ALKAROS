namespace ALKAROS.Recipes.Versioning;

/// <summary>
/// Lifecycle status of a recipe version.
/// </summary>
public enum RecipeVersionStatus
{
    /// <summary>
    /// Draft version under creation or revision. Editable until activated or locked.
    /// </summary>
    Draft,

    /// <summary>
    /// Currently active production recipe version. Immutable and authoritative for operations.
    /// </summary>
    Active,

    /// <summary>
    /// Superseded by a newer active version. Preserved immutably for historical traceability.
    /// </summary>
    Archived,

    /// <summary>
    /// Retired from production without replacement. Immutable.
    /// </summary>
    Deprecated
}
