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
