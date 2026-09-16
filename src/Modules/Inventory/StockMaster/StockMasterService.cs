using ALKAROS.Measurements;

namespace ALKAROS.Inventory.StockMaster;

public sealed class StockMasterService : IStockMasterService
{
    private readonly IStockLocationRepository _locationRepo;
    private readonly IStockItemRepository _itemRepo;
    private readonly IProductStockMappingRepository _mappingRepo;
    private readonly IUnitConverter _unitConverter;

    public StockMasterService(
        IStockLocationRepository locationRepo,
        IStockItemRepository itemRepo,
        IProductStockMappingRepository mappingRepo,
        IUnitConverter unitConverter)
    {
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _mappingRepo = mappingRepo ?? throw new ArgumentNullException(nameof(mappingRepo));
        _unitConverter = unitConverter ?? throw new ArgumentNullException(nameof(unitConverter));
    }

    public async Task<StockLocation> CreateLocationAsync(
        string code,
        string name,
        StockLocationType locationType,
        bool isActive = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Location code cannot be empty.", nameof(code));

        var normalizedCode = code.Trim().ToUpperInvariant();
        var existing = await _locationRepo.GetByCodeAsync(normalizedCode, ct);
        if (existing != null)
        {
            throw new DuplicateStockLocationException($"Stock location with code '{normalizedCode}' already exists.");
        }

        var location = StockLocation.Create(normalizedCode, name, locationType, isActive);
        await _locationRepo.AddAsync(location, ct);
        return location;
    }

    public async Task<StockItem> CreateStockItemAsync(
        string code,
        string name,
        StockItemType itemType,
        string trackingUnitCode,
        Guid? defaultLocationId = null,
        bool isActive = true,
        decimal? reorderPoint = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Stock item code cannot be empty.", nameof(code));

        if (string.IsNullOrWhiteSpace(trackingUnitCode))
            throw new ArgumentException("Tracking unit code cannot be empty.", nameof(trackingUnitCode));

        // Dimension safety: Validate that tracking unit is recognized by UnitConverter
        var normalizedUnit = trackingUnitCode.Trim().ToLowerInvariant();
        _ = _unitConverter.GetDimension(normalizedUnit);

        var normalizedCode = code.Trim().ToUpperInvariant();
        var existing = await _itemRepo.GetByCodeAsync(normalizedCode, ct);
        if (existing != null)
        {
            throw new DuplicateStockItemException($"Stock item with code '{normalizedCode}' already exists.");
        }

        if (defaultLocationId.HasValue)
        {
            var loc = await _locationRepo.GetByIdAsync(defaultLocationId.Value, ct)
                ?? throw new StockLocationNotFoundException(defaultLocationId.Value);

            if (!loc.IsActive)
            {
                throw new InactiveStockLocationException(
                    $"Cannot set inactive stock location '{loc.Code}' as default location.");
            }
        }

        var item = StockItem.Create(
            code: normalizedCode,
            name: name,
            itemType: itemType,
            trackingUnitCode: normalizedUnit,
            defaultLocationId: defaultLocationId,
            isActive: isActive,
            reorderPoint: reorderPoint);

        await _itemRepo.AddAsync(item, ct);
        return item;
    }

    public async Task<StockItem> SetReorderPointAsync(
        Guid stockItemId,
        decimal? reorderPoint,
        CancellationToken ct = default)
    {
        var item = await _itemRepo.GetByIdAsync(stockItemId, ct)
            ?? throw new StockItemNotFoundException(stockItemId);

        item.Update(item.Name, item.ItemType, item.TrackingUnitCode, item.DefaultLocationId, item.IsActive, reorderPoint);
        await _itemRepo.UpdateAsync(item, ct);
        return item;
    }

    public async Task<ProductStockMapping> AssignProductToStockItemAsync(
        Guid productId,
        Guid stockItemId,
        decimal quantityMultiplier = 1.0m,
        string? notes = null,
        CancellationToken ct = default)
    {
        var item = await _itemRepo.GetByIdAsync(stockItemId, ct)
            ?? throw new StockItemNotFoundException(stockItemId);

        var mapping = new ProductStockMapping(productId, item.Id, quantityMultiplier, notes);
        await _mappingRepo.AddOrUpdateAsync(mapping, ct);
        return mapping;
    }

    public async Task<(StockItem Item, StockLocation Location)> ValidateMovementTargetAsync(
        Guid stockItemId,
        Guid stockLocationId,
        CancellationToken ct = default)
    {
        var item = await _itemRepo.GetByIdAsync(stockItemId, ct)
            ?? throw new StockItemNotFoundException(stockItemId);

        var location = await _locationRepo.GetByIdAsync(stockLocationId, ct)
            ?? throw new StockLocationNotFoundException(stockLocationId);

        item.EnsureActiveForMovement();
        location.EnsureActiveForMovement();

        return (item, location);
    }
}
