using ALKAROS.Tables.TableLifecycle;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Tables;

public sealed class TableManagementStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ITableRepository _repository;

    public TableManagementStore(NpgsqlDataSource dataSource, ITableRepository repository)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Table?> GetAsync(Guid tableId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(tableId, cancellationToken);

    public async Task<IReadOnlyList<Table>> GetAllAsync(
        Guid? zoneId,
        CancellationToken cancellationToken = default)
    {
        var tables = new List<Table>();
        await using var command = _dataSource.CreateCommand(
            """
            SELECT table_id, table_number, zone_id, capacity, active, current_status,
                   current_order_id, current_bill_id, row_version
            FROM table_mgmt.tables
            WHERE @zone_id IS NULL OR zone_id = @zone_id
            ORDER BY table_number, table_id;
            """);
        command.Parameters.Add("zone_id", NpgsqlDbType.Uuid).Value = zoneId ?? (object)DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tables.Add(Read(reader));
        return tables;
    }

    public async Task<Table> CreateAsync(
        CreateTableRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        TableContractMapper.RequireCreateVersion(request.ExpectedRowVersion);
        var table = new Table(
            Guid.NewGuid(),
            request.TableNumber,
            request.ZoneId,
            request.Capacity,
            request.Active);
        await _repository.AddAsync(table, cancellationToken);
        return await _repository.GetByIdAsync(table.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created table could not be read back.");
    }

    public async Task<Table> UpdateAsync(
        Guid tableId,
        UpdateTableRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureId(tableId);
        ArgumentNullException.ThrowIfNull(request);
        var expected = TableContractMapper.RequiredVersion(request.ExpectedRowVersion, nameof(request.ExpectedRowVersion));
        _ = new Table(tableId, request.TableNumber, request.ZoneId, request.Capacity, request.Active);

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE table_mgmt.tables
            SET table_number = @table_number,
                zone_id = @zone_id,
                capacity = @capacity,
                active = @active,
                row_version = row_version + 1
            WHERE table_id = @table_id AND row_version = @expected_row_version
            RETURNING table_id, table_number, zone_id, capacity, active, current_status,
                      current_order_id, current_bill_id, row_version;
            """);
        command.Parameters.AddWithValue("table_id", tableId);
        command.Parameters.AddWithValue("table_number", request.TableNumber);
        command.Parameters.AddWithValue("zone_id", request.ZoneId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("capacity", request.Capacity);
        command.Parameters.AddWithValue("active", request.Active);
        command.Parameters.AddWithValue("expected_row_version", expected);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            return Read(reader);

        return await MissingOrConflictAsync(tableId, expected, cancellationToken);
    }

    public async Task<Table> ChangeStatusAsync(
        Guid tableId,
        ChangeTableStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureId(tableId);
        ArgumentNullException.ThrowIfNull(request);
        var expected = TableContractMapper.RequiredVersion(request.ExpectedRowVersion, nameof(request.ExpectedRowVersion));
        if (!Enum.TryParse<TableState>(request.Status, ignoreCase: true, out var target)
            || !Enum.IsDefined(target))
        {
            throw new ArgumentException("Status is not a supported table state.", nameof(request));
        }

        var current = await _repository.GetByIdAsync(tableId, cancellationToken)
            ?? throw new TableManagementNotFoundException($"Table {tableId} was not found.");
        if (current.RowVersion != expected)
            throw new TableManagementConcurrencyException("table", tableId, expected, current.RowVersion);

        try
        {
            _ = current.TransitionTo(target);
        }
        catch (InvalidOperationException exception)
        {
            throw new TableManagementConflictException(exception.Message, exception);
        }
        try
        {
            await _repository.UpdateStatusAsync(tableId, target, expected, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return await MissingOrConflictAsync(tableId, expected, cancellationToken);
        }

        return await _repository.GetByIdAsync(tableId, cancellationToken)
            ?? throw new TableManagementNotFoundException($"Table {tableId} was not found after status update.");
    }

    private async Task<Table> MissingOrConflictAsync(
        Guid tableId,
        long expected,
        CancellationToken cancellationToken)
    {
        var actual = await _repository.GetByIdAsync(tableId, cancellationToken);
        if (actual is null)
            throw new TableManagementNotFoundException($"Table {tableId} was not found.");
        throw new TableManagementConcurrencyException("table", tableId, expected, actual.RowVersion);
    }

    private static Table Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetGuid(2),
        reader.GetInt32(3),
        reader.GetBoolean(4),
        Enum.Parse<TableState>(reader.GetString(5)),
        reader.IsDBNull(6) ? null : reader.GetGuid(6),
        reader.IsDBNull(7) ? null : reader.GetGuid(7),
        reader.GetInt64(8));

    private static void EnsureId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Table ID cannot be empty.", nameof(id));
    }
}
