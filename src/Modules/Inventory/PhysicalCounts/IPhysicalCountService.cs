namespace ALKAROS.Inventory.PhysicalCounts;

public sealed record PhysicalCountRequest(
    Guid StockItemId,
    Guid StockLocationId,
    decimal CountedQuantity,
    Guid CountedByUserId,
    string? Notes);

public sealed record PhysicalCountResult(
    StockPhysicalCount Count,
    decimal PreviousOnHandQuantity,
    decimal NewOnHandQuantity);

public interface IPhysicalCountService
{
    Task<PhysicalCountResult> RecordPhysicalCountAsync(PhysicalCountRequest request, CancellationToken ct = default);
}
