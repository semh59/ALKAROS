namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// A <see cref="DataCategory"/> has no entry in <see cref="DisposalMatrix"/>.
/// Fails closed: an unmapped category is never silently skipped or defaulted.
/// </summary>
public sealed class RetentionCategoryUnmappedException : Exception
{
    public DataCategory Category { get; }

    public RetentionCategoryUnmappedException(DataCategory category)
        : base($"Data category '{category}' has no disposal-matrix entry.")
    {
        Category = category;
    }
}
