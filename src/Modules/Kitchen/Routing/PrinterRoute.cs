namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Domain entity representing a printer route configuration entry (kitchen.printer_routes).
/// Follows the precedence hierarchy: Item (1) > Product (2) > DailySpecial (3) > Category (4) > Default (5).
/// </summary>
public sealed class PrinterRoute
{
    public PrinterRoute(
        Guid id,
        RouteLevel routeLevel,
        Guid printerId,
        Guid? itemId = null,
        Guid? productId = null,
        Guid? categoryId = null,
        DateOnly? specialDate = null,
        bool isActive = true,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Route ID cannot be empty.", nameof(id));
        if (printerId == Guid.Empty)
            throw new ArgumentException("Printer ID cannot be empty.", nameof(printerId));

        ValidatePayloadForRouteLevel(routeLevel, itemId, productId, categoryId, specialDate);

        Id = id;
        RouteLevel = routeLevel;
        PrinterId = printerId;
        ItemId = itemId;
        ProductId = productId;
        CategoryId = categoryId;
        SpecialDate = specialDate;
        IsActive = isActive;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public RouteLevel RouteLevel { get; }
    public Guid PrinterId { get; }
    public Guid? ItemId { get; }
    public Guid? ProductId { get; }
    public Guid? CategoryId { get; }
    public DateOnly? SpecialDate { get; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public static PrinterRoute CreateItemRoute(Guid id, Guid itemId, Guid printerId, bool isActive = true)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("Item ID cannot be empty.", nameof(itemId));
        return new PrinterRoute(id, RouteLevel.Item, printerId, itemId: itemId, isActive: isActive);
    }

    public static PrinterRoute CreateProductRoute(Guid id, Guid productId, Guid printerId, bool isActive = true)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        return new PrinterRoute(id, RouteLevel.Product, printerId, productId: productId, isActive: isActive);
    }

    public static PrinterRoute CreateDailySpecialRoute(Guid id, DateOnly specialDate, Guid categoryId, Guid printerId, bool isActive = true)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID cannot be empty.", nameof(categoryId));
        return new PrinterRoute(id, RouteLevel.DailySpecial, printerId, categoryId: categoryId, specialDate: specialDate, isActive: isActive);
    }

    public static PrinterRoute CreateCategoryRoute(Guid id, Guid categoryId, Guid printerId, bool isActive = true)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID cannot be empty.", nameof(categoryId));
        return new PrinterRoute(id, RouteLevel.Category, printerId, categoryId: categoryId, isActive: isActive);
    }

    public static PrinterRoute CreateDefaultRoute(Guid id, Guid printerId, bool isActive = true)
    {
        return new PrinterRoute(id, RouteLevel.Default, printerId, isActive: isActive);
    }

    public PrinterRoute SetActive(bool active, DateTimeOffset? timestamp = null)
    {
        return new PrinterRoute(
            Id,
            RouteLevel,
            PrinterId,
            ItemId,
            ProductId,
            CategoryId,
            SpecialDate,
            isActive: active,
            createdAt: CreatedAt,
            updatedAt: timestamp ?? DateTimeOffset.UtcNow);
    }

    private static void ValidatePayloadForRouteLevel(
        RouteLevel level,
        Guid? itemId,
        Guid? productId,
        Guid? categoryId,
        DateOnly? specialDate)
    {
        switch (level)
        {
            case RouteLevel.Item:
                if (!itemId.HasValue || itemId.Value == Guid.Empty)
                    throw new ArgumentException("Item route must specify a non-empty ItemId.", nameof(itemId));
                if (productId.HasValue || categoryId.HasValue || specialDate.HasValue)
                    throw new ArgumentException("Item route cannot contain ProductId, CategoryId, or SpecialDate.");
                break;

            case RouteLevel.Product:
                if (!productId.HasValue || productId.Value == Guid.Empty)
                    throw new ArgumentException("Product route must specify a non-empty ProductId.", nameof(productId));
                if (itemId.HasValue || categoryId.HasValue || specialDate.HasValue)
                    throw new ArgumentException("Product route cannot contain ItemId, CategoryId, or SpecialDate.");
                break;

            case RouteLevel.DailySpecial:
                if (!specialDate.HasValue)
                    throw new ArgumentException("Daily special route must specify SpecialDate.", nameof(specialDate));
                if (!categoryId.HasValue || categoryId.Value == Guid.Empty)
                    throw new ArgumentException("Daily special route must specify a non-empty CategoryId.", nameof(categoryId));
                if (itemId.HasValue || productId.HasValue)
                    throw new ArgumentException("Daily special route cannot contain ItemId or ProductId.");
                break;

            case RouteLevel.Category:
                if (!categoryId.HasValue || categoryId.Value == Guid.Empty)
                    throw new ArgumentException("Category route must specify a non-empty CategoryId.", nameof(categoryId));
                if (itemId.HasValue || productId.HasValue || specialDate.HasValue)
                    throw new ArgumentException("Category route cannot contain ItemId, ProductId, or SpecialDate.");
                break;

            case RouteLevel.Default:
                if (itemId.HasValue || productId.HasValue || categoryId.HasValue || specialDate.HasValue)
                    throw new ArgumentException("Default route cannot contain ItemId, ProductId, CategoryId, or SpecialDate.");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(level), $"Unknown RouteLevel: {level}");
        }
    }
}
