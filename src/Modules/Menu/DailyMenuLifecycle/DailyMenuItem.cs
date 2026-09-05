namespace ALKAROS.Menu.DailyMenuLifecycle;

public sealed class DailyMenuItem
{
    public Guid Id { get; }
    public Guid DailyMenuId { get; }
    public Guid ProductId { get; }
    public string ProductNameSnapshot { get; }
    public Guid? RecipeVersionId { get; }
    public decimal Price { get; private set; }
    public decimal PlannedPortions { get; private set; }
    public decimal PreparedPortions { get; private set; }
    public decimal AvailablePortions { get; private set; }
    public decimal ReservedPortions { get; private set; }
    public decimal ConsumedPortions { get; private set; }
    public decimal WastePortions { get; private set; }
    public bool IsOutOfStock { get; private set; }
    public string? PrinterRoutePolicy { get; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public DailyMenuItem(
        Guid id,
        Guid dailyMenuId,
        Guid productId,
        string productNameSnapshot,
        Guid? recipeVersionId,
        decimal price,
        decimal plannedPortions,
        decimal preparedPortions,
        decimal availablePortions,
        decimal reservedPortions,
        decimal consumedPortions,
        decimal wastePortions,
        bool isOutOfStock,
        string? printerRoutePolicy,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (dailyMenuId == Guid.Empty)
            throw new ArgumentException("DailyMenuId cannot be empty.", nameof(dailyMenuId));
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new ArgumentException("ProductNameSnapshot cannot be empty.", nameof(productNameSnapshot));
        if (price < 0m)
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        if (plannedPortions < 0m)
            throw new ArgumentOutOfRangeException(nameof(plannedPortions), "PlannedPortions cannot be negative.");

        Id = id;
        DailyMenuId = dailyMenuId;
        ProductId = productId;
        ProductNameSnapshot = productNameSnapshot.Trim();
        RecipeVersionId = recipeVersionId;
        Price = price;
        PlannedPortions = plannedPortions;
        PreparedPortions = preparedPortions;
        AvailablePortions = availablePortions;
        ReservedPortions = reservedPortions;
        ConsumedPortions = consumedPortions;
        WastePortions = wastePortions;
        IsOutOfStock = isOutOfStock;
        PrinterRoutePolicy = printerRoutePolicy;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static DailyMenuItem Create(
        Guid dailyMenuId,
        Guid productId,
        string productNameSnapshot,
        decimal price,
        Guid? recipeVersionId = null,
        decimal plannedPortions = 0m,
        string? printerRoutePolicy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new DailyMenuItem(
            id: Guid.NewGuid(),
            dailyMenuId: dailyMenuId,
            productId: productId,
            productNameSnapshot: productNameSnapshot,
            recipeVersionId: recipeVersionId,
            price: price,
            plannedPortions: plannedPortions,
            preparedPortions: 0m,
            availablePortions: 0m,
            reservedPortions: 0m,
            consumedPortions: 0m,
            wastePortions: 0m,
            isOutOfStock: false,
            printerRoutePolicy: printerRoutePolicy,
            isActive: true,
            createdAt: now,
            updatedAt: now);
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(newPrice), "Price cannot be negative.");

        Price = newPrice;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePlannedPortions(decimal newPlannedPortions)
    {
        if (newPlannedPortions < 0m)
            throw new ArgumentOutOfRangeException(nameof(newPlannedPortions), "Planned portions cannot be negative.");

        PlannedPortions = newPlannedPortions;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateStatus(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
