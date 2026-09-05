using Npgsql;

namespace ALKAROS.Inventory.StockMaster;

public sealed class PostgresProductStockMappingRepository : IProductStockMappingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresProductStockMappingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AddOrUpdateAsync(ProductStockMapping mapping, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        const string sql = @"
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier, notes, created_at)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (product_id, stock_item_id) DO UPDATE
            SET quantity_multiplier = EXCLUDED.quantity_multiplier,
                notes = EXCLUDED.notes;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(mapping.ProductId);
        cmd.Parameters.AddWithValue(mapping.StockItemId);
        cmd.Parameters.AddWithValue(mapping.QuantityMultiplier);
        cmd.Parameters.AddWithValue((object?)mapping.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue(mapping.CreatedAt);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ProductStockMapping>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT product_id, stock_item_id, quantity_multiplier, notes, created_at
            FROM inventory.product_stock_mappings
            WHERE product_id = $1
            ORDER BY stock_item_id;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(productId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ProductStockMapping>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProductStockMapping(
                productId: reader.GetGuid(0),
                stockItemId: reader.GetGuid(1),
                quantityMultiplier: reader.GetDecimal(2),
                notes: reader.IsDBNull(3) ? null : reader.GetString(3),
                createdAt: reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return list;
    }

    public async Task<IReadOnlyList<ProductStockMapping>> GetByStockItemIdAsync(Guid stockItemId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT product_id, stock_item_id, quantity_multiplier, notes, created_at
            FROM inventory.product_stock_mappings
            WHERE stock_item_id = $1
            ORDER BY product_id;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ProductStockMapping>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProductStockMapping(
                productId: reader.GetGuid(0),
                stockItemId: reader.GetGuid(1),
                quantityMultiplier: reader.GetDecimal(2),
                notes: reader.IsDBNull(3) ? null : reader.GetString(3),
                createdAt: reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return list;
    }

    public async Task RemoveAsync(Guid productId, Guid stockItemId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM inventory.product_stock_mappings WHERE product_id = $1 AND stock_item_id = $2;";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(productId);
        cmd.Parameters.AddWithValue(stockItemId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
