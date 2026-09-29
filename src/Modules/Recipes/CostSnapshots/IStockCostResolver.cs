using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Recipes.CostSnapshots;

public interface IStockCostResolver
{
    Task<decimal?> ResolveMovingAverageCostAsync(Guid stockItemId, DateOnly asOfDate, CancellationToken ct = default);
}

public sealed class PostgresStockCostResolver : IStockCostResolver
{
    private static readonly Action<ILogger, Guid, Exception?> LogUndefinedGoodsReceiptTable =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(LogUndefinedGoodsReceiptTable)),
            "Moving average cost lookup for stock item {StockItemId} found purchasing.goods_receipt_items " +
            "undefined; returning null cost. This is expected if Purchasing has not been migrated yet.");

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresStockCostResolver>? _logger;

    public PostgresStockCostResolver(NpgsqlDataSource dataSource, ILogger<PostgresStockCostResolver>? logger = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _logger = logger;
    }

    public async Task<decimal?> ResolveMovingAverageCostAsync(Guid stockItemId, DateOnly asOfDate, CancellationToken ct = default)
    {
        // Weighted average purchase price, sum(accepted_quantity * unit_price) / sum(accepted_quantity), over every
        // goods receipt line of the stock item received on or before asOfDate. inventory.stock_movements carries no
        // price; the receipt lines do.
        // V1-RMD-423 (V1-RMD-398 G-08): "on or before asOfDate" is the restaurant's local calendar date. A plain
        // CAST(received_at AS date) used the database session zone (UTC in deployment), so a delivery at 01:30 in
        // Istanbul counted towards the previous day's cost.
        const string sql = @"
SELECT COALESCE(SUM(gri.accepted_quantity * gri.unit_price) / NULLIF(SUM(gri.accepted_quantity), 0), 0)
FROM purchasing.goods_receipt_items gri
JOIN purchasing.goods_receipts gr ON gr.receipt_id = gri.receipt_id
WHERE gri.stock_item_id = $1
  AND gri.accepted_quantity > 0
  AND (gr.received_at AT TIME ZONE 'Europe/Istanbul')::date <= $2;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(asOfDate);

        object? result;
        try
        {
            result = await cmd.ExecuteScalarAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Found by an independent audit (2026-09-06): this swallowed
            // "undefined_table" completely silently. purchasing.goods_receipt_items
            // only exists once Purchasing's own migrations have run (V11-PUR-001);
            // treating "no purchase history yet" as "no cost data" is a
            // reasonable business default, but a genuinely misconfigured
            // deployment (a schema that should exist but doesn't) deserves a
            // visible trace, not silence.
            if (_logger is not null)
                LogUndefinedGoodsReceiptTable(_logger, stockItemId, ex);
            return null;
        }

        if (result == null || result == DBNull.Value)
        {
            return null;
        }

        var avgCost = Convert.ToDecimal(result, System.Globalization.CultureInfo.InvariantCulture);
        return avgCost > 0 ? avgCost : null;
    }
}
