namespace ALKAROS.Inventory.PhysicalCounts;

/// <summary>
/// V11-INV-008: what a manager/staff member actually counted on a shelf,
/// recorded regardless of whether it differed from the system's on-hand
/// balance (a match is still evidence — the future actual-vs-theoretical
/// report, V11-RPT-003, needs opening/closing counts even when nothing
/// moved). <see cref="ResultingMovementId"/> is set only when the count
/// differed and produced a real <c>StockMovementType.Adjustment</c>.
/// </summary>
public sealed class StockPhysicalCount
{
    public StockPhysicalCount(
        Guid id,
        Guid stockItemId,
        Guid stockLocationId,
        decimal countedQuantity,
        decimal previousOnHandQuantity,
        Guid countedByUserId,
        string? notes,
        Guid? resultingMovementId,
        DateTimeOffset? countedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));
        if (stockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(stockLocationId));
        if (countedQuantity < 0m)
            throw new ArgumentOutOfRangeException(nameof(countedQuantity), "Counted quantity cannot be negative.");
        if (countedByUserId == Guid.Empty)
            throw new ArgumentException("CountedByUserId cannot be empty.", nameof(countedByUserId));

        Id = id;
        StockItemId = stockItemId;
        StockLocationId = stockLocationId;
        CountedQuantity = countedQuantity;
        PreviousOnHandQuantity = previousOnHandQuantity;
        CountedByUserId = countedByUserId;
        Notes = notes?.Trim();
        ResultingMovementId = resultingMovementId;
        CountedAt = countedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public Guid StockItemId { get; }
    public Guid StockLocationId { get; }
    public decimal CountedQuantity { get; }
    public decimal PreviousOnHandQuantity { get; }
    public Guid CountedByUserId { get; }
    public string? Notes { get; }
    public Guid? ResultingMovementId { get; }
    public DateTimeOffset CountedAt { get; }

    public decimal Delta => CountedQuantity - PreviousOnHandQuantity;
}
