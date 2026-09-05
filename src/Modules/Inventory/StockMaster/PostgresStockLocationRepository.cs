using System.Globalization;
using Npgsql;

namespace ALKAROS.Inventory.StockMaster;

public sealed class PostgresStockLocationRepository : IStockLocationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresStockLocationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AddAsync(StockLocation location, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        const string sql = @"
            INSERT INTO inventory.stock_locations (id, code, name, location_type, is_active, created_at, row_version)
            VALUES ($1, $2, $3, $4, $5, $6, $7);";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(location.Id);
        cmd.Parameters.AddWithValue(location.Code);
        cmd.Parameters.AddWithValue(location.Name);
        cmd.Parameters.AddWithValue(location.LocationType.ToString());
        cmd.Parameters.AddWithValue(location.IsActive);
        cmd.Parameters.AddWithValue(location.CreatedAt);
        cmd.Parameters.AddWithValue(location.RowVersion);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<StockLocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, code, name, location_type, is_active, created_at, row_version
            FROM inventory.stock_locations
            WHERE id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<StockLocation?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, code, name, location_type, is_active, created_at, row_version
            FROM inventory.stock_locations
            WHERE code = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(code.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<StockLocation>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var sql = @"
            SELECT id, code, name, location_type, is_active, created_at, row_version
            FROM inventory.stock_locations";

        if (activeOnly)
            sql += " WHERE is_active = true";

        sql += " ORDER BY code ASC;";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<StockLocation>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }
        return list;
    }

    public async Task UpdateAsync(StockLocation location, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        const string sql = @"
            UPDATE inventory.stock_locations
            SET name = $2,
                location_type = $3,
                is_active = $4,
                row_version = row_version + 1
            WHERE id = $1 AND row_version = $5
            RETURNING row_version;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(location.Id);
        cmd.Parameters.AddWithValue(location.Name);
        cmd.Parameters.AddWithValue(location.LocationType.ToString());
        cmd.Parameters.AddWithValue(location.IsActive);
        cmd.Parameters.AddWithValue(location.RowVersion);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
        {
            throw new StockMasterConcurrencyException(
                $"Optimistic concurrency violation on StockLocation {location.Id}.");
        }

        location.RowVersion = Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM inventory.stock_locations WHERE id = $1;";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static StockLocation MapRow(NpgsqlDataReader reader)
    {
        return new StockLocation(
            id: reader.GetGuid(0),
            code: reader.GetString(1),
            name: reader.GetString(2),
            locationType: Enum.Parse<StockLocationType>(reader.GetString(3), ignoreCase: true),
            isActive: reader.GetBoolean(4),
            createdAt: reader.GetFieldValue<DateTimeOffset>(5),
            rowVersion: reader.GetInt32(6));
    }
}
