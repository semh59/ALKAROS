namespace ALKAROS.Inventory.StockMaster;

public sealed class StockItem
{
    public StockItem(
        Guid id,
        string code,
        string name,
        StockItemType itemType,
        string trackingUnitCode,
        Guid? defaultLocationId = null,
        bool isActive = true,
        DateTimeOffset? createdAt = null,
        int rowVersion = 1,
        decimal? reorderPoint = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be empty.", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(trackingUnitCode))
            throw new ArgumentException("Tracking unit code cannot be empty.", nameof(trackingUnitCode));

        if (reorderPoint is < 0m)
            throw new ArgumentOutOfRangeException(nameof(reorderPoint), "Reorder point cannot be negative.");

        Id = id;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        ItemType = itemType;
        TrackingUnitCode = trackingUnitCode.Trim().ToLowerInvariant();
        DefaultLocationId = defaultLocationId;
        IsActive = isActive;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        RowVersion = rowVersion;
        ReorderPoint = reorderPoint;
    }

    public Guid Id { get; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public StockItemType ItemType { get; private set; }
    public string TrackingUnitCode { get; private set; }
    public Guid? DefaultLocationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public int RowVersion { get; internal set; }

    /// <summary>
    /// V11-INV-009: the on-hand threshold below which this item should be
    /// flagged low (V11-RPT-002's alert/report). Null means no threshold is
    /// configured yet — existing behavior (the passive `CriticalStockReport`
    /// falling back to a caller-supplied threshold) is unchanged.
    /// </summary>
    public decimal? ReorderPoint { get; private set; }

    public static StockItem Create(
        string code,
        string name,
        StockItemType itemType,
        string trackingUnitCode,
        Guid? defaultLocationId = null,
        bool isActive = true,
        decimal? reorderPoint = null)
    {
        return new StockItem(
            id: Guid.NewGuid(),
            code: code,
            name: name,
            itemType: itemType,
            trackingUnitCode: trackingUnitCode,
            defaultLocationId: defaultLocationId,
            isActive: isActive,
            createdAt: DateTimeOffset.UtcNow,
            rowVersion: 1,
            reorderPoint: reorderPoint);
    }

    public void Update(
        string name,
        StockItemType itemType,
        string trackingUnitCode,
        Guid? defaultLocationId,
        bool isActive,
        decimal? reorderPoint = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(trackingUnitCode))
            throw new ArgumentException("Tracking unit code cannot be empty.", nameof(trackingUnitCode));

        if (reorderPoint is < 0m)
            throw new ArgumentOutOfRangeException(nameof(reorderPoint), "Reorder point cannot be negative.");

        Name = name.Trim();
        ItemType = itemType;
        TrackingUnitCode = trackingUnitCode.Trim().ToLowerInvariant();
        DefaultLocationId = defaultLocationId;
        IsActive = isActive;
        ReorderPoint = reorderPoint;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void EnsureActiveForMovement()
    {
        if (!IsActive)
        {
            throw new InactiveStockItemException(
                $"Stock item '{Code}' ({Id}) is inactive and cannot accept stock movements.");
        }
    }
}
