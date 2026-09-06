using Npgsql;

namespace ALKAROS.Menu.StaticMenu;

public sealed class PostgresMenuItemRepository : IMenuItemRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresMenuItemRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(MenuItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        const string sql = @"
            INSERT INTO menu.menu_items (menu_item_id, menu_id, product_id, display_order, active, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7);";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(item.Id);
        cmd.Parameters.AddWithValue(item.MenuId);
        cmd.Parameters.AddWithValue(item.ProductId);
        cmd.Parameters.AddWithValue(item.DisplayOrder);
        cmd.Parameters.AddWithValue(item.IsActive);
        cmd.Parameters.AddWithValue(item.CreatedAt);
        cmd.Parameters.AddWithValue(item.UpdatedAt);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateMenuItemProductException(item.MenuId, item.ProductId);
        }
    }

    public async Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT menu_item_id, menu_id, product_id, display_order, active, created_at, updated_at
            FROM menu.menu_items
            WHERE menu_item_id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapMenuItem(reader);
    }

    public async Task<MenuItem?> GetByMenuAndProductAsync(Guid menuId, Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT menu_item_id, menu_id, product_id, display_order, active, created_at, updated_at
            FROM menu.menu_items
            WHERE menu_id = $1 AND product_id = $2;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(menuId);
        cmd.Parameters.AddWithValue(productId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapMenuItem(reader);
    }

    public async Task<IReadOnlyList<MenuItem>> GetByMenuIdAsync(Guid menuId, bool activeOnly = false, CancellationToken ct = default)
    {
        var sql = @"
            SELECT menu_item_id, menu_id, product_id, display_order, active, created_at, updated_at
            FROM menu.menu_items
            WHERE menu_id = $1" +
            (activeOnly ? " AND active = true" : "") +
            $" ORDER BY display_order ASC, created_at ASC LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(menuId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<MenuItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapMenuItem(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetByMenuIdAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task UpdateAsync(MenuItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        const string sql = @"
            UPDATE menu.menu_items
            SET display_order = $1, active = $2, updated_at = $3
            WHERE menu_item_id = $4;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(item.DisplayOrder);
        cmd.Parameters.AddWithValue(item.IsActive);
        cmd.Parameters.AddWithValue(item.UpdatedAt);
        cmd.Parameters.AddWithValue(item.Id);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
            throw new MenuItemNotFoundException(item.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM menu.menu_items WHERE menu_item_id = $1;";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ReorderAsync(Guid menuId, IReadOnlyList<Guid> orderedMenuItemIds, CancellationToken ct = default)
    {
        if (orderedMenuItemIds == null || orderedMenuItemIds.Count == 0)
            return;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        for (var i = 0; i < orderedMenuItemIds.Count; i++)
        {
            const string sql = @"
                UPDATE menu.menu_items
                SET display_order = $1, updated_at = NOW()
                WHERE menu_id = $2 AND menu_item_id = $3;";

            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue(i);
            cmd.Parameters.AddWithValue(menuId);
            cmd.Parameters.AddWithValue(orderedMenuItemIds[i]);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static MenuItem MapMenuItem(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetInt32(3),
            reader.GetBoolean(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6));
}
