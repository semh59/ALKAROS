namespace ALKAROS.Catalog.ProductCatalog;

/// <summary>
/// Represents a modifier (e.g., "Extra cheese", "No onions") (PDF III.4.6).
/// The code is globally unique; product_id is an optional scoping link.
/// </summary>
public sealed class Modifier
{
    public Modifier(
        Guid id,
        Guid modifierGroupId,
        string code,
        string name,
        decimal priceDelta = 0,
        Guid? productId = null,
        bool active = true)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Modifier code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Modifier name cannot be empty.", nameof(name));

        Id = id;
        ModifierGroupId = modifierGroupId;
        Code = code;
        Name = name;
        PriceDelta = priceDelta;
        ProductId = productId;
        Active = active;
    }

    public Guid Id { get; }
    public Guid ModifierGroupId { get; }
    public string Code { get; }
    public string Name { get; }
    public decimal PriceDelta { get; }
    public Guid? ProductId { get; }
    public bool Active { get; }
}
