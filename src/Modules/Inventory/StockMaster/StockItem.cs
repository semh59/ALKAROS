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
        int rowVersion = 1)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be empty.", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(trackingUnitCode))
            throw new ArgumentException("Tracking unit code cannot be empty.", nameof(trackingUnitCode));

        Id = id;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        ItemType = itemType;
        TrackingUnitCode = trackingUnitCode.Trim().ToLowerInvariant();
        DefaultLocationId = defaultLocationId;
        IsActive = isActive;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        RowVersion = rowVersion;
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

    public static StockItem Create(
        string code,
        string name,
        StockItemType itemType,
        string trackingUnitCode,
        Guid? defaultLocationId = null,
        bool isActive = true)
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
            rowVersion: 1);
    }

    public void Update(
        string name,
        StockItemType itemType,
        string trackingUnitCode,
        Guid? defaultLocationId,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(trackingUnitCode))
            throw new ArgumentException("Tracking unit code cannot be empty.", nameof(trackingUnitCode));

        Name = name.Trim();
        ItemType = itemType;
        TrackingUnitCode = trackingUnitCode.Trim().ToLowerInvariant();
        DefaultLocationId = defaultLocationId;
        IsActive = isActive;
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
