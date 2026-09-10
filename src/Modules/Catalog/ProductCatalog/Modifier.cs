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
        // V1-RMD-156: the catalog side of this rule used to be missing while
        // the order side already had it — OrderItemModifier has rejected a
        // negative priceDelta since V1-ORD-001. A manager could still create
        // a modifier priced below zero here, and the first waiter to select
        // it got an ArgumentException with no clue which modifier was at
        // fault: the product became permanently unorderable through that
        // modifier group. Reject it at the one place it is actually created.
        if (priceDelta < 0)
            throw new ArgumentException("Modifier price delta cannot be negative.", nameof(priceDelta));

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
