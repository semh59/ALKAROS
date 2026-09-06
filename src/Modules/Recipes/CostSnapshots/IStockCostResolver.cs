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
        // Moving average: sum(quantity * unit_cost) / sum(quantity) from posted PurchaseReceipt movements
        // In inventory.stock_movements, movements carry quantity and total_cost (or unit_cost)
        // Let's inspect inventory.stock_movements:
        // We know movements have quantity, unit_cost or we can calculate average unit cost.
        // Wait, earlier we saw inventory.stock_movements schema:
        // (stock_movement_id, stock_item_id, stock_location_id, movement_type, direction, quantity, unit_code, source_type, source_reference_id, reason, created_at)
        // Wait! In goods_receipt_items or purchase_order_lines, unit_price is stored!
        // We can query goods_receipt_items for this stock_item up to asOfDate:
        const string sql = @"
SELECT COALESCE(SUM(gri.accepted_quantity * gri.unit_price) / NULLIF(SUM(gri.accepted_quantity), 0), 0)
FROM purchasing.goods_receipt_items gri
JOIN purchasing.goods_receipts gr ON gr.receipt_id = gri.receipt_id
WHERE gri.stock_item_id = $1
  AND gri.accepted_quantity > 0
  AND CAST(gr.received_at AS date) <= $2;";

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
