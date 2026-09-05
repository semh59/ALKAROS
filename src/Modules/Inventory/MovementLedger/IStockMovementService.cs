namespace ALKAROS.Inventory.MovementLedger;

public interface IStockMovementService
{
    Task<StockMovement> RecordMovementAsync(
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
        CancellationToken ct = default);

    Task<StockMovement> ReverseMovementAsync(
        Guid originalMovementId,
        string? reason = null,
        Guid? createdBy = null,
        CancellationToken ct = default);
}
