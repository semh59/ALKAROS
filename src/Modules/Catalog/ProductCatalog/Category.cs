namespace ALKAROS.Catalog.ProductCatalog;

/// <summary>
/// Represents a product category (PDF III.4.1).
/// </summary>
public sealed class Category
{
    public Category(Guid id, string code, string name, Guid? parentId = null, int sortOrder = 0, bool active = true)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Category code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name cannot be empty.", nameof(name));

        Id = id;
        Code = code;
        Name = name;
        ParentId = parentId;
        SortOrder = sortOrder;
        Active = active;
    }

    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public Guid? ParentId { get; }
    public int SortOrder { get; }
    public bool Active { get; }
}
