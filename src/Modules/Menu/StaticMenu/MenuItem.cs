namespace ALKAROS.Menu.StaticMenu;

public sealed class MenuItem
{
    public Guid Id { get; }
    public Guid MenuId { get; }
    public Guid ProductId { get; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public MenuItem(
        Guid id,
        Guid menuId,
        Guid productId,
        int displayOrder,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("MenuItem id cannot be empty.", nameof(id));
        if (menuId == Guid.Empty)
            throw new ArgumentException("Menu id cannot be empty.", nameof(menuId));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id cannot be empty.", nameof(productId));
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        Id = id;
        MenuId = menuId;
        ProductId = productId;
        DisplayOrder = displayOrder;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static MenuItem Create(Guid menuId, Guid productId, int displayOrder = 0)
    {
        var now = DateTimeOffset.UtcNow;
        return new MenuItem(Guid.NewGuid(), menuId, productId, displayOrder, true, now, now);
    }

    public void UpdateOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        DisplayOrder = displayOrder;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateStatus(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
