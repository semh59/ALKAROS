using System.Text.Json;
using Npgsql;

namespace ALKAROS.Menu.DailyMenuLifecycle;

public sealed class PostgresDailyMenuRepository : IDailyMenuRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDailyMenuRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(DailyMenu menu, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(menu);

        const string sql = @"
            INSERT INTO menu.daily_menus (
                daily_menu_id, business_date, status, opened_at, closed_at, closed_by, note, row_version, created_at, updated_at
            ) VALUES (
                $1, $2, $3, $4, $5, $6, $7, $8, $9, $10
            );";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(menu.Id);
        cmd.Parameters.AddWithValue(menu.BusinessDate);
        cmd.Parameters.AddWithValue(menu.Status.ToString());
        cmd.Parameters.AddWithValue(menu.OpenedAt.HasValue ? menu.OpenedAt.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(menu.ClosedAt.HasValue ? menu.ClosedAt.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(menu.ClosedBy.HasValue ? menu.ClosedBy.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(menu.Note ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue(menu.RowVersion);
        cmd.Parameters.AddWithValue(menu.CreatedAt);
        cmd.Parameters.AddWithValue(menu.UpdatedAt);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateDailyMenuBusinessDateException(menu.BusinessDate);
        }
    }

    public async Task<DailyMenu?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT daily_menu_id, business_date, status, opened_at, closed_at, closed_by, note, row_version, created_at, updated_at
            FROM menu.daily_menus
            WHERE daily_menu_id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapMenu(reader);
    }

    public async Task<DailyMenu?> GetByBusinessDateAsync(DateOnly businessDate, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT daily_menu_id, business_date, status, opened_at, closed_at, closed_by, note, row_version, created_at, updated_at
            FROM menu.daily_menus
            WHERE business_date = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(businessDate);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapMenu(reader);
    }

    public async Task UpdateAsync(DailyMenu menu, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(menu);

        const string sql = @"
            UPDATE menu.daily_menus
            SET status = $1, opened_at = $2, closed_at = $3, closed_by = $4, note = $5, row_version = $6, updated_at = $7
            WHERE daily_menu_id = $8;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(menu.Status.ToString());
        cmd.Parameters.AddWithValue(menu.OpenedAt.HasValue ? menu.OpenedAt.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(menu.ClosedAt.HasValue ? menu.ClosedAt.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(menu.ClosedBy.HasValue ? menu.ClosedBy.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(menu.Note ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue(menu.RowVersion);
        cmd.Parameters.AddWithValue(menu.UpdatedAt);
        cmd.Parameters.AddWithValue(menu.Id);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
            throw new DailyMenuNotFoundException(menu.Id);
    }

    public async Task<IReadOnlyList<DailyMenu>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT daily_menu_id, business_date, status, opened_at, closed_at, closed_by, note, row_version, created_at, updated_at
            FROM menu.daily_menus
            ORDER BY business_date DESC;";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<DailyMenu>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapMenu(reader));
        }

        return list;
    }

    private static DailyMenu MapMenu(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetFieldValue<DateOnly>(1),
            Enum.Parse<DailyMenuStatus>(reader.GetString(2)),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetInt32(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetFieldValue<DateTimeOffset>(9));
}

public sealed class PostgresDailyMenuItemRepository : IDailyMenuItemRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDailyMenuItemRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(DailyMenuItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        const string sql = @"
            INSERT INTO menu.daily_menu_items (
                daily_menu_item_id, daily_menu_id, product_id, product_name_snapshot,
                recipe_version_id, price, planned_portions, prepared_portions,
                available_portions, reserved_portions, consumed_portions,
                waste_portions, out_of_stock, printer_route_policy, active,
                created_at, updated_at
            ) VALUES (
                $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17
            );";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(item.Id);
        cmd.Parameters.AddWithValue(item.DailyMenuId);
        cmd.Parameters.AddWithValue(item.ProductId);
        cmd.Parameters.AddWithValue(item.ProductNameSnapshot);
        cmd.Parameters.AddWithValue(item.RecipeVersionId.HasValue ? item.RecipeVersionId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(item.Price);
        cmd.Parameters.AddWithValue(item.PlannedPortions);
        cmd.Parameters.AddWithValue(item.PreparedPortions);
        cmd.Parameters.AddWithValue(item.AvailablePortions);
        cmd.Parameters.AddWithValue(item.ReservedPortions);
        cmd.Parameters.AddWithValue(item.ConsumedPortions);
        cmd.Parameters.AddWithValue(item.WastePortions);
        cmd.Parameters.AddWithValue(item.IsOutOfStock);
        cmd.Parameters.AddWithValue(item.PrinterRoutePolicy ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue(item.IsActive);
        cmd.Parameters.AddWithValue(item.CreatedAt);
        cmd.Parameters.AddWithValue(item.UpdatedAt);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateDailyMenuItemException(item.DailyMenuId, item.ProductId);
        }
    }

    public async Task<DailyMenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT daily_menu_item_id, daily_menu_id, product_id, product_name_snapshot,
                   recipe_version_id, price, planned_portions, prepared_portions,
                   available_portions, reserved_portions, consumed_portions,
                   waste_portions, out_of_stock, printer_route_policy, active,
                   created_at, updated_at
            FROM menu.daily_menu_items
            WHERE daily_menu_item_id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapItem(reader);
    }

    public async Task<DailyMenuItem?> GetByMenuAndProductAsync(Guid dailyMenuId, Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT daily_menu_item_id, daily_menu_id, product_id, product_name_snapshot,
                   recipe_version_id, price, planned_portions, prepared_portions,
                   available_portions, reserved_portions, consumed_portions,
                   waste_portions, out_of_stock, printer_route_policy, active,
                   created_at, updated_at
            FROM menu.daily_menu_items
            WHERE daily_menu_id = $1 AND product_id = $2;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(dailyMenuId);
        cmd.Parameters.AddWithValue(productId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapItem(reader);
    }

    public async Task<IReadOnlyList<DailyMenuItem>> GetByDailyMenuIdAsync(Guid dailyMenuId, bool activeOnly = false, CancellationToken ct = default)
    {
        var sql = @"
            SELECT daily_menu_item_id, daily_menu_id, product_id, product_name_snapshot,
                   recipe_version_id, price, planned_portions, prepared_portions,
                   available_portions, reserved_portions, consumed_portions,
                   waste_portions, out_of_stock, printer_route_policy, active,
                   created_at, updated_at
            FROM menu.daily_menu_items
            WHERE daily_menu_id = $1" +
            (activeOnly ? " AND active = true" : "") +
            " ORDER BY created_at ASC;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(dailyMenuId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DailyMenuItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapItem(reader));
        }

        return list;
    }

    public async Task UpdateAsync(DailyMenuItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        const string sql = @"
            UPDATE menu.daily_menu_items
            SET price = $1, planned_portions = $2, active = $3, updated_at = $4
            WHERE daily_menu_item_id = $5;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(item.Price);
        cmd.Parameters.AddWithValue(item.PlannedPortions);
        cmd.Parameters.AddWithValue(item.IsActive);
        cmd.Parameters.AddWithValue(item.UpdatedAt);
        cmd.Parameters.AddWithValue(item.Id);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
            throw new DailyMenuItemNotFoundException(item.Id);
    }

    private static DailyMenuItem MapItem(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9),
            reader.GetDecimal(10),
            reader.GetDecimal(11),
            reader.GetBoolean(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.GetBoolean(14),
            reader.GetFieldValue<DateTimeOffset>(15),
            reader.GetFieldValue<DateTimeOffset>(16));
}

public sealed class PostgresDailyMenuItemHistoryRepository : IDailyMenuItemHistoryRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDailyMenuItemHistoryRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(DailyMenuItemHistory history, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        const string sql = @"
            INSERT INTO menu.daily_menu_item_history (
                daily_menu_item_history_id, daily_menu_item_id, old_value, new_value, changed_by, changed_at
            ) VALUES (
                $1, $2, $3::jsonb, $4::jsonb, $5, $6
            );";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(history.Id);
        cmd.Parameters.AddWithValue(history.DailyMenuItemId);
        cmd.Parameters.AddWithValue(history.OldValueJson);
        cmd.Parameters.AddWithValue(history.NewValueJson);
        cmd.Parameters.AddWithValue(history.ChangedBy.HasValue ? history.ChangedBy.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(history.ChangedAt);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<DailyMenuItemHistory>> GetByDailyMenuItemIdAsync(Guid dailyMenuItemId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT daily_menu_item_history_id, daily_menu_item_id, old_value, new_value, changed_by, changed_at
            FROM menu.daily_menu_item_history
            WHERE daily_menu_item_id = $1
            ORDER BY changed_at ASC;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(dailyMenuItemId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DailyMenuItemHistory>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DailyMenuItemHistory(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return list;
    }
}

public sealed class PostgresCatalogProductPriceReader : ICatalogProductPriceReader
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCatalogProductPriceReader(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CatalogProductPriceInfo?> GetProductPriceInfoAsync(Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT product_id, sku, name, current_price, active
            FROM catalog.products
            WHERE product_id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(productId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new CatalogProductPriceInfo(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetDecimal(3),
            reader.GetBoolean(4));
    }
}

public sealed class PostgresRecipeVersionValidator : IRecipeVersionValidator
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresRecipeVersionValidator(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<bool> IsRecipeVersionActiveAsync(Guid recipeVersionId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM recipe.recipe_versions
            WHERE id = $1 AND status = 'Active';";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(recipeVersionId);

        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return count > 0;
    }
}
