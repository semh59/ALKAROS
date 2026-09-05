namespace ALKAROS.Inventory.StockMaster;

public sealed class StockLocation
{
    public StockLocation(
        Guid id,
        string code,
        string name,
        StockLocationType locationType,
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

        Id = id;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        LocationType = locationType;
        IsActive = isActive;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public StockLocationType LocationType { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public int RowVersion { get; internal set; }

    public static StockLocation Create(
        string code,
        string name,
        StockLocationType locationType,
        bool isActive = true)
    {
        return new StockLocation(
            id: Guid.NewGuid(),
            code: code,
            name: name,
            locationType: locationType,
            isActive: isActive,
            createdAt: DateTimeOffset.UtcNow,
            rowVersion: 1);
    }

    public void Update(string name, StockLocationType locationType, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name.Trim();
        LocationType = locationType;
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
            throw new InactiveStockLocationException(
                $"Stock location '{Code}' ({Id}) is inactive and cannot accept stock movements.");
        }
    }
}
