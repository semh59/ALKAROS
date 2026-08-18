using ALKAROS.Tables.Reservations.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Tables.Reservations.Tests;

[Collection(nameof(TableReservationTestFixtureDefinition))]
public sealed class PostgresTableReservationTests : IClassFixture<TableReservationTestDatabase>, IAsyncLifetime
{
    private readonly TableReservationTestDatabase _db;
    private readonly PostgresTableReservationRepository _repository;
    private readonly TableReservationService _service;
    private Guid _userId;
    private Guid _zoneId;

    public PostgresTableReservationTests(TableReservationTestDatabase db)
    {
        _db = db;
        _repository = new PostgresTableReservationRepository(_db.DataSource);
        _service = new TableReservationService(_repository);
    }

    public async Task InitializeAsync()
    {
        _userId = Guid.NewGuid();
        _zoneId = Guid.NewGuid();

        await using var connection = await _db.DataSource.OpenConnectionAsync();

        // 1. Seed user
        const string insertUserSql = """
            INSERT INTO identity.users (user_id, username, display_name, password_hash, active, created_at, updated_at)
            VALUES (@id, @username, @display, 'hash', true, now(), now())
            ON CONFLICT (user_id) DO NOTHING;
            """;
        await using (var cmd = new NpgsqlCommand(insertUserSql, connection))
        {
            cmd.Parameters.AddWithValue("id", _userId);
            cmd.Parameters.AddWithValue("username", $"user_{_userId:N}"[..20]);
            cmd.Parameters.AddWithValue("display", "Hostess User");
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Seed zone
        const string insertZoneSql = """
            INSERT INTO table_mgmt.zones (zone_id, code, name, sort_order, active)
            VALUES (@id, @code, @name, 1, true)
            ON CONFLICT (zone_id) DO NOTHING;
            """;
        await using (var cmd = new NpgsqlCommand(insertZoneSql, connection))
        {
            cmd.Parameters.AddWithValue("id", _zoneId);
            cmd.Parameters.AddWithValue("code", $"Z_{_zoneId:N}"[..10]);
            cmd.Parameters.AddWithValue("name", "Garden");
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateReservationAndClaimExecutesCleanlyAndProjectsStatus()
    {
        // 1. Create table
        var tableId = await CreateTableAsync("R-101", "Available", 1);
        var orderId = await CreateOrderAsync(tableId, "ORD-R101", 0m);

        // 2. Reserve table
        var reserveRequest = new CreateReservationRequest(
            tableId,
            ExpectedTableRowVersion: 1,
            OrderId: orderId,
            ActorId: _userId,
            ActorType: TableReservationActorType.User,
            Reason: "Dinner reservation for 4",
            PartySize: 4,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(2));

        var reserveResult = await _service.CreateReservationAsync(reserveRequest);

        reserveResult.Should().NotBeNull();
        reserveResult.TableId.Should().Be(tableId);
        reserveResult.NewTableRowVersion.Should().Be(2);
        reserveResult.Status.Should().Be(TableReservationStatus.Active);

        // Verify table in DB is Reserved
        var (status, curOrderId, curBillId, rowVersion) = await GetTableAsync(tableId);
        status.Should().Be("Reserved");
        curOrderId.Should().Be(orderId);
        curBillId.Should().BeNull();
        rowVersion.Should().Be(2);

        // Verify reservation record in DB
        var reservation = await _repository.GetByIdAsync(reserveResult.ReservationId);
        reservation.Should().NotBeNull();
        reservation!.TableId.Should().Be(tableId);
        reservation.OrderId.Should().Be(orderId);
        reservation.ActorId.Should().Be(_userId);
        reservation.ActorType.Should().Be(TableReservationActorType.User);
        reservation.Status.Should().Be(TableReservationStatus.Active);
        reservation.Reason.Should().Be("Dinner reservation for 4");
        reservation.PartySize.Should().Be(4);
        reservation.RowVersion.Should().Be(1);

        // Verify Audit Event for Reserved
        var auditReserved = await GetLatestAuditEventAsync("Table", tableId);
        auditReserved.Should().NotBeNull();
        auditReserved!.EventName.Should().Be("Table.Reserved");
        auditReserved.ActorId.Should().Be(_userId);

        // 3. Claim Reservation (Customer seated -> Occupied)
        var claimRequest = new ClaimReservationRequest(
            reserveResult.ReservationId,
            ExpectedReservationRowVersion: 1,
            ExpectedTableRowVersion: 2,
            OrderId: orderId,
            ClaimedBy: _userId);

        var claimResult = await _service.ClaimReservationAsync(claimRequest);

        claimResult.Should().NotBeNull();
        claimResult.ReservationId.Should().Be(reserveResult.ReservationId);
        claimResult.TableId.Should().Be(tableId);
        claimResult.NewReservationRowVersion.Should().Be(2);
        claimResult.NewTableRowVersion.Should().Be(3);
        claimResult.PreviousStatus.Should().Be(TableReservationStatus.Active);
        claimResult.NewStatus.Should().Be(TableReservationStatus.Claimed);
        claimResult.FinalTableStatus.Should().Be("Occupied");

        // Verify table in DB is Occupied
        var (cStatus, cOrderId, _, cRowVersion) = await GetTableAsync(tableId);
        cStatus.Should().Be("Occupied");
        cOrderId.Should().Be(orderId);
        cRowVersion.Should().Be(3);

        // Verify reservation in DB is Claimed
        var updatedRes = await _repository.GetByIdAsync(reserveResult.ReservationId);
        updatedRes!.Status.Should().Be(TableReservationStatus.Claimed);
        updatedRes.ReleasedAt.Should().NotBeNull();
        updatedRes.ReleasedBy.Should().Be(_userId);
        updatedRes.RowVersion.Should().Be(2);

        // Verify Audit Event for Claimed
        var auditClaimed = await GetLatestAuditEventAsync("Table", tableId);
        auditClaimed.Should().NotBeNull();
        auditClaimed!.EventName.Should().Be("Table.ReservationClaimed");
    }

    [Fact]
    public async Task CreateReservationAndCancelRestoresTableToAvailable()
    {
        var tableId = await CreateTableAsync("R-201", "Available", 1);

        var reserveRequest = new CreateReservationRequest(
            tableId,
            ExpectedTableRowVersion: 1,
            OrderId: null,
            ActorId: _userId,
            ActorType: TableReservationActorType.User,
            Reason: "Phone reservation",
            PartySize: 2);

        var reserveResult = await _service.CreateReservationAsync(reserveRequest);

        var cancelRequest = new CancelReservationRequest(
            reserveResult.ReservationId,
            ExpectedReservationRowVersion: 1,
            ExpectedTableRowVersion: 2,
            CancelledBy: _userId,
            Reason: "Customer called to cancel");

        var cancelResult = await _service.CancelReservationAsync(cancelRequest);

        cancelResult.Should().NotBeNull();
        cancelResult.NewStatus.Should().Be(TableReservationStatus.Cancelled);
        cancelResult.FinalTableStatus.Should().Be("Available");
        cancelResult.NewTableRowVersion.Should().Be(3);

        // Verify table in DB is Available
        var (status, curOrderId, _, rowVersion) = await GetTableAsync(tableId);
        status.Should().Be("Available");
        curOrderId.Should().BeNull();
        rowVersion.Should().Be(3);

        // Verify reservation in DB is Cancelled
        var updatedRes = await _repository.GetByIdAsync(reserveResult.ReservationId);
        updatedRes!.Status.Should().Be(TableReservationStatus.Cancelled);
        updatedRes.ReleaseReason.Should().Be("Customer called to cancel");
    }

    [Fact]
    public async Task CreateReservationAndExpireRestoresTableToAvailable()
    {
        var tableId = await CreateTableAsync("R-301", "Available", 1);

        var reserveRequest = new CreateReservationRequest(
            tableId,
            ExpectedTableRowVersion: 1,
            OrderId: null,
            ActorId: _userId,
            ActorType: TableReservationActorType.System,
            Reason: "QR pending confirmation hold",
            PartySize: 2);

        var reserveResult = await _service.CreateReservationAsync(reserveRequest);

        var expireRequest = new ExpireReservationRequest(
            reserveResult.ReservationId,
            ExpectedReservationRowVersion: 1,
            ExpectedTableRowVersion: 2,
            ExpiredBy: null,
            Reason: "QR order timeout expired");

        var expireResult = await _service.ExpireReservationAsync(expireRequest);

        expireResult.Should().NotBeNull();
        expireResult.NewStatus.Should().Be(TableReservationStatus.Expired);
        expireResult.FinalTableStatus.Should().Be("Available");
        expireResult.NewTableRowVersion.Should().Be(3);

        // Verify table in DB is Available
        var (status, _, _, rowVersion) = await GetTableAsync(tableId);
        status.Should().Be("Available");
        rowVersion.Should().Be(3);

        // Verify reservation in DB is Expired
        var updatedRes = await _repository.GetByIdAsync(reserveResult.ReservationId);
        updatedRes!.Status.Should().Be(TableReservationStatus.Expired);
        updatedRes.ReleaseReason.Should().Be("QR order timeout expired");
    }

    [Theory]
    [InlineData("Occupied")]
    [InlineData("Reserved")]
    [InlineData("Cleaning")]
    [InlineData("OutOfService")]
    public async Task ReserveUnavailableTableThrowsTableNotAvailableForReservationException(string invalidState)
    {
        var tableId = await CreateTableAsync($"R-401-{invalidState}", invalidState, 1);

        var request = new CreateReservationRequest(
            tableId,
            ExpectedTableRowVersion: 1,
            OrderId: null,
            ActorId: _userId,
            ActorType: TableReservationActorType.User,
            Reason: "Attempt reserve invalid state",
            PartySize: 2);

        var act = () => _service.CreateReservationAsync(request);

        var ex = await act.Should().ThrowAsync<TableNotAvailableForReservationException>();
        ex.Which.TableId.Should().Be(tableId);
        ex.Which.ActualState.Should().Be(invalidState);
    }

    [Fact]
    public async Task ReserveWithStaleTableRowVersionThrowsTableReservationConcurrencyException()
    {
        var tableId = await CreateTableAsync("R-501", "Available", 2); // DB is version 2

        var request = new CreateReservationRequest(
            tableId,
            ExpectedTableRowVersion: 1, // Stale version 1
            OrderId: null,
            ActorId: _userId,
            ActorType: TableReservationActorType.User,
            Reason: "Stale table version",
            PartySize: 2);

        var act = () => _service.CreateReservationAsync(request);

        var ex = await act.Should().ThrowAsync<TableReservationConcurrencyException>();
        ex.Which.EntityId.Should().Be(tableId);
        ex.Which.ExpectedVersion.Should().Be(1);
        ex.Which.ActualVersion.Should().Be(2);
    }

    [Fact]
    public async Task CancelWithStaleReservationRowVersionThrowsTableReservationConcurrencyException()
    {
        var tableId = await CreateTableAsync("R-601", "Available", 1);

        var reserveRequest = new CreateReservationRequest(
            tableId,
            ExpectedTableRowVersion: 1,
            OrderId: null,
            ActorId: _userId,
            ActorType: TableReservationActorType.User,
            Reason: "Reservation to cancel",
            PartySize: 2);

        var reserveResult = await _service.CreateReservationAsync(reserveRequest);

        var cancelRequest = new CancelReservationRequest(
            reserveResult.ReservationId,
            ExpectedReservationRowVersion: 99, // Wrong version
            ExpectedTableRowVersion: 2,
            CancelledBy: _userId,
            Reason: "Wrong version cancel");

        var act = () => _service.CancelReservationAsync(cancelRequest);

        var ex = await act.Should().ThrowAsync<TableReservationConcurrencyException>();
        ex.Which.EntityId.Should().Be(reserveResult.ReservationId);
        ex.Which.ExpectedVersion.Should().Be(99);
    }

    [Fact]
    public async Task ClaimAlreadyCancelledReservationThrowsInvalidReservationStateException()
    {
        var tableId = await CreateTableAsync("R-701", "Available", 1);

        var reserveRequest = new CreateReservationRequest(
            tableId,
            ExpectedTableRowVersion: 1,
            OrderId: null,
            ActorId: _userId,
            ActorType: TableReservationActorType.User,
            Reason: "Phone reservation",
            PartySize: 2);

        var reserveResult = await _service.CreateReservationAsync(reserveRequest);

        // Cancel it
        await _service.CancelReservationAsync(new CancelReservationRequest(
            reserveResult.ReservationId,
            ExpectedReservationRowVersion: 1,
            ExpectedTableRowVersion: 2,
            CancelledBy: _userId,
            Reason: "Cancelled"));

        // Attempt claim
        var claimRequest = new ClaimReservationRequest(
            reserveResult.ReservationId,
            ExpectedReservationRowVersion: 2,
            ExpectedTableRowVersion: 3,
            OrderId: null,
            ClaimedBy: _userId);

        var act = () => _service.ClaimReservationAsync(claimRequest);

        var ex = await act.Should().ThrowAsync<InvalidReservationStateException>();
        ex.Which.ReservationId.Should().Be(reserveResult.ReservationId);
        ex.Which.ActualStatus.Should().Be(TableReservationStatus.Cancelled);
    }

    // Helper methods for database assertions
    private async Task<Guid> CreateTableAsync(string tableNumber, string status, long rowVersion, bool active = true)
    {
        var id = Guid.NewGuid();
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, current_order_id, current_bill_id, row_version)
            VALUES (@id, @zone_id, @number, 4, @active, @status, null, null, @version);
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("zone_id", _zoneId);
        cmd.Parameters.AddWithValue("number", tableNumber);
        cmd.Parameters.AddWithValue("active", active);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("version", rowVersion);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<Guid> CreateOrderAsync(Guid tableId, string orderNumber, decimal total)
    {
        var id = Guid.NewGuid();
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO orders.orders (
                order_id, source, table_id, status, confirmation_status, order_number,
                subtotal, discount_total, tax_total, total, created_at, updated_at, row_version
            ) VALUES (
                @id, 'Cashier', @table_id, 'Submitted', 'Accepted', @order_number,
                @total, 0, 0, @total, now(), now(), 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("table_id", tableId);
        cmd.Parameters.AddWithValue("order_number", orderNumber);
        cmd.Parameters.AddWithValue("total", total);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<(string Status, Guid? CurrentOrderId, Guid? CurrentBillId, long RowVersion)> GetTableAsync(Guid tableId)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = "SELECT current_status, current_order_id, current_bill_id, row_version FROM table_mgmt.tables WHERE table_id = @id;";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", tableId);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.GetInt64(3));
    }

    private async Task<DbAuditEvent?> GetLatestAuditEventAsync(string aggregateType, Guid aggregateId)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            SELECT id, event_name, aggregate_type, aggregate_id, actor_id, reason
            FROM audit.audit_events
            WHERE aggregate_type = @type AND aggregate_id = @id
            ORDER BY occurred_at DESC
            LIMIT 1;
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("type", aggregateType);
        cmd.Parameters.AddWithValue("id", aggregateId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new DbAuditEvent(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private sealed record DbAuditEvent(Guid Id, string EventName, string AggregateType, Guid AggregateId, Guid? ActorId, string? Reason);
}

/// <summary>
/// Validates the up/down migration cycle of 025-table-reservations.
/// </summary>
public sealed class PostgresTableReservationMigrationTests : IClassFixture<TableReservationTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTableReservationMigrationTests(TableReservationTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "025-table-reservations.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "025-table-reservations.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify table is dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('table_mgmt.table_reservations')::text;", connection))
        {
            var result = await checkCmd.ExecuteScalarAsync();
            result.Should().Be(DBNull.Value);
        }

        // 2. Run up.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(upSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify table exists again
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('table_mgmt.table_reservations')::text;", connection))
        {
            var result = await checkCmd.ExecuteScalarAsync();
            result.Should().NotBeNull();
            result.Should().NotBe(DBNull.Value);
            result.Should().Be("table_mgmt.table_reservations");
        }
    }
}

[CollectionDefinition(nameof(TableReservationTestFixtureDefinition), DisableParallelization = true)]
public sealed class TableReservationTestFixtureDefinition : ICollectionFixture<TableReservationTestDatabase>
{
}
