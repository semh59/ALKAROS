namespace ALKAROS.Menu.StaticMenu;

public abstract class MenuException : Exception
{
    protected MenuException(string message) : base(message) { }
    protected MenuException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class MenuNotFoundException : MenuException
{
    public Guid? MenuId { get; }
    public string? Code { get; }

    public MenuNotFoundException(Guid menuId)
        : base($"Menu with ID '{menuId}' was not found.")
    {
        MenuId = menuId;
    }

    public MenuNotFoundException(string code)
        : base($"Menu with code '{code}' was not found.")
    {
        Code = code;
    }
}

public sealed class MenuItemNotFoundException : MenuException
{
    public Guid MenuItemId { get; }

    public MenuItemNotFoundException(Guid menuItemId)
        : base($"MenuItem with ID '{menuItemId}' was not found.")
    {
        MenuItemId = menuItemId;
    }
}

public sealed class DuplicateMenuCodeException : MenuException
{
    public string Code { get; }

    public DuplicateMenuCodeException(string code)
        : base($"A menu with code '{code}' already exists.")
    {
        Code = code;
    }
}

public sealed class DuplicateMenuItemProductException : MenuException
{
    public Guid MenuId { get; }
    public Guid ProductId { get; }

    public DuplicateMenuItemProductException(Guid menuId, Guid productId)
        : base($"Product '{productId}' is already assigned to menu '{menuId}'. Duplicate menu products are not allowed.")
    {
        MenuId = menuId;
        ProductId = productId;
    }
}

public sealed class InvalidMenuCommandException : MenuException
{
    public InvalidMenuCommandException(string message) : base(message) { }
}

public sealed class CatalogProductNotFoundException : MenuException
{
    public Guid ProductId { get; }

    public CatalogProductNotFoundException(Guid productId)
        : base($"Catalog product with ID '{productId}' was not found.")
    {
        ProductId = productId;
    }
}
