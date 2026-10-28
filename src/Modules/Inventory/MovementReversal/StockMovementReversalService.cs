using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using Npgsql;

namespace ALKAROS.Inventory.MovementReversal;

public sealed class StockMovementReversalService : IStockMovementReversalService
{
    private readonly IStockMovementRepository _movementRepo;
    private readonly IStockItemRepository _itemRepo;
    private readonly IStockLocationRepository _locationRepo;
    private readonly IStockBalanceProjector _balanceProjector;
    private readonly IStockBalanceRepository _balanceRepo;

    public StockMovementReversalService(
        IStockMovementRepository movementRepo,
        IStockItemRepository itemRepo,
        IStockLocationRepository locationRepo,
        IStockBalanceProjector balanceProjector,
        IStockBalanceRepository balanceRepo)
    {
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
        _balanceProjector = balanceProjector ?? throw new ArgumentNullException(nameof(balanceProjector));
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
    }

    public async Task<StockMovementReversalResult> ReverseMovementAsync(
        StockMovementReversalRequest request,
        CancellationToken ct = default)
    {
        var (reversal, original) = await PrepareReversalAsync(request, ct);

        try
        {
            await _movementRepo.AppendAsync(reversal, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // Database-level race protection via unique index uq_stock_movements_single_reversal
            throw new DuplicateReversalException($"Stock movement '{original.Id}' has already been reversed.");
        }

        var restoredBalance = await _balanceProjector.ApplyMovementAsync(reversal, ct);
        if (restoredBalance == null)
        {
            restoredBalance = await _balanceRepo.GetByItemAndLocationAsync(reversal.StockItemId, reversal.StockLocationId, ct)
                ?? throw new InvalidOperationException("Failed to retrieve restored stock balance.");
        }

        return new StockMovementReversalResult(reversal, original, restoredBalance);
    }

    public async Task<StockMovementReversalResult> ReverseMovementAsync(
        StockMovementReversalRequest request,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var (reversal, original) = await PrepareReversalAsync(request, ct);

        try
        {
            await _movementRepo.AppendAsync(reversal, connection, transaction, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new DuplicateReversalException($"Stock movement '{original.Id}' has already been reversed.");
        }

        var restoredBalance = await _balanceRepo.TryApplyGuardedOnHandDeltaAsync(
            reversal.StockItemId, reversal.StockLocationId, reversal.Effect.OnHandDelta, connection, transaction, ct)
            ?? throw new ReversalWouldMakeStockNegativeException(
                $"Reversing stock movement '{original.Id}' would drive the on-hand balance negative.");
        return new StockMovementReversalResult(reversal, original, restoredBalance);
    }

    public async Task<IReadOnlyList<StockMovement>> ReturnConsumedShareAsync(
        StockShareReturnRequest request,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (request.OrderItemId == Guid.Empty)
            throw new ArgumentException("Order item ID cannot be empty.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidReversalReasonException("Return reason cannot be empty or whitespace.");
        if (request.TotalQuantity <= 0m || request.ReturnedQuantity <= 0m || request.PreviouslyReturnedQuantity < 0m
            || request.PreviouslyReturnedQuantity + request.ReturnedQuantity > request.TotalQuantity)
            throw new InvalidStockMovementException(
                $"Returned quantity {request.ReturnedQuantity} after {request.PreviouslyReturnedQuantity} must be above zero and not exceed {request.TotalQuantity} in total.");

        var movements = await _movementRepo.GetBySourceAsync(StockMovementSourceType.Order, request.OrderItemId, ct);
        var consumptions = new List<StockMovement>();
        foreach (var movement in movements.Where(m => m.MovementType == StockMovementType.Consumption))
        {
            if (!await _movementRepo.HasReversalAsync(movement.Id, ct))
                consumptions.Add(movement);
        }

        var written = new List<StockMovement>();
        foreach (var group in consumptions.GroupBy(m => (m.StockItemId, m.StockLocationId)))
        {
            var consumed = group.Sum(m => m.Quantity);
            var alreadyReturned = movements
                .Where(m => m.MovementType == StockMovementType.Return
                            && m.StockItemId == group.Key.StockItemId && m.StockLocationId == group.Key.StockLocationId)
                .Sum(m => m.Quantity);
            var target = Math.Round(
                consumed * (request.PreviouslyReturnedQuantity + request.ReturnedQuantity) / request.TotalQuantity,
                4, MidpointRounding.AwayFromZero);
            var quantity = target - alreadyReturned;
            if (quantity <= 0m)
                continue;

            var first = group.First();
            var returned = StockMovement.Create(
                first.StockItemId, first.StockLocationId, StockMovementType.Return, quantity, first.UnitCode,
                StockMovementSourceType.Order, MovementDirection.In, request.OrderItemId, request.Reason.Trim(), request.ActorId);
            await _movementRepo.AppendAsync(returned, connection, transaction, ct);
            await _balanceRepo.ApplyOnHandDeltaAsync(
                returned.StockItemId, returned.StockLocationId, returned.Effect.OnHandDelta, connection, transaction, ct);
            written.Add(returned);
        }

        return written;
    }

    /// <summary>Validates the request and builds the reversal movement; writes nothing.</summary>
    private async Task<(StockMovement Reversal, StockMovement Original)> PrepareReversalAsync(
        StockMovementReversalRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OriginalMovementId == Guid.Empty)
            throw new ArgumentException("Original movement ID cannot be empty.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidReversalReasonException("Reversal reason cannot be empty or whitespace.");

        var original = await _movementRepo.GetByIdAsync(request.OriginalMovementId, ct)
            ?? throw new StockMovementNotFoundException(request.OriginalMovementId);

        if (original.MovementType == StockMovementType.Reversal)
        {
            throw new ReversalNotEligibleException($"Cannot reverse movement '{original.Id}' because it is already a Reversal.");
        }

        if (await _movementRepo.HasReversalAsync(original.Id, ct))
        {
            throw new DuplicateReversalException($"Stock movement '{original.Id}' has already been reversed.");
        }

        var item = await _itemRepo.GetByIdAsync(original.StockItemId, ct)
            ?? throw new StockItemNotFoundException(original.StockItemId);

        item.EnsureActiveForMovement();

        var location = await _locationRepo.GetByIdAsync(original.StockLocationId, ct)
            ?? throw new StockLocationNotFoundException(original.StockLocationId);

        location.EnsureActiveForMovement();

        var reversal = StockMovement.CreateReversal(
            originalMovement: original,
            reason: request.Reason.Trim(),
            createdBy: request.ActorId);
        return (reversal, original);
    }

    public async Task<bool> CanReverseAsync(
        Guid movementId,
        CancellationToken ct = default)
    {
        if (movementId == Guid.Empty)
            return false;

        var movement = await _movementRepo.GetByIdAsync(movementId, ct);
        if (movement == null || movement.MovementType == StockMovementType.Reversal)
            return false;

        var hasReversal = await _movementRepo.HasReversalAsync(movementId, ct);
        return !hasReversal;
    }

    public async Task<StockMovement?> GetReversalForMovementAsync(
        Guid originalMovementId,
        CancellationToken ct = default)
    {
        if (originalMovementId == Guid.Empty)
            return null;

        var movements = await _movementRepo.GetBySourceAsync(StockMovementSourceType.StockMovement, originalMovementId, ct);
        return movements.FirstOrDefault(m => m.MovementType == StockMovementType.Reversal);
    }
}
