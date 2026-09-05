namespace ALKAROS.Inventory.BalanceProjection;

public sealed class StockBalance
{
    public StockBalance(
        Guid id,
        Guid stockItemId,
        Guid stockLocationId,
        decimal onHandQuantity,
        decimal reservedQuantity = 0m,
        decimal? availableQuantity = null,
        DateTimeOffset? updatedAt = null,
        int rowVersion = 1)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));

        if (stockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(stockLocationId));

        Id = id;
        StockItemId = stockItemId;
        StockLocationId = stockLocationId;
        OnHandQuantity = onHandQuantity;
        ReservedQuantity = reservedQuantity;
        AvailableQuantity = availableQuantity ?? (onHandQuantity - reservedQuantity);
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }
    public Guid StockItemId { get; }
    public Guid StockLocationId { get; }
    public decimal OnHandQuantity { get; private set; }
    public decimal ReservedQuantity { get; private set; }
    public decimal AvailableQuantity { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int RowVersion { get; internal set; }

    public static StockBalance Create(
        Guid stockItemId,
        Guid stockLocationId,
        decimal initialOnHand = 0m)
    {
        return new StockBalance(
            id: Guid.NewGuid(),
            stockItemId: stockItemId,
            stockLocationId: stockLocationId,
            onHandQuantity: initialOnHand,
            reservedQuantity: 0m,
            availableQuantity: initialOnHand,
            updatedAt: DateTimeOffset.UtcNow,
            rowVersion: 1);
    }

    public void ApplyOnHandDelta(decimal delta)
    {
        OnHandQuantity += delta;
        AvailableQuantity = OnHandQuantity - ReservedQuantity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetOnHand(decimal newOnHandQuantity)
    {
        OnHandQuantity = newOnHandQuantity;
        AvailableQuantity = OnHandQuantity - ReservedQuantity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
