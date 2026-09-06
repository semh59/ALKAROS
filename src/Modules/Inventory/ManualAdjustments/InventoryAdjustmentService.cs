using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Measurements;

namespace ALKAROS.Inventory.ManualAdjustments;

public sealed class InventoryAdjustmentService : IInventoryAdjustmentService
{
    private readonly IStockMovementRepository _movementRepo;
    private readonly IStockItemRepository _itemRepo;
    private readonly IStockLocationRepository _locationRepo;
    private readonly IStockBalanceRepository _balanceRepo;
    private readonly IStockBalanceProjector _balanceProjector;
    private readonly IUnitConverter _unitConverter;

    public InventoryAdjustmentService(
        IStockMovementRepository movementRepo,
        IStockItemRepository itemRepo,
        IStockLocationRepository locationRepo,
        IStockBalanceRepository balanceRepo,
        IStockBalanceProjector balanceProjector,
        IUnitConverter unitConverter)
    {
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
        _balanceProjector = balanceProjector ?? throw new ArgumentNullException(nameof(balanceProjector));
        _unitConverter = unitConverter ?? throw new ArgumentNullException(nameof(unitConverter));
    }

    public async Task<InventoryAdjustmentResult> AdjustInventoryAsync(
        InventoryAdjustmentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(request));

        if (request.StockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(request));

        if (request.Quantity <= 0m)
            throw new InvalidAdjustmentQuantityException($"Adjustment quantity must be strictly positive, got {request.Quantity}.");

        if (string.IsNullOrWhiteSpace(request.UnitCode))
            throw new ArgumentException("UnitCode cannot be empty.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidAdjustmentReasonException("Adjustment reason is mandatory and cannot be empty.");

        if (request.AuthorizedBy == Guid.Empty)
            throw new UnauthorizedAdjustmentException("Adjustment requires an authorized operator/manager ID.");

        var item = await _itemRepo.GetByIdAsync(request.StockItemId, ct)
            ?? throw new StockItemNotFoundException(request.StockItemId);

        item.EnsureActiveForMovement();

        var location = await _locationRepo.GetByIdAsync(request.StockLocationId, ct)
            ?? throw new StockLocationNotFoundException(request.StockLocationId);

        location.EnsureActiveForMovement();

        // Dimension safety check against item's tracking unit
        var requestedUnit = request.UnitCode.Trim().ToLowerInvariant();
        decimal quantityInTrackingUnit = request.Quantity;

        if (!string.Equals(requestedUnit, item.TrackingUnitCode, StringComparison.OrdinalIgnoreCase))
        {
            if (!_unitConverter.CanConvert(requestedUnit, item.TrackingUnitCode))
            {
                var fromDim = _unitConverter.GetDimension(requestedUnit);
                var toDim = _unitConverter.GetDimension(item.TrackingUnitCode);
                throw new IncompatibleUnitDimensionException(requestedUnit, fromDim, item.TrackingUnitCode, toDim);
            }
            quantityInTrackingUnit = _unitConverter.Convert(request.Quantity, requestedUnit, item.TrackingUnitCode);
        }

        // Fetch current on-hand balance to check non-negative constraint
        var currentBalance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, location.Id, ct);
        var currentOnHand = currentBalance?.OnHandQuantity ?? 0m;

        var movementDirection = request.Direction == AdjustmentDirection.Increase
            ? MovementDirection.In
            : MovementDirection.Out;

        // Non-negative outcome invariant ("olumsuz olmayan sonuç")
        if (movementDirection == MovementDirection.Out)
        {
            if (currentOnHand - quantityInTrackingUnit < 0m)
            {
                throw new NegativeInventoryResultException(
                    $"Adjustment decrease of {quantityInTrackingUnit} {item.TrackingUnitCode} would result in negative on-hand balance ({currentOnHand - quantityInTrackingUnit}). Current on-hand is {currentOnHand} {item.TrackingUnitCode}.");
            }
        }

        // Record adjustment movement in ledger
        var movement = StockMovement.Create(
            stockItemId: item.Id,
            stockLocationId: location.Id,
            movementType: StockMovementType.Adjustment,
            quantity: quantityInTrackingUnit,
            unitCode: item.TrackingUnitCode,
            sourceType: StockMovementSourceType.InventoryAudit,
            direction: movementDirection,
            reason: request.Reason.Trim(),
            createdBy: request.AuthorizedBy);

        await _movementRepo.AppendAsync(movement, ct);

        // Project balance update
        var updatedBalance = await _balanceProjector.ApplyMovementAsync(movement, ct);
        if (updatedBalance == null)
        {
            updatedBalance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, location.Id, ct)
                ?? throw new InvalidOperationException("Failed to retrieve updated balance after adjustment.");
        }

        return new InventoryAdjustmentResult(
            movement,
            updatedBalance,
            currentOnHand,
            updatedBalance.OnHandQuantity);
    }
}
