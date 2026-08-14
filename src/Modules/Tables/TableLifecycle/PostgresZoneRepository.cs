using Npgsql;

namespace ALKAROS.Tables.TableLifecycle;

public sealed class PostgresZoneRepository : IZoneRepository
{
    private const string Table = "table_mgmt.zones";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresZoneRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Zone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT zone_id, code, name, sort_order, active
            FROM {Table}
            WHERE zone_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadZone(reader);
    }

    public async Task<Zone?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT zone_id, code, name, sort_order, active
            FROM {Table}
            WHERE code = @code;
            """);
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadZone(reader);
    }

    public async Task<IReadOnlyList<Zone>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Zone>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT zone_id, code, name, sort_order, active
            FROM {Table}
            ORDER BY sort_order, code;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadZone(reader));

        return result;
    }

    public async Task AddAsync(Zone zone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zone);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (zone_id, code, name, sort_order, active)
            VALUES (@zone_id, @code, @name, @sort_order, @active);
            """);
        command.Parameters.AddWithValue("zone_id", zone.Id);
        command.Parameters.AddWithValue("code", zone.Code);
        command.Parameters.AddWithValue("name", zone.Name);
        command.Parameters.AddWithValue("sort_order", zone.SortOrder);
        command.Parameters.AddWithValue("active", zone.Active);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Zone zone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zone);

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET code = @code,
                name = @name,
                sort_order = @sort_order,
                active = @active
            WHERE zone_id = @zone_id;
            """);
        command.Parameters.AddWithValue("zone_id", zone.Id);
        command.Parameters.AddWithValue("code", zone.Code);
        command.Parameters.AddWithValue("name", zone.Name);
        command.Parameters.AddWithValue("sort_order", zone.SortOrder);
        command.Parameters.AddWithValue("active", zone.Active);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException($"Zone {zone.Id} not found.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {Table} WHERE zone_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Zone ReadZone(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetBoolean(4));
}