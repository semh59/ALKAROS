namespace ALKAROS.Inventory.MovementLedger;

public sealed class StockMovement
{
    public StockMovement(
        Guid id,
        Guid stockItemId,
        Guid stockLocationId,
        StockMovementType movementType,
        MovementDirection direction,
        decimal quantity,
        string unitCode,
        string sourceType,
        Guid? sourceReferenceId = null,
        string? reason = null,
        Guid? createdBy = null,
        DateTimeOffset? createdAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));

        if (stockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(stockLocationId));

        if (quantity <= 0m)
            throw new InvalidStockMovementException($"Movement quantity must be strictly greater than zero, got {quantity}.");

        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("Unit code cannot be empty.", nameof(unitCode));

        if (string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Source type cannot be empty.", nameof(sourceType));

        var trimmedSourceType = sourceType.Trim();
        if (!StockMovementSourceType.IsValid(trimmedSourceType))
        {
            throw new InvalidStockMovementException(
                $"Invalid source type '{trimmedSourceType}'. Valid source types are: {string.Join(", ", StockMovementSourceType.ValidSourceTypes)}.");
        }

        // Reversal invariant validation
        if (movementType == StockMovementType.Reversal)
        {
            if (!string.Equals(trimmedSourceType, StockMovementSourceType.StockMovement, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidStockMovementException(
                    $"Reversal movement must have source type '{StockMovementSourceType.StockMovement}', got '{trimmedSourceType}'.");
            }

            if (!sourceReferenceId.HasValue || sourceReferenceId.Value == Guid.Empty)
            {
                throw new InvalidStockMovementException("Reversal movement must specify a valid source_reference_id referencing the original movement.");
            }
        }
        else if (string.Equals(trimmedSourceType, StockMovementSourceType.StockMovement, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidStockMovementException($"Only Reversal movements may use '{StockMovementSourceType.StockMovement}' as source type.");
        }

        // Adjustment direction validation
        if (movementType == StockMovementType.Adjustment && direction is not (MovementDirection.In or MovementDirection.Out))
        {
            throw new InvalidStockMovementException($"Adjustment movement direction must be either In or Out, got {direction}.");
        }

        Id = id;
        StockItemId = stockItemId;
        StockLocationId = stockLocationId;
        MovementType = movementType;
        Direction = direction;
        Quantity = quantity;
        UnitCode = unitCode.Trim().ToLowerInvariant();
        SourceType = trimmedSourceType;
        SourceReferenceId = sourceReferenceId;
        Reason = reason?.Trim();
        CreatedBy = createdBy;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public Guid StockItemId { get; }
    public Guid StockLocationId { get; }
    public StockMovementType MovementType { get; }
    public MovementDirection Direction { get; }
    public decimal Quantity { get; }
    public string UnitCode { get; }
    public string SourceType { get; }
    public Guid? SourceReferenceId { get; }
    public string? Reason { get; }
    public Guid? CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }

    public StockEffect Effect => StockEffect.Calculate(MovementType, Direction, Quantity);

    public static MovementDirection ResolveDefaultDirection(StockMovementType type)
    {
        return type switch
        {
            StockMovementType.PurchaseReceipt => MovementDirection.In,
            StockMovementType.ProductionOutput => MovementDirection.In,
            StockMovementType.Return => MovementDirection.In,
            StockMovementType.Consumption => MovementDirection.Out,
            StockMovementType.Waste => MovementDirection.Out,
            StockMovementType.Reservation => MovementDirection.Reserve,
            StockMovementType.Release => MovementDirection.Release,
            _ => throw new InvalidStockMovementException($"Movement type {type} requires an explicit direction.")
        };
    }

    public static MovementDirection InvertDirection(MovementDirection direction)
    {
        return direction switch
        {
            MovementDirection.In => MovementDirection.Out,
            MovementDirection.Out => MovementDirection.In,
            MovementDirection.Reserve => MovementDirection.Release,
            MovementDirection.Release => MovementDirection.Reserve,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), $"Cannot invert direction: {direction}")
        };
    }

    public static StockMovement Create(
        Guid stockItemId,
        Guid stockLocationId,
        StockMovementType movementType,
        decimal quantity,
        string unitCode,
        string sourceType,
        MovementDirection? direction = null,
        Guid? sourceReferenceId = null,
        string? reason = null,
        Guid? createdBy = null)
    {
        var resolvedDirection = direction ?? ResolveDefaultDirection(movementType);

        return new StockMovement(
            id: Guid.NewGuid(),
            stockItemId: stockItemId,
            stockLocationId: stockLocationId,
            movementType: movementType,
            direction: resolvedDirection,
            quantity: quantity,
            unitCode: unitCode,
            sourceType: sourceType,
            sourceReferenceId: sourceReferenceId,
            reason: reason,
            createdBy: createdBy,
            createdAt: DateTimeOffset.UtcNow);
    }

    public static StockMovement CreateReversal(
        StockMovement originalMovement,
        string? reason = null,
        Guid? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(originalMovement);

        if (originalMovement.MovementType == StockMovementType.Reversal)
        {
            throw new InvalidStockMovementException("Cannot reverse a reversal movement.");
        }

        var invertedDirection = InvertDirection(originalMovement.Direction);

        return new StockMovement(
            id: Guid.NewGuid(),
            stockItemId: originalMovement.StockItemId,
            stockLocationId: originalMovement.StockLocationId,
            movementType: StockMovementType.Reversal,
            direction: invertedDirection,
            quantity: originalMovement.Quantity,
            unitCode: originalMovement.UnitCode,
            sourceType: StockMovementSourceType.StockMovement,
            sourceReferenceId: originalMovement.Id,
            reason: reason ?? $"Reversal of movement {originalMovement.Id}",
            createdBy: createdBy,
            createdAt: DateTimeOffset.UtcNow);
    }
}
