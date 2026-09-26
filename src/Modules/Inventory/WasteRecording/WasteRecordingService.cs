using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Measurements;
using Npgsql;

namespace ALKAROS.Inventory.WasteRecording;

public sealed class WasteRecordingService : IWasteRecordingService
{
    private readonly IInventoryTransactionRunner _transactionRunner;
    private readonly IWasteRecordRepository _wasteRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly IStockItemRepository _itemRepo;
    private readonly IStockLocationRepository _locationRepo;
    private readonly IStockBalanceRepository _balanceRepo;
    private readonly IUnitConverter _unitConverter;

    public WasteRecordingService(
        IInventoryTransactionRunner transactionRunner,
        IWasteRecordRepository wasteRepo,
        IStockMovementRepository movementRepo,
        IStockItemRepository itemRepo,
        IStockLocationRepository locationRepo,
        IStockBalanceRepository balanceRepo,
        IUnitConverter unitConverter)
    {
        _transactionRunner = transactionRunner ?? throw new ArgumentNullException(nameof(transactionRunner));
        _wasteRepo = wasteRepo ?? throw new ArgumentNullException(nameof(wasteRepo));
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
        _unitConverter = unitConverter ?? throw new ArgumentNullException(nameof(unitConverter));
    }

    public Task<WasteRecordingResult> RecordWasteAsync(
        RecordWasteRequest request,
        CancellationToken cancellationToken = default)
        => RecordWasteCoreAsync(request, null, null, cancellationToken);

