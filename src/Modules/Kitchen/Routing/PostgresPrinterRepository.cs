namespace ALKAROS.Kitchen.Routing;

using Npgsql;

public sealed class PostgresPrinterRepository : IPrinterRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPrinterRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Printer?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, name, station_id, ip_address, port, is_active, created_at, updated_at
            FROM kitchen.printers
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapPrinter(reader);
    }

    public async Task<IReadOnlyList<Printer>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, name, station_id, ip_address, port, is_active, created_at, updated_at
            FROM kitchen.printers
            ORDER BY name;
            """;

        var list = new List<Printer>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapPrinter(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<Printer>> GetActiveAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, name, station_id, ip_address, port, is_active, created_at, updated_at
            FROM kitchen.printers
            WHERE is_active = TRUE
            ORDER BY name;
            """;

        var list = new List<Printer>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapPrinter(reader));
        }

        return list;
    }

    public async Task SaveAsync(Printer printer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(printer);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO kitchen.printers (
                id, name, station_id, ip_address, port, is_active, created_at, updated_at
            ) VALUES (
                @id, @name, @station_id, @ip_address, @port, @is_active, @created_at, @updated_at
            )
            ON CONFLICT (id) DO UPDATE SET
                name = EXCLUDED.name,
                station_id = EXCLUDED.station_id,
                ip_address = EXCLUDED.ip_address,
                port = EXCLUDED.port,
                is_active = EXCLUDED.is_active,
                updated_at = EXCLUDED.updated_at;
            """;

        cmd.Parameters.AddWithValue("id", printer.Id);
        cmd.Parameters.AddWithValue("name", printer.Name);
        cmd.Parameters.AddWithValue("station_id", printer.StationId);
        cmd.Parameters.AddWithValue("ip_address", (object?)printer.IpAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("port", (object?)printer.Port ?? DBNull.Value);
        cmd.Parameters.AddWithValue("is_active", printer.IsActive);
        cmd.Parameters.AddWithValue("created_at", printer.CreatedAt);
        cmd.Parameters.AddWithValue("updated_at", (object?)printer.UpdatedAt ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM kitchen.printers WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static Printer MapPrinter(NpgsqlDataReader reader)
    {
        return new Printer(
            id: reader.GetGuid(0),
            name: reader.GetString(1),
            stationId: reader.GetString(2),
            ipAddress: reader.IsDBNull(3) ? null : reader.GetString(3),
            port: reader.IsDBNull(4) ? null : reader.GetInt32(4),
            isActive: reader.GetBoolean(5),
            createdAt: reader.GetFieldValue<DateTimeOffset>(6),
            updatedAt: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
    }
}
