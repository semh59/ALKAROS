using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Measurements;

namespace ALKAROS.Inventory.ManualAdjustments;

public sealed class InventoryAdjustmentService : IInventoryAdjustmentService
{
    private readonly IInventoryTransactionRunner _transactionRunner;
    private readonly IStockMovementRepository _movementRepo;
    private readonly IStockItemRepository _itemRepo;
    private readonly IStockLocationRepository _locationRepo;
    private readonly IStockBalanceRepository _balanceRepo;
    private readonly IUnitConverter _unitConverter;

    public InventoryAdjustmentService(
        IInventoryTransactionRunner transactionRunner,
        IStockMovementRepository movementRepo,
        IStockItemRepository itemRepo,
        IStockLocationRepository locationRepo,
        IStockBalanceRepository balanceRepo,
        IUnitConverter unitConverter)
    {
        _transactionRunner = transactionRunner ?? throw new ArgumentNullException(nameof(transactionRunner));
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
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

        // Fast, friendly pre-check against the current on-hand balance — NOT
        // the final authority. Two concurrent decreases could both read the
        // same stale balance here and both pass; the guarded transactional
        // apply below (V1-RMD-125) is what actually prevents a negative
        // outcome under a race.
        var currentBalance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, location.Id, ct);
        var currentOnHand = currentBalance?.OnHandQuantity ?? 0m;

        var movementDirection = request.Direction == AdjustmentDirection.Increase
            ? MovementDirection.In
            : MovementDirection.Out;
        var signedDelta = movementDirection == MovementDirection.In ? quantityInTrackingUnit : -quantityInTrackingUnit;

        // Non-negative outcome invariant ("olumsuz olmayan sonuç")
        if (movementDirection == MovementDirection.Out && currentOnHand - quantityInTrackingUnit < 0m)
        {
            throw new NegativeInventoryResultException(
                $"Adjustment decrease of {quantityInTrackingUnit} {item.TrackingUnitCode} would result in negative on-hand balance ({currentOnHand - quantityInTrackingUnit}). Current on-hand is {currentOnHand} {item.TrackingUnitCode}.");
        }

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

        // Ledger append and the guarded balance apply commit as one unit:
        // the movement is appended, then TryApplyGuardedOnHandDeltaAsync
        // (V1-RMD-125) atomically re-checks the non-negative invariant
        // against the row's real, current value — closing the
        // read-then-write gap the fast pre-check above cannot. A guard
        // failure throws BalanceGuardFailedException so the transaction
        // runner rolls back the (otherwise orphaned) ledger append too.
        StockBalance updatedBalance;
        try
        {
            updatedBalance = await _transactionRunner.RunAsync(async (connection, transaction) =>
            {
                await _movementRepo.AppendAsync(movement, connection, transaction, ct);
                return await _balanceRepo.TryApplyGuardedOnHandDeltaAsync(
                    item.Id, location.Id, signedDelta, connection, transaction, ct)
                    ?? throw new BalanceGuardFailedException();
            }, ct);
        }
        catch (BalanceGuardFailedException)
        {
            var latest = await _balanceRepo.GetByItemAndLocationAsync(item.Id, location.Id, ct);
            var latestOnHand = latest?.OnHandQuantity ?? 0m;
            throw new NegativeInventoryResultException(
                $"Adjustment decrease of {quantityInTrackingUnit} {item.TrackingUnitCode} would result in negative on-hand balance ({latestOnHand - quantityInTrackingUnit}). Current on-hand is {latestOnHand} {item.TrackingUnitCode}.");
        }

        return new InventoryAdjustmentResult(
            movement,
            updatedBalance,
            currentOnHand,
            updatedBalance.OnHandQuantity);
    }
}
