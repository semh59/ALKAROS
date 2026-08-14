namespace ALKAROS.Tables.TableLifecycle.Tests;

using ALKAROS.Tables.TableLifecycle;
using ALKAROS.Tables.TableLifecycle.Tests.Fixtures;
using Npgsql;
using Xunit;

/// <summary>
/// Integration tests that exercise the table/zone repositories and the
/// table_mgmt migration constraints (zone FK, per-zone table number
/// uniqueness with NULLS NOT DISTINCT, canonical status check) against a
/// real database created from 010-tables.up.sql.
/// </summary>
public sealed class PostgresTableTests : IClassFixture<TablesTestDatabase>
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresTableRepository _tables;
    private readonly PostgresZoneRepository _zones;

    public PostgresTableTests(TablesTestDatabase database)
    {
        _dataSource = database.DataSource;
        _tables = new PostgresTableRepository(database.DataSource);
        _zones = new PostgresZoneRepository(database.DataSource);
    }

    [Fact]
    public async Task TableRoundTripPersistsAllPdfFields()
    {
        var zone = new Zone(Guid.NewGuid(), "TERRACE", "Terrace");
        await _zones.AddAsync(zone);
        var table = new Table(
            Guid.NewGuid(),
            "5",
            zone.Id,
            capacity: 4,
            active: false,
            state: TableState.Cleaning);

        await _tables.AddAsync(table);

        var loaded = await _tables.GetByIdAsync(table.Id);
        Assert.NotNull(loaded);
        Assert.Equal(table.Id, loaded.Id);
        Assert.Equal("5", loaded.TableNumber);
        Assert.Equal(zone.Id, loaded.ZoneId);
        Assert.Equal(4, loaded.Capacity);
        Assert.False(loaded.Active);
        Assert.Equal(TableState.Cleaning, loaded.State);
        Assert.Null(loaded.CurrentOrderId);
        Assert.Null(loaded.CurrentBillId);
        Assert.Equal(1, loaded.RowVersion);
    }

    [Fact]
    public async Task ZoneRoundTripPersistsAllFields()
    {
        var zone = new Zone(Guid.NewGuid(), "GARDEN", "Garden", 2, active: false);
        await _zones.AddAsync(zone);

        var byId = await _zones.GetByIdAsync(zone.Id);
        Assert.NotNull(byId);
        Assert.Equal("GARDEN", byId.Code);
        Assert.Equal("Garden", byId.Name);
        Assert.Equal(2, byId.SortOrder);
        Assert.False(byId.Active);

        var byCode = await _zones.GetByCodeAsync("GARDEN");
        Assert.NotNull(byCode);
        Assert.Equal(zone.Id, byCode.Id);
    }

    [Fact]
    public async Task ZoneUpdateAndDeleteArePersisted()
    {
        var zone = new Zone(Guid.NewGuid(), "OLD", "Old Name");
        await _zones.AddAsync(zone);

        await _zones.UpdateAsync(new Zone(zone.Id, "NEW", "New Name", 1, active: false));
        var updated = await _zones.GetByIdAsync(zone.Id);
        Assert.NotNull(updated);
        Assert.Equal("NEW", updated.Code);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal(1, updated.SortOrder);
        Assert.False(updated.Active);

        await _zones.DeleteAsync(zone.Id);
        Assert.Null(await _zones.GetByIdAsync(zone.Id));
    }

    [Fact]
    public async Task DuplicateZoneCodeRejected()
    {
        await _zones.AddAsync(new Zone(Guid.NewGuid(), "MAIN", "Main"));

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => _zones.AddAsync(new Zone(Guid.NewGuid(), "MAIN", "Another")));
        Assert.Equal(UniqueViolation, exception.SqlState);
    }

    [Fact]
    public async Task UnzonedDuplicateTableNumberRejected()
    {
        await _tables.AddAsync(new Table(Guid.NewGuid(), "7"));

        var act = () => _tables.AddAsync(new Table(Guid.NewGuid(), "7"));

        var exception = await Assert.ThrowsAsync<PostgresException>(act);
        Assert.Equal(UniqueViolation, exception.SqlState);
    }

    [Fact]
    public async Task SameTableNumberInDifferentZonesAllowed()
    {
        var firstZone = new Zone(Guid.NewGuid(), "A", "Zone A");
        var secondZone = new Zone(Guid.NewGuid(), "B", "Zone B");
        await _zones.AddAsync(firstZone);
        await _zones.AddAsync(secondZone);

        await _tables.AddAsync(new Table(Guid.NewGuid(), "3", firstZone.Id));
        await _tables.AddAsync(new Table(Guid.NewGuid(), "3", secondZone.Id));
    }

    [Fact]
    public async Task UnknownZoneForeignKeyRejected()
    {
        var table = new Table(Guid.NewGuid(), "4", zoneId: Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<PostgresException>(() => _tables.AddAsync(table));
        Assert.Equal(ForeignKeyViolation, exception.SqlState);
    }

    [Fact]
    public async Task InvalidStatusCheckRejected()
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, current_status)
            VALUES (@table_id, @table_number, @current_status);
            """);
        command.Parameters.AddWithValue("table_id", Guid.NewGuid());
        command.Parameters.AddWithValue("table_number", "9");
        command.Parameters.AddWithValue("current_status", "Bogus");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task StatusUpdatePersistsAndBumpsRowVersion()
    {
        var table = new Table(Guid.NewGuid(), "6");
        await _tables.AddAsync(table);

        var newVersion = await _tables.UpdateStatusAsync(table.Id, TableState.Occupied, table.RowVersion);

        Assert.Equal(table.RowVersion + 1, newVersion);
        var loaded = await _tables.GetByIdAsync(table.Id);
        Assert.NotNull(loaded);
        Assert.Equal(TableState.Occupied, loaded.State);
        Assert.Equal(newVersion, loaded.RowVersion);
    }

    [Fact]
    public async Task StaleRowVersionUpdateRejected()
    {
        var table = new Table(Guid.NewGuid(), "8");
        await _tables.AddAsync(table);

        var act = () => _tables.UpdateStatusAsync(table.Id, TableState.Occupied, expectedRowVersion: 5);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("concurrent modification", exception.Message);
    }

    [Fact]
    public async Task SecondUpdateWithSameRowVersionRejected()
    {
        var table = new Table(Guid.NewGuid(), "10");
        await _tables.AddAsync(table);

        await _tables.UpdateStatusAsync(table.Id, TableState.Occupied, expectedRowVersion: 1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tables.UpdateStatusAsync(table.Id, TableState.Available, expectedRowVersion: 1));
    }

    [Fact]
    public async Task GetByZoneAndGetUnzonedSeparateTables()
    {
        var zone = new Zone(Guid.NewGuid(), "Z", "Zone");
        await _zones.AddAsync(zone);
        var zoned = new Table(Guid.NewGuid(), "2", zone.Id);
        var unzoned = new Table(Guid.NewGuid(), "1");
        await _tables.AddAsync(zoned);
        await _tables.AddAsync(unzoned);

        var byZone = await _tables.GetByZoneAsync(zone.Id);
        Assert.Equal([zoned.Id], byZone.Select(t => t.Id));

        var unzonedTables = await _tables.GetUnzonedAsync();
        Assert.Contains(unzonedTables, t => t.Id == unzoned.Id);
        Assert.DoesNotContain(unzonedTables, t => t.Id == zoned.Id);
    }
}

/// <summary>
/// Validates the rollback direction of the 010 migration against its own
/// freshly created database; dropping the schema must not affect any other
/// test class fixture.
/// </summary>
public sealed class PostgresDownSqlTests : IClassFixture<TablesTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDownSqlTests(TablesTestDatabase database)
    {
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task DownSqlDropsTablesAndSchema()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "010-tables.down.sql"));

        await using var command = _dataSource.CreateCommand(downSql);
        await command.ExecuteNonQueryAsync();

        await using var after = _dataSource.CreateCommand(
            "SELECT to_regclass('table_mgmt.zones'), to_regclass('table_mgmt.tables');");
        await using var reader = await after.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.IsDBNull(0));
        Assert.True(reader.IsDBNull(1));
    }
}