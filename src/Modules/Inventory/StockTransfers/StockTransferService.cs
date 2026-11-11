using System.Security.Cryptography;
using System.Text;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using Npgsql;

namespace ALKAROS.Inventory.StockTransfers;

public sealed class StockTransferService(
    IInventoryTransactionRunner transactionRunner,
    IStockMovementRepository movementRepo,
    IStockItemRepository itemRepo,
    IStockLocationRepository locationRepo,
    IStockBalanceRepository balanceRepo) : IStockTransferService
{
    private const decimal MaxQuantity = 10_000_000_000m;

    public async Task<StockTransferResult> TransferAsync(StockTransferRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AuthorizedBy == Guid.Empty)
            throw new ArgumentException("An authorized operator is required.", nameof(request));
        if (request.Quantity <= 0m || request.Quantity >= MaxQuantity || decimal.Round(request.Quantity, 4) != request.Quantity)
            throw new InvalidStockTransferException("Transfer quantity must be positive, below 10 billion and have at most four decimals.");
        if (request.FromLocationId == request.ToLocationId)
            throw new InvalidStockTransferException("A transfer needs two different locations.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidStockTransferException("A transfer reason is mandatory.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new InvalidStockTransferException("A transfer needs an idempotency key.");

        var transferId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"stock-transfer:{request.IdempotencyKey}")).AsSpan(0, 16));
        if (await FindReplayAsync(transferId, request, ct) is { } replay)
            return replay;

        var item = await itemRepo.GetByIdAsync(request.StockItemId, ct) ?? throw new StockItemNotFoundException(request.StockItemId);
        item.EnsureActiveForMovement();
        (await locationRepo.GetByIdAsync(request.FromLocationId, ct) ?? throw new StockLocationNotFoundException(request.FromLocationId)).EnsureActiveForMovement();
        (await locationRepo.GetByIdAsync(request.ToLocationId, ct) ?? throw new StockLocationNotFoundException(request.ToLocationId)).EnsureActiveForMovement();

        var reason = request.Reason.Trim();
        StockMovement Leg(Guid locationId, MovementDirection direction) => StockMovement.Create(
            stockItemId: item.Id, stockLocationId: locationId, movementType: StockMovementType.Transfer,
            quantity: request.Quantity, unitCode: item.TrackingUnitCode, sourceType: StockMovementSourceType.StockTransfer,
            direction: direction, sourceReferenceId: transferId, reason: reason, createdBy: request.AuthorizedBy);

        try
        {
            var balances = await transactionRunner.RunAsync<(StockBalance From, StockBalance To)?>(async (connection, transaction) =>
            {
                await using var keyLock = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
                keyLock.Parameters.AddWithValue($"stock-transfer:{request.IdempotencyKey}");
                await keyLock.ExecuteNonQueryAsync(ct);
                if ((await movementRepo.GetBySourceAsync(StockMovementSourceType.StockTransfer, transferId, ct)).Count > 0)
                    return null;

                await movementRepo.AppendAsync(Leg(request.FromLocationId, MovementDirection.Out), connection, transaction, ct);
                await movementRepo.AppendAsync(Leg(request.ToLocationId, MovementDirection.In), connection, transaction, ct);

                // Rows are always locked in id order so two opposite transfers cannot wait on each other.
                StockBalance? from = null, to = null;
                foreach (var (locationId, delta) in new[] { (request.FromLocationId, -request.Quantity), (request.ToLocationId, request.Quantity) }.OrderBy(step => step.Item1))
                {
                    var applied = await balanceRepo.TryApplyGuardedOnHandDeltaAsync(item.Id, locationId, delta, connection, transaction, ct)
                        ?? throw new BalanceGuardFailedException();
                    if (delta < 0m && applied.AvailableQuantity < 0m)
                        throw new BalanceGuardFailedException();
                    if (locationId == request.FromLocationId) from = applied; else to = applied;
                }

                return (from!, to!);
            }, ct);

            return balances is { } done
                ? new StockTransferResult(transferId, done.From.OnHandQuantity, done.To.OnHandQuantity, false)
                : await FindReplayAsync(transferId, request, ct) ?? throw new InvalidOperationException("The transfer recorded under this key could not be read back.");
        }
        catch (BalanceGuardFailedException)
        {
            throw new InsufficientStockForTransferException("The source location does not hold that much stock.");
        }
    }

    private async Task<StockTransferResult?> FindReplayAsync(Guid transferId, StockTransferRequest request, CancellationToken ct)
    {
        var recorded = await movementRepo.GetBySourceAsync(StockMovementSourceType.StockTransfer, transferId, ct);
        if (recorded.Count == 0)
            return null;

        var matches = recorded.Count == 2
            && recorded.All(m => m.StockItemId == request.StockItemId && m.Quantity == request.Quantity)
            && recorded.Any(m => m.Direction == MovementDirection.Out && m.StockLocationId == request.FromLocationId)
            && recorded.Any(m => m.Direction == MovementDirection.In && m.StockLocationId == request.ToLocationId);
        if (!matches)
            throw new StockTransferKeyReusedException("This idempotency key was already used for a different transfer.");

        var from = await balanceRepo.GetByItemAndLocationAsync(request.StockItemId, request.FromLocationId, ct);
        var to = await balanceRepo.GetByItemAndLocationAsync(request.StockItemId, request.ToLocationId, ct);
        return new StockTransferResult(transferId, from?.OnHandQuantity ?? 0m, to?.OnHandQuantity ?? 0m, true);
    }
}
