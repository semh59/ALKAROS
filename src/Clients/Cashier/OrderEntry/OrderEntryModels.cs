namespace ALKAROS.Clients.Cashier.OrderEntry;

/// <summary>
/// Catalog product item for cashier order entry (V1-CUI-002, PDF:I.8).
/// </summary>
public sealed record CatalogProductItem(
    Guid ProductId,
    string Name,
    string Category,
    decimal BasePrice,
    IReadOnlyList<CatalogModifierItem> Modifiers);

/// <summary>
/// Modifier item for a product (e.g., Extra Cheese, No Onion) (V1-CUI-002).
/// </summary>
public sealed record CatalogModifierItem(
    Guid ModifierId,
    string Name,
    decimal AdditionalPrice,
    bool IsRequired = false);

/// <summary>
/// Selected modifier for a draft order line (V1-CUI-002).
/// </summary>
public sealed record SelectedModifier(
    Guid ModifierId,
    string Name,
    decimal Price);

/// <summary>
/// An item line in the working order draft (V1-CUI-002).
/// </summary>
public sealed record DraftOrderItem(
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    IReadOnlyList<SelectedModifier> Modifiers,
    string? SpecialInstructions)
{
    public decimal ModifiersTotal => Modifiers.Sum(m => m.Price);
    public decimal ItemUnitPrice => UnitPrice + ModifiersTotal;
    public decimal LineTotal => ItemUnitPrice * Quantity;
}

/// <summary>
/// The complete working draft for a table order (V1-CUI-002).
/// </summary>
public sealed record OrderDraft(
    Guid TableId,
    string TableNumber,
    IReadOnlyList<DraftOrderItem> Items,
    string? Note)
{
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public int TotalItemCount => Items.Sum(i => i.Quantity);
}

/// <summary>
/// Order submission result from cashier client (V1-CUI-002).
/// </summary>
public sealed record OrderSubmissionResult(
    bool IsSuccess,
    Guid? OrderId,
    string IdempotencyKey,
    string? ErrorMessage,
    bool CanRetry);
