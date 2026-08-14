using Npgsql;

namespace ALKAROS.Tables.TableLifecycle;

public sealed class PostgresTableRepository : ITableRepository
{
    private const string Table = "table_mgmt.tables";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresTableRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Table?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty.", nameof(id));

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT table_id, table_number, zone_id, capacity, active, current_status,
                   current_order_id, current_bill_id, row_version
            FROM {Table}
            WHERE table_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadTable(reader);
    }

    public async Task<IReadOnlyList<Table>> GetByZoneAsync(Guid zoneId, CancellationToken cancellationToken = default)
    {
        if (zoneId == Guid.Empty)
            throw new ArgumentException("Zone id cannot be empty.", nameof(zoneId));

        var result = new List<Table>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT table_id, table_number, zone_id, capacity, active, current_status,
                   current_order_id, current_bill_id, row_version
            FROM {Table}
            WHERE zone_id = @zone_id
            ORDER BY table_number;
            """);
        command.Parameters.AddWithValue("zone_id", zoneId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadTable(reader));

        return result;
    }

    public async Task<IReadOnlyList<Table>> GetUnzonedAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Table>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT table_id, table_number, zone_id, capacity, active, current_status,
                   current_order_id, current_bill_id, row_version
            FROM {Table}
            WHERE zone_id IS NULL
            ORDER BY table_number;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadTable(reader));

        return result;
    }

    public async Task AddAsync(Table table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table} (table_id, table_number, zone_id, capacity, active, current_status,
                                 current_order_id, current_bill_id, row_version)
            VALUES (@table_id, @table_number, @zone_id, @capacity, @active, @current_status,
                    @current_order_id, @current_bill_id, @row_version);
            """);
        command.Parameters.AddWithValue("table_id", table.Id);
        command.Parameters.AddWithValue("table_number", table.TableNumber);
        command.Parameters.AddWithValue("zone_id", table.ZoneId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("capacity", table.Capacity);
        command.Parameters.AddWithValue("active", table.Active);
        command.Parameters.AddWithValue("current_status", table.State.ToString());
        command.Parameters.AddWithValue("current_order_id", table.CurrentOrderId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("current_bill_id", table.CurrentBillId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("row_version", table.RowVersion);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> UpdateStatusAsync(
        Guid id,
        TableState target,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET current_status = @target,
                row_version = row_version + 1
            WHERE table_id = @id AND row_version = @expected_row_version
            RETURNING row_version;
            """);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("target", target.ToString());
        command.Parameters.AddWithValue("expected_row_version", expectedRowVersion);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new InvalidOperationException(
                $"Table {id} not found or concurrent modification (expected row version {expectedRowVersion}).");

        return (long)result;
    }

    private static Table ReadTable(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.GetInt32(3),
            reader.GetBoolean(4),
            Enum.Parse<TableState>(reader.GetString(5)),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.GetInt64(8));
}