using ALKAROS.Inventory.StockMaster;
using ALKAROS.Recipes.Units;

namespace ALKAROS.Inventory.MovementLedger;

public sealed class StockMovementService : IStockMovementService
{
    private readonly IStockMovementRepository _movementRepo;
    private readonly IStockItemRepository _itemRepo;
    private readonly IStockLocationRepository _locationRepo;
    private readonly IUnitConverter _unitConverter;

    public StockMovementService(
        IStockMovementRepository movementRepo,
        IStockItemRepository itemRepo,
        IStockLocationRepository locationRepo,
        IUnitConverter unitConverter)
    {
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
        _unitConverter = unitConverter ?? throw new ArgumentNullException(nameof(unitConverter));
    }

    public async Task<StockMovement> RecordMovementAsync(
        Guid stockItemId,
        Guid stockLocationId,
        StockMovementType movementType,
        decimal quantity,
        string unitCode,
        string sourceType,
        MovementDirection? direction = null,
        Guid? sourceReferenceId = null,
        string? reason = null,
        Guid? createdBy = null,
        CancellationToken ct = default)
    {
        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));

        if (stockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(stockLocationId));

        if (quantity <= 0m)
            throw new InvalidStockMovementException($"Movement quantity must be strictly greater than zero, got {quantity}.");

        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("Unit code cannot be empty.", nameof(unitCode));

        var item = await _itemRepo.GetByIdAsync(stockItemId, ct)
            ?? throw new StockItemNotFoundException(stockItemId);

        item.EnsureActiveForMovement();

        var location = await _locationRepo.GetByIdAsync(stockLocationId, ct)
            ?? throw new StockLocationNotFoundException(stockLocationId);

        location.EnsureActiveForMovement();

        // Dimension safety check against item's tracking unit
        var normalizedUnit = unitCode.Trim().ToLowerInvariant();
        if (!string.Equals(normalizedUnit, item.TrackingUnitCode, StringComparison.OrdinalIgnoreCase))
        {
            if (!_unitConverter.CanConvert(normalizedUnit, item.TrackingUnitCode))
            {
                var fromDim = _unitConverter.GetDimension(normalizedUnit);
                var toDim = _unitConverter.GetDimension(item.TrackingUnitCode);
                throw new IncompatibleUnitDimensionException(normalizedUnit, fromDim, item.TrackingUnitCode, toDim);
            }
        }

        // Reversal checks if called directly with Reversal type
        if (movementType == StockMovementType.Reversal)
        {
            if (!sourceReferenceId.HasValue || sourceReferenceId.Value == Guid.Empty)
            {
                throw new InvalidStockMovementException("Reversal movement must reference a valid source movement ID.");
            }

            if (await _movementRepo.HasReversalAsync(sourceReferenceId.Value, ct))
            {
                throw new DuplicateReversalException($"Movement '{sourceReferenceId.Value}' has already been reversed.");
            }
        }

        var movement = StockMovement.Create(
            stockItemId: item.Id,
            stockLocationId: location.Id,
            movementType: movementType,
            quantity: quantity,
            unitCode: normalizedUnit,
            sourceType: sourceType,
            direction: direction,
            sourceReferenceId: sourceReferenceId,
            reason: reason,
            createdBy: createdBy);

        await _movementRepo.AppendAsync(movement, ct);
        return movement;
    }

    public async Task<StockMovement> ReverseMovementAsync(
        Guid originalMovementId,
        string? reason = null,
        Guid? createdBy = null,
        CancellationToken ct = default)
    {
        if (originalMovementId == Guid.Empty)
            throw new ArgumentException("Original movement ID cannot be empty.", nameof(originalMovementId));

        var original = await _movementRepo.GetByIdAsync(originalMovementId, ct)
            ?? throw new StockMovementNotFoundException(originalMovementId);

        if (original.MovementType == StockMovementType.Reversal)
        {
            throw new InvalidStockMovementException("Cannot reverse a reversal movement.");
        }

        if (await _movementRepo.HasReversalAsync(originalMovementId, ct))
        {
            throw new DuplicateReversalException($"Movement '{originalMovementId}' has already been reversed.");
        }

        var item = await _itemRepo.GetByIdAsync(original.StockItemId, ct)
            ?? throw new StockItemNotFoundException(original.StockItemId);

        item.EnsureActiveForMovement();

        var location = await _locationRepo.GetByIdAsync(original.StockLocationId, ct)
            ?? throw new StockLocationNotFoundException(original.StockLocationId);

        location.EnsureActiveForMovement();

        var reversal = StockMovement.CreateReversal(original, reason, createdBy);

        await _movementRepo.AppendAsync(reversal, ct);
        return reversal;
    }
}
