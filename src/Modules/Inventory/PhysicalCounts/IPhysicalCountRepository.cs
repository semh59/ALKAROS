using Npgsql;

namespace ALKAROS.Inventory.PhysicalCounts;

public interface IPhysicalCountRepository
{
    /// <summary>
    /// Appends the count using the caller's own connection/transaction, so
    /// it commits atomically with the resulting balance adjustment (when
    /// there is one) — same shape as <c>IStockMovementRepository.AppendAsync</c>.
    /// </summary>
    Task AppendAsync(
        StockPhysicalCount count,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default);

    /// <summary>
    /// The most recent count for this item/location at or before
    /// <paramref name="at"/> — the shape V11-RPT-003's actual-vs-theoretical
    /// report will use to resolve a period's opening/closing count.
    /// </summary>
    Task<StockPhysicalCount?> GetMostRecentBeforeAsync(
        Guid stockItemId, Guid stockLocationId, DateTimeOffset at, CancellationToken ct = default);
}
