using Npgsql;

namespace ALKAROS.Inventory.ModifierStock;

/// <summary>
/// V1-RMD-152: Postgres-backed modifier BOM, deliberately shaped like
/// <see cref="ALKAROS.Inventory.StockMaster.PostgresProductStockMappingRepository"/>.
/// </summary>
public sealed class PostgresModifierStockMappingRepository : IModifierStockMappingRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresModifierStockMappingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AddOrUpdateAsync(ModifierStockMapping mapping, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        const string sql = @"
            INSERT INTO inventory.modifier_stock_mappings (modifier_id, stock_item_id, quantity_multiplier, notes, created_at)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (modifier_id, stock_item_id) DO UPDATE
            SET quantity_multiplier = EXCLUDED.quantity_multiplier,
                notes = EXCLUDED.notes;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(mapping.ModifierId);
        cmd.Parameters.AddWithValue(mapping.StockItemId);
        cmd.Parameters.AddWithValue(mapping.QuantityMultiplier);
        cmd.Parameters.AddWithValue((object?)mapping.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue(mapping.CreatedAt);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public Task<IReadOnlyList<ModifierStockMapping>> GetByModifierIdAsync(Guid modifierId, CancellationToken ct = default)
        => GetByModifierIdsAsync([modifierId], ct);

    public async Task<IReadOnlyList<ModifierStockMapping>> GetByModifierIdsAsync(
        IReadOnlyCollection<Guid> modifierIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(modifierIds);
        if (modifierIds.Count == 0)
            return [];

        var sql = $@"
            SELECT modifier_id, stock_item_id, quantity_multiplier, notes, created_at
            FROM inventory.modifier_stock_mappings
            WHERE modifier_id = ANY($1)
            ORDER BY modifier_id, stock_item_id
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(modifierIds.ToArray());

        var list = new List<ModifierStockMapping>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ModifierStockMapping(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"Modifier stock mapping read exceeded {MaxUnpagedRows} rows; the filter is too broad.");
        }

        return list;
    }

    public async Task<bool> RemoveAsync(Guid modifierId, Guid stockItemId, CancellationToken ct = default)
    {
        const string sql = @"
            DELETE FROM inventory.modifier_stock_mappings
            WHERE modifier_id = $1 AND stock_item_id = $2;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(modifierId);
        cmd.Parameters.AddWithValue(stockItemId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
