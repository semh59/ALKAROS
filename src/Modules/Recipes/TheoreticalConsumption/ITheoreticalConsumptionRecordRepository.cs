using Npgsql;

namespace ALKAROS.Recipes.TheoreticalConsumption;

public interface ITheoreticalConsumptionRecordRepository
{
    /// <summary>
    /// Appends the record using the caller's own connection/transaction, so
    /// it commits atomically with the caller's Accept write — same shape as
    /// <c>IStockMovementRepository.AppendAsync</c> (V0-ARC-001 row 11's own
    /// same-transaction pattern).
    /// </summary>
    Task AppendAsync(
        TheoreticalConsumptionRecord record,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default);

    /// <summary>
    /// Total theoretical usage per stock item within [from, to) — the shape
    /// V11-RPT-003's actual-vs-theoretical report will aggregate against.
    /// </summary>
    Task<IReadOnlyList<TheoreticalConsumptionTotal>> GetTotalsByStockItemAsync(
        DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken ct = default);
}

public sealed record TheoreticalConsumptionTotal(Guid StockItemId, string UnitCode, decimal TotalQuantity);
