namespace ALKAROS.Inventory.ManualAdjustments;

public interface IInventoryAdjustmentService
{
    Task<InventoryAdjustmentResult> AdjustInventoryAsync(
        InventoryAdjustmentRequest request,
        CancellationToken ct = default);
}