    public Task<WasteRecordingResult> RecordWasteAsync(
        RecordWasteRequest request,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
        => RecordWasteCoreAsync(request, connection, transaction, cancellationToken);

    private async Task<WasteRecordingResult> RecordWasteCoreAsync(
        RecordWasteRequest request,
        NpgsqlConnection? callerConnection,
        NpgsqlTransaction? callerTransaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inCallerTransaction = callerTransaction is not null;

        if (request.StockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(request));

        if (request.StockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(request));

        if (request.Quantity <= 0m)
            throw new InvalidWasteQuantityException($"Waste quantity must be strictly positive, got {request.Quantity}.");

        if (string.IsNullOrWhiteSpace(request.UnitCode))
            throw new ArgumentException("UnitCode cannot be empty.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidWasteReasonException("Waste reason is mandatory and cannot be empty.");

        if (request.RecordedBy == Guid.Empty)
            throw new UnauthorizedWasteRecorderException("Waste recording requires an authorized staff/manager ID.");

        if (string.IsNullOrWhiteSpace(request.WasteSource) || !WasteSources.IsValid(request.WasteSource))
            throw new WasteRecordingException($"Invalid waste source '{request.WasteSource}'.");

        // Idempotency check: duplicate submissions with identical idempotency key return the existing record without duplicate movements
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingRecord = inCallerTransaction
                ? await _wasteRepo.GetByIdempotencyKeyAsync(request.IdempotencyKey, callerConnection!, callerTransaction!, cancellationToken)
                : await _wasteRepo.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
            if (existingRecord != null)
            {
                var existingMovement = await _movementRepo.GetByIdAsync(existingRecord.StockMovementId, cancellationToken);
                return new WasteRecordingResult(existingRecord, existingMovement!, IsIdempotentReplay: true);
            }
        }

        var item = await _itemRepo.GetByIdAsync(request.StockItemId, cancellationToken)
            ?? throw new WasteItemNotFoundException($"Stock item {request.StockItemId} not found.");

        item.EnsureActiveForMovement();

        var location = await _locationRepo.GetByIdAsync(request.StockLocationId, cancellationToken)
            ?? throw new WasteLocationNotFoundException($"Stock location {request.StockLocationId} not found.");

        location.EnsureActiveForMovement();

        // Dimension safety and conversion to tracking unit
        var requestedUnit = request.UnitCode.Trim().ToLowerInvariant();
        decimal normalizedQuantity = request.Quantity;

        if (!string.Equals(requestedUnit, item.TrackingUnitCode, StringComparison.OrdinalIgnoreCase))
        {
            if (!_unitConverter.CanConvert(requestedUnit, item.TrackingUnitCode))
            {
                throw new IncompatibleWasteUnitException(
                    $"Unit '{requestedUnit}' cannot be converted to tracking unit '{item.TrackingUnitCode}'.");
            }
            normalizedQuantity = _unitConverter.Convert(request.Quantity, requestedUnit, item.TrackingUnitCode);
        }

        // Fast, friendly pre-check — NOT the final authority. Two concurrent
        // waste recordings could both read the same stale balance here and
        // both pass; the guarded transactional apply below (V1-RMD-125) is
        // what actually prevents a negative outcome under a race.
        var balance = inCallerTransaction ? null : await _balanceRepo.GetByItemAndLocationAsync(item.Id, location.Id, cancellationToken);
        var currentOnHand = balance?.OnHandQuantity ?? 0m;
        if (!inCallerTransaction && currentOnHand < normalizedQuantity)
        {
            throw new InsufficientStockForWasteException(
                $"Insufficient stock for waste recording. Available on-hand: {currentOnHand} {item.TrackingUnitCode}, requested waste: {normalizedQuantity} {item.TrackingUnitCode}.");
        }

        var recordId = request.Id ?? Guid.NewGuid();
        var movementId = Guid.NewGuid();

        var movement = new StockMovement(
            id: movementId,
            stockItemId: item.Id,
            stockLocationId: location.Id,
            movementType: StockMovementType.Waste,
            direction: MovementDirection.Out,
            quantity: normalizedQuantity,
            unitCode: item.TrackingUnitCode,
            sourceType: StockMovementSourceType.WasteRecord,
            sourceReferenceId: recordId,
            reason: request.Reason.Trim(),
            createdBy: request.RecordedBy);

        var wasteRecord = new WasteRecord(
            id: recordId,
            stockMovementId: movement.Id,
            stockItemId: item.Id,
            stockLocationId: location.Id,
            wasteSource: request.WasteSource,
            quantity: request.Quantity,
            unitCode: requestedUnit,
            normalizedQuantity: normalizedQuantity,
            trackingUnitCode: item.TrackingUnitCode,
            wasteReason: request.Reason.Trim(),
            recordedBy: request.RecordedBy,
            recordedAt: DateTimeOffset.UtcNow,
            sourceReferenceId: request.SourceReferenceId,
            idempotencyKey: request.IdempotencyKey,
            metadataJson: request.MetadataJson);

        // The ledger append, the waste record insert, and the guarded
        // balance apply commit as one unit (V1-RMD-125) — previously these
        // were three independent round trips, so a failure between them
        // could leave an orphaned, immutable ledger row with no matching
        // WasteRecord, and the non-negative check was a separate, unlocked
        // read that a concurrent request could race past. A guard failure
        // throws BalanceGuardFailedException so the transaction runner
        // rolls back the ledger append and waste record insert too.
        async Task<StockBalance> WriteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            await _movementRepo.AppendAsync(movement, connection, transaction, cancellationToken);
            await _wasteRepo.InsertAsync(wasteRecord, connection, transaction, cancellationToken);

            return await _balanceRepo.TryApplyGuardedOnHandDeltaAsync(
                item.Id, location.Id, -normalizedQuantity, connection, transaction, cancellationToken)
                ?? throw new BalanceGuardFailedException();
        }

        if (inCallerTransaction)
        {
            // Inside the caller's transaction nothing can be read back after a failure (the transaction is the
            // caller's to roll back), so a guard failure is reported with the requested quantity only.
            try
            {
                await WriteAsync(callerConnection!, callerTransaction!);
            }
            catch (BalanceGuardFailedException)
            {
                throw new InsufficientStockForWasteException(
                    $"Insufficient stock for waste recording. Requested waste: {normalizedQuantity} {item.TrackingUnitCode}.");
            }

            return new WasteRecordingResult(wasteRecord, movement, IsIdempotentReplay: false);
        }

        try
        {
            await _transactionRunner.RunAsync(WriteAsync, cancellationToken);

            return new WasteRecordingResult(wasteRecord, movement, IsIdempotentReplay: false);
        }
        catch (BalanceGuardFailedException)
        {
            var latest = await _balanceRepo.GetByItemAndLocationAsync(item.Id, location.Id, cancellationToken);
            var latestOnHand = latest?.OnHandQuantity ?? 0m;
            throw new InsufficientStockForWasteException(
                $"Insufficient stock for waste recording. Available on-hand: {latestOnHand} {item.TrackingUnitCode}, requested waste: {normalizedQuantity} {item.TrackingUnitCode}.");
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "uq_waste_records_idempotency")
        {
            // A concurrent request with the identical idempotency key won
            // the race between our own pre-check (above) and this insert —
            // replay its result instead of surfacing a raw DB exception.
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existing = await _wasteRepo.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
                if (existing is not null)
                {
                    var existingMovement = await _movementRepo.GetByIdAsync(existing.StockMovementId, cancellationToken);
                    return new WasteRecordingResult(existing, existingMovement!, IsIdempotentReplay: true);
                }
            }

            throw;
        }
    }

    public Task<WasteRecord?> GetWasteRecordByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _wasteRepo.GetByIdAsync(id, cancellationToken);
    }

    public Task<WasteRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return _wasteRepo.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
    }

    public Task<IReadOnlyList<WasteRecord>> GetWasteRecordsBySourceAsync(string wasteSource, Guid sourceReferenceId, CancellationToken cancellationToken = default)
    {
        return _wasteRepo.GetBySourceAsync(wasteSource, sourceReferenceId, cancellationToken);
    }
}
