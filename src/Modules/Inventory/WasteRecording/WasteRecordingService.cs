using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Measurements;

namespace ALKAROS.Inventory.WasteRecording;

public sealed class WasteRecordingService : IWasteRecordingService
{
    private readonly IWasteRecordRepository _wasteRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly IStockItemRepository _itemRepo;
    private readonly IStockLocationRepository _locationRepo;
    private readonly IStockBalanceRepository _balanceRepo;
    private readonly IStockBalanceProjector _balanceProjector;
    private readonly IUnitConverter _unitConverter;

    public WasteRecordingService(
        IWasteRecordRepository wasteRepo,
        IStockMovementRepository movementRepo,
        IStockItemRepository itemRepo,
        IStockLocationRepository locationRepo,
        IStockBalanceRepository balanceRepo,
        IStockBalanceProjector balanceProjector,
        IUnitConverter unitConverter)
    {
        _wasteRepo = wasteRepo ?? throw new ArgumentNullException(nameof(wasteRepo));
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
        _balanceProjector = balanceProjector ?? throw new ArgumentNullException(nameof(balanceProjector));
        _unitConverter = unitConverter ?? throw new ArgumentNullException(nameof(unitConverter));
    }

    public async Task<WasteRecordingResult> RecordWasteAsync(
        RecordWasteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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
            var existingRecord = await _wasteRepo.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
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

        // Check on-hand balance to enforce non-negative on-hand invariant
        var balance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, location.Id, cancellationToken);
        var currentOnHand = balance?.OnHandQuantity ?? 0m;
        if (currentOnHand < normalizedQuantity)
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

        await _movementRepo.AppendAsync(movement, cancellationToken);
        await _wasteRepo.InsertAsync(wasteRecord, cancellationToken);
        await _balanceProjector.ApplyMovementAsync(movement, cancellationToken);

        return new WasteRecordingResult(wasteRecord, movement, IsIdempotentReplay: false);
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
