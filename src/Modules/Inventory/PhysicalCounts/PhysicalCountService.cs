using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;

namespace ALKAROS.Inventory.PhysicalCounts;

/// <summary>
/// V11-INV-008: reuses <c>ManualAdjustments</c>' own guarded-transaction
/// pattern (<see cref="IInventoryTransactionRunner"/> +
/// <c>TryApplyGuardedOnHandDeltaAsync</c> + <see cref="StockMovementSourceType.InventoryAudit"/>)
/// rather than duplicating it, but is its own service: a physical count
/// records the ABSOLUTE quantity someone counted, not a relative delta a
/// caller supplies — the delta against the current balance is computed
/// here. Like a real stock take, this assumes no other movement happens
/// mid-count (the delta is computed against a balance read before the
/// transaction, same as <c>InventoryAdjustmentService</c>'s own fast
/// pre-check) — a genuine concurrent write mid-count is out of scope.
/// </summary>
public sealed class PhysicalCountService : IPhysicalCountService
{
    private readonly IInventoryTransactionRunner _transactionRunner;
    private readonly IStockItemRepository _itemRepo;
    private readonly IStockLocationRepository _locationRepo;
    private readonly IStockBalanceRepository _balanceRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly IPhysicalCountRepository _countRepo;

    public PhysicalCountService(
        IInventoryTransactionRunner transactionRunner,
        IStockItemRepository itemRepo,
        IStockLocationRepository locationRepo,
        IStockBalanceRepository balanceRepo,
        IStockMovementRepository movementRepo,
        IPhysicalCountRepository countRepo)
    {
        _transactionRunner = transactionRunner ?? throw new ArgumentNullException(nameof(transactionRunner));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        _countRepo = countRepo ?? throw new ArgumentNullException(nameof(countRepo));
    }

    public async Task<PhysicalCountResult> RecordPhysicalCountAsync(PhysicalCountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(request));
        if (request.StockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(request));
        if (request.CountedQuantity < 0m)
            throw new ArgumentOutOfRangeException(nameof(request), "Counted quantity cannot be negative.");
        if (request.CountedByUserId == Guid.Empty)
            throw new ArgumentException("CountedByUserId cannot be empty.", nameof(request));

        var item = await _itemRepo.GetByIdAsync(request.StockItemId, ct)
            ?? throw new StockItemNotFoundException(request.StockItemId);
        item.EnsureActiveForMovement();

        var location = await _locationRepo.GetByIdAsync(request.StockLocationId, ct)
            ?? throw new StockLocationNotFoundException(request.StockLocationId);
        location.EnsureActiveForMovement();

        var currentBalance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, location.Id, ct);
        var currentOnHand = currentBalance?.OnHandQuantity ?? 0m;
        var delta = request.CountedQuantity - currentOnHand;
        var countId = Guid.NewGuid();

        StockPhysicalCount? recordedCount = null;

        var newOnHand = await _transactionRunner.RunAsync(async (connection, transaction) =>
        {
            var onHandAfter = currentOnHand;
            Guid? resultingMovementId = null;

            if (delta != 0m)
            {
                var direction = delta > 0m ? MovementDirection.In : MovementDirection.Out;
                var quantity = Math.Abs(delta);

                var movement = StockMovement.Create(
                    stockItemId: item.Id,
                    stockLocationId: location.Id,
                    movementType: StockMovementType.Adjustment,
                    quantity: quantity,
                    unitCode: item.TrackingUnitCode,
                    sourceType: StockMovementSourceType.InventoryAudit,
                    direction: direction,
                    reason: $"Physical count: {request.CountedQuantity} {item.TrackingUnitCode} counted (was {currentOnHand}).",
                    createdBy: request.CountedByUserId);
                await _movementRepo.AppendAsync(movement, connection, transaction, ct);

                var signedDelta = direction == MovementDirection.In ? quantity : -quantity;
                var applied = await _balanceRepo.TryApplyGuardedOnHandDeltaAsync(
                    item.Id, location.Id, signedDelta, connection, transaction, ct);
                if (applied is null)
                {
                    throw new PhysicalCountBalanceGuardFailedException(
                        $"Applying physical count for stock item '{item.Name}' would have driven on-hand negative under a concurrent write.");
                }

                resultingMovementId = movement.Id;
                onHandAfter = applied.OnHandQuantity;
            }

            recordedCount = new StockPhysicalCount(
                id: countId,
                stockItemId: item.Id,
                stockLocationId: location.Id,
                countedQuantity: request.CountedQuantity,
                previousOnHandQuantity: currentOnHand,
                countedByUserId: request.CountedByUserId,
                notes: request.Notes,
                resultingMovementId: resultingMovementId);
            await _countRepo.AppendAsync(recordedCount, connection, transaction, ct);

            return onHandAfter;
        }, ct);

        return new PhysicalCountResult(recordedCount!, currentOnHand, newOnHand);
    }
}
