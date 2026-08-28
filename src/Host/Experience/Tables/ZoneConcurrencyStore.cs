using ALKAROS.Tables.TableLifecycle;
using Npgsql;

namespace ALKAROS.Host.Experience.Tables;

public sealed class ZoneConcurrencyStore
{
    private readonly NpgsqlDataSource _dataSource;

    public ZoneConcurrencyStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<IReadOnlyList<ZoneDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<ZoneDto>();
        await using var command = _dataSource.CreateCommand(
            """
            SELECT zone_id, code, name, sort_order, active, xmin::text::bigint
            FROM table_mgmt.zones
            ORDER BY sort_order, code;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            zones.Add(Read(reader));
        return zones;
    }

    public async Task<ZoneDto?> GetAsync(Guid zoneId, CancellationToken cancellationToken = default)
    {
        EnsureId(zoneId);
        await using var command = _dataSource.CreateCommand(
            """
            SELECT zone_id, code, name, sort_order, active, xmin::text::bigint
            FROM table_mgmt.zones
            WHERE zone_id = @zone_id;
            """);
        command.Parameters.AddWithValue("zone_id", zoneId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<ZoneDto> CreateAsync(
        CreateZoneRequest request,
        IZoneRepository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(repository);
        TableContractMapper.RequireCreateVersion(request.ExpectedRowVersion);
        var zone = new Zone(Guid.NewGuid(), request.Code, request.Name, request.SortOrder, request.Active);
        await repository.AddAsync(zone, cancellationToken);
        return await GetAsync(zone.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created zone could not be read back.");
    }

    public async Task<ZoneDto> UpdateAsync(
        Guid zoneId,
        UpdateZoneRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureId(zoneId);
        ArgumentNullException.ThrowIfNull(request);
        var expected = TableContractMapper.RequiredVersion(request.ExpectedRowVersion, nameof(request.ExpectedRowVersion));
        _ = new Zone(zoneId, request.Code, request.Name, request.SortOrder, request.Active);

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE table_mgmt.zones
            SET code = @code, name = @name, sort_order = @sort_order, active = @active
            WHERE zone_id = @zone_id AND xmin::text::bigint = @expected_row_version
            RETURNING zone_id, code, name, sort_order, active, xmin::text::bigint;
            """);
        command.Parameters.AddWithValue("zone_id", zoneId);
        command.Parameters.AddWithValue("code", request.Code);
        command.Parameters.AddWithValue("name", request.Name);
        command.Parameters.AddWithValue("sort_order", request.SortOrder);
        command.Parameters.AddWithValue("active", request.Active);
        command.Parameters.AddWithValue("expected_row_version", expected);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            return Read(reader);

        var actual = await GetAsync(zoneId, cancellationToken);
        if (actual is null)
            throw new TableManagementNotFoundException($"Zone {zoneId} was not found.");
        throw new TableManagementConcurrencyException("zone", zoneId, expected, actual.RowVersion);
    }

    public async Task DeleteAsync(
        Guid zoneId,
        long? expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureId(zoneId);
        var expected = TableContractMapper.RequiredVersion(expectedRowVersion, nameof(expectedRowVersion));
        await using var command = _dataSource.CreateCommand(
            """
            DELETE FROM table_mgmt.zones
            WHERE zone_id = @zone_id AND xmin::text::bigint = @expected_row_version;
            """);
        command.Parameters.AddWithValue("zone_id", zoneId);
        command.Parameters.AddWithValue("expected_row_version", expected);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 1)
            return;

        var actual = await GetAsync(zoneId, cancellationToken);
        if (actual is null)
            throw new TableManagementNotFoundException($"Zone {zoneId} was not found.");
        throw new TableManagementConcurrencyException("zone", zoneId, expected, actual.RowVersion);
    }

    private static ZoneDto Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetInt32(3),
        reader.GetBoolean(4),
        reader.GetInt64(5));

    private static void EnsureId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Zone ID cannot be empty.", nameof(id));
    }
}
