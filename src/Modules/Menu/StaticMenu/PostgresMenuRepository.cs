using Npgsql;

namespace ALKAROS.Menu.StaticMenu;

public sealed class PostgresMenuRepository : IMenuRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresMenuRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(Menu menu, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(menu);

        const string sql = @"
            INSERT INTO menu.menus (menu_id, code, name, active, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6);";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(menu.Id);
        cmd.Parameters.AddWithValue(menu.Code);
        cmd.Parameters.AddWithValue(menu.Name);
        cmd.Parameters.AddWithValue(menu.IsActive);
        cmd.Parameters.AddWithValue(menu.CreatedAt);
        cmd.Parameters.AddWithValue(menu.UpdatedAt);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateMenuCodeException(menu.Code);
        }
    }

    public async Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT menu_id, code, name, active, created_at, updated_at
            FROM menu.menus
            WHERE menu_id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapMenu(reader);
    }

    public async Task<Menu?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        const string sql = @"
            SELECT menu_id, code, name, active, created_at, updated_at
            FROM menu.menus
            WHERE code = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(code.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return MapMenu(reader);
    }

    public async Task UpdateAsync(Menu menu, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(menu);

        const string sql = @"
            UPDATE menu.menus
            SET name = $1, active = $2, updated_at = $3
            WHERE menu_id = $4;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(menu.Name);
        cmd.Parameters.AddWithValue(menu.IsActive);
        cmd.Parameters.AddWithValue(menu.UpdatedAt);
        cmd.Parameters.AddWithValue(menu.Id);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
            throw new MenuNotFoundException(menu.Id);
    }

    public async Task<IReadOnlyList<Menu>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var sql = @"
            SELECT menu_id, code, name, active, created_at, updated_at
            FROM menu.menus" +
            (activeOnly ? " WHERE active = true" : "") +
            " ORDER BY code ASC;";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<Menu>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapMenu(reader));
        }

        return list;
    }

    private static Menu MapMenu(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5));
}
