using ALKAROS.Tables.CurrentPointers.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Tables.CurrentPointers.Tests;

[Collection(nameof(TablePointerTestFixtureDefinition))]
public sealed class PostgresTablePointerProjectorTests : IClassFixture<TablePointerTestDatabase>, IAsyncLifetime
{
    private readonly TablePointerTestDatabase _db;
    private readonly PostgresTablePointerProjector _projector;
    private Guid _userId;
    private Guid _zoneId;

    public PostgresTablePointerProjectorTests(TablePointerTestDatabase db)
    {
        _db = db;
        _projector = new PostgresTablePointerProjector(_db.DataSource);
    }

    public async Task InitializeAsync()
    {
        _userId = Guid.NewGuid();
        _zoneId = Guid.NewGuid();

        await using var connection = await _db.DataSource.OpenConnectionAsync();

        const string insertUserSql = """
            INSERT INTO identity.users (user_id, username, display_name, password_hash, active, created_at, updated_at)
            VALUES (@id, @username, @display, 'hash', true, now(), now())
            ON CONFLICT (user_id) DO NOTHING;
            """;
        await using (var cmd = new NpgsqlCommand(insertUserSql, connection))
        {
            cmd.Parameters.AddWithValue("id", _userId);
            cmd.Parameters.AddWithValue("username", $"user_{_userId:N}"[..20]);
            cmd.Parameters.AddWithValue("display", "Admin User");
            await cmd.ExecuteNonQueryAsync();
        }

        const string insertZoneSql = """
            INSERT INTO table_mgmt.zones (zone_id, code, name, sort_order, active)
            VALUES (@id, @code, @name, 1, true)
            ON CONFLICT (zone_id) DO NOTHING;
            """;
        await using (var cmd = new NpgsqlCommand(insertZoneSql, connection))
        {
            cmd.Parameters.AddWithValue("id", _zoneId);
            cmd.Parameters.AddWithValue("code", $"Z_{_zoneId:N}"[..10]);
            cmd.Parameters.AddWithValue("name", "Terrace");
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CleanTableWithMatchingPointersReportsNoDriftAndDoesNotModifyOnRebuild()
    {
        var tableId = await CreateTableAsync("P-101", "Occupied", 1);
        var orderId = await CreateOrderAsync(tableId, "ORD-P101", 100m, "Submitted");
        var billId = await CreateBillAsync(tableId, orderId, "BIL-P101", 100m, "Open");
        await SetTablePointersAsync(tableId, orderId, billId);

        var discrepancy = await _projector.DetectTableDriftAsync(tableId);

        discrepancy.Should().NotBeNull();
        discrepancy!.HasDrift.Should().BeFalse();
        discrepancy.DriftTypes.Should().Be(TablePointerDriftType.None);
        discrepancy.CurrentStatus.Should().Be("Occupied");
        discrepancy.ProjectedStatus.Should().Be("Occupied");

        var rebuildResult = await _projector.RebuildTablePointersAsync(tableId);

        rebuildResult.WasModified.Should().BeFalse();
        rebuildResult.PreviousRowVersion.Should().Be(1);
        rebuildResult.NewRowVersion.Should().Be(1);
        rebuildResult.CorrectedDrift.Should().Be(TablePointerDriftType.None);
    }

    [Fact]
    public async Task MissingOrderAndBillPointersDetectsDriftAndRebuildsAtomically()
    {
        var tableId = await CreateTableAsync("P-201", "Available", 1);
        var orderId = await CreateOrderAsync(tableId, "ORD-P201", 150m, "Submitted");
        var billId = await CreateBillAsync(tableId, orderId, "BIL-P201", 150m, "Open");
        // table pointers remain null and status is Available (drift!)

        var discrepancy = await _projector.DetectTableDriftAsync(tableId);

        discrepancy.Should().NotBeNull();
        discrepancy!.HasDrift.Should().BeTrue();
        discrepancy.DriftTypes.Should().HaveFlag(TablePointerDriftType.StatusMismatch);
        discrepancy.DriftTypes.Should().HaveFlag(TablePointerDriftType.MissingOrderPointer);
        discrepancy.DriftTypes.Should().HaveFlag(TablePointerDriftType.MissingBillPointer);
        discrepancy.ProjectedStatus.Should().Be("Occupied");
        discrepancy.AuthoritativeOrderId.Should().Be(orderId);
        discrepancy.AuthoritativeBillId.Should().Be(billId);

        var result = await _projector.RebuildTablePointersAsync(tableId);

        result.WasModified.Should().BeTrue();
        result.PreviousStatus.Should().Be("Available");
        result.NewStatus.Should().Be("Occupied");
        result.NewOrderId.Should().Be(orderId);
        result.NewBillId.Should().Be(billId);
        result.NewRowVersion.Should().Be(2);

        // Verify table in DB updated
        var (status, curOrderId, curBillId, rowVersion) = await GetTableAsync(tableId);
        status.Should().Be("Occupied");
        curOrderId.Should().Be(orderId);
        curBillId.Should().Be(billId);
        rowVersion.Should().Be(2);

        // Verify Audit Event
        var audit = await GetLatestAuditEventAsync("Table", tableId);
        audit.Should().NotBeNull();
        audit!.EventName.Should().Be("Table.PointersRebuilt");
    }

    [Fact]
    public async Task StaleAndGhostPointersOnCompletedOrderClearsPointersAndRestoresAvailable()
    {
        var tableId = await CreateTableAsync("P-301", "Occupied", 1);
        var oldOrderId = await CreateOrderAsync(tableId, "ORD-P301", 100m, "Completed");
        var oldBillId = await CreateBillAsync(tableId, oldOrderId, "BIL-P301", 100m, "Paid");
        await SetTablePointersAsync(tableId, oldOrderId, oldBillId);

        var discrepancy = await _projector.DetectTableDriftAsync(tableId);

        discrepancy.Should().NotBeNull();
        discrepancy!.HasDrift.Should().BeTrue();
        discrepancy.DriftTypes.Should().HaveFlag(TablePointerDriftType.StatusMismatch);
        discrepancy.DriftTypes.Should().HaveFlag(TablePointerDriftType.GhostOrderPointer);
        discrepancy.DriftTypes.Should().HaveFlag(TablePointerDriftType.GhostBillPointer);
        discrepancy.ProjectedStatus.Should().Be("Available");
        discrepancy.AuthoritativeOrderId.Should().BeNull();
        discrepancy.AuthoritativeBillId.Should().BeNull();

        var result = await _projector.RebuildTablePointersAsync(tableId);

        result.WasModified.Should().BeTrue();
        result.NewStatus.Should().Be("Available");
        result.NewOrderId.Should().BeNull();
        result.NewBillId.Should().BeNull();
        result.NewRowVersion.Should().Be(2);

        var (status, curOrderId, curBillId, _) = await GetTableAsync(tableId);
        status.Should().Be("Available");
        curOrderId.Should().BeNull();
        curBillId.Should().BeNull();
    }

    [Fact]
    public async Task MultiOpenOrdersDeterministicallySelectsMostRecentAsPrimaryPointer()
    {
        var tableId = await CreateTableAsync("P-401", "Available", 1);

        var order1Id = await CreateOrderAsync(tableId, "ORD-M1", 50m, "Submitted", DateTimeOffset.UtcNow.AddMinutes(-10));
        var order2Id = await CreateOrderAsync(tableId, "ORD-M2", 75m, "Submitted", DateTimeOffset.UtcNow.AddMinutes(-5));
        var order3Id = await CreateOrderAsync(tableId, "ORD-M3", 120m, "Submitted", DateTimeOffset.UtcNow);

        var discrepancy = await _projector.DetectTableDriftAsync(tableId);

        discrepancy.Should().NotBeNull();
        discrepancy!.AuthoritativeOrderId.Should().Be(order3Id); // Most recent order

        var result = await _projector.RebuildTablePointersAsync(tableId);

        result.NewOrderId.Should().Be(order3Id);
        result.NewStatus.Should().Be("Occupied");

        // Verify multiple rebuilds produce identical authoritative result
        var repeatRebuild = await _projector.RebuildTablePointersAsync(tableId);
        repeatRebuild.WasModified.Should().BeFalse();
        repeatRebuild.NewOrderId.Should().Be(order3Id);
    }

    [Fact]
    public async Task ActiveMergedParticipantProjectsOccupiedWithClearedPointers()
    {
        var primaryId = await CreateTableAsync("P-501", "Occupied", 1);
        var partId = await CreateTableAsync("P-502", "Occupied", 1);

        var oldOrderId = await CreateOrderAsync(partId, "ORD-P502", 50m, "Completed");
        await SetTablePointersAsync(partId, oldOrderId, null);

        // Insert active merge in table_mgmt.table_merges
        await InsertActiveMergeAsync(primaryId, partId);

        var discrepancy = await _projector.DetectTableDriftAsync(partId);

        discrepancy.Should().NotBeNull();
        discrepancy!.ProjectedStatus.Should().Be("Occupied");
        discrepancy.AuthoritativeOrderId.Should().BeNull();
        discrepancy.AuthoritativeBillId.Should().BeNull();
        discrepancy.DriftTypes.Should().HaveFlag(TablePointerDriftType.GhostOrderPointer);

        var result = await _projector.RebuildTablePointersAsync(partId);

        result.NewStatus.Should().Be("Occupied");
        result.NewOrderId.Should().BeNull();
        result.NewBillId.Should().BeNull();
    }

    [Fact]
    public async Task ActiveReservationProjectsReservedStatusAndReservationOrder()
    {
        var tableId = await CreateTableAsync("P-601", "Available", 1);
        var reservationOrderId = await CreateOrderAsync(tableId, "ORD-RES601", 0m, "PendingConfirmation");

        await InsertActiveReservationAsync(tableId, reservationOrderId);

        var discrepancy = await _projector.DetectTableDriftAsync(tableId);

        discrepancy.Should().NotBeNull();
        discrepancy!.ProjectedStatus.Should().Be("Reserved");
        discrepancy.AuthoritativeOrderId.Should().Be(reservationOrderId);

        var result = await _projector.RebuildTablePointersAsync(tableId);

        result.NewStatus.Should().Be("Reserved");
        result.NewOrderId.Should().Be(reservationOrderId);
    }

    [Fact]
    public async Task CleaningAndOutOfServiceStatesArePreservedWhenNoActiveOrders()
    {
        var cleaningTableId = await CreateTableAsync("P-701", "Cleaning", 1);
        var outOfServiceTableId = await CreateTableAsync("P-702", "OutOfService", 1);

        var cleaningDrift = await _projector.DetectTableDriftAsync(cleaningTableId);
        cleaningDrift!.HasDrift.Should().BeFalse();
        cleaningDrift.ProjectedStatus.Should().Be("Cleaning");

        var oosDrift = await _projector.DetectTableDriftAsync(outOfServiceTableId);
        oosDrift!.HasDrift.Should().BeFalse();
        oosDrift.ProjectedStatus.Should().Be("OutOfService");
    }

    [Fact]
    public async Task RebuildAllTablePointersScansAndRepairsAllDriftedTablesInSystem()
    {
        var table1 = await CreateTableAsync("P-801", "Available", 1);
        var table2 = await CreateTableAsync("P-802", "Available", 1);
        var order2 = await CreateOrderAsync(table2, "ORD-P802", 100m, "Submitted");

        var summary = await _projector.RebuildAllTablePointersAsync();

        summary.TotalScannedTables.Should().BeGreaterThanOrEqualTo(2);
        summary.RebuiltTablesCount.Should().BeGreaterThanOrEqualTo(1);

        // After rebuild all, no drift should remain
        var remainingDrifts = await _projector.DetectAllDriftAsync();
        remainingDrifts.Should().BeEmpty();
    }

    // Helper methods for DB seeding
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

    private async Task SetTablePointersAsync(Guid tableId, Guid? orderId, Guid? billId)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            UPDATE table_mgmt.tables
            SET current_order_id = @order_id, current_bill_id = @bill_id
            WHERE table_id = @table_id;
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("table_id", tableId);
        cmd.Parameters.AddWithValue("order_id", (object?)orderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bill_id", (object?)billId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<Guid> CreateOrderAsync(Guid tableId, string orderNumber, decimal total, string status, DateTimeOffset? createdAt = null)
    {
        var id = Guid.NewGuid();
        var now = createdAt ?? DateTimeOffset.UtcNow;
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO orders.orders (
                order_id, source, table_id, status, confirmation_status, order_number,
                subtotal, discount_total, tax_total, total, created_at, updated_at, row_version
            ) VALUES (
                @id, 'Cashier', @table_id, @status, 'Accepted', @order_number,
                @total, 0, 0, @total, @created_at, @created_at, 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("table_id", tableId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("order_number", orderNumber);
        cmd.Parameters.AddWithValue("total", total);
        cmd.Parameters.AddWithValue("created_at", now);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<Guid> CreateBillAsync(Guid tableId, Guid orderId, string billNumber, decimal payable, string status)
    {
        var id = Guid.NewGuid();
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO billing.bills (
                bill_id, bill_number, table_id, order_id, status, subtotal,
                discount_total, tax_total, payable_amount, allocated_amount, paid_amount,
                change_amount, currency_code, opened_at, created_at, updated_at, row_version
            ) VALUES (
                @id, @bill_number, @table_id, @order_id, @status, @payable,
                0, 0, @payable, 0, 0, 0, 'TRY', now(), now(), now(), 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("bill_number", billNumber);
        cmd.Parameters.AddWithValue("table_id", tableId);
        cmd.Parameters.AddWithValue("order_id", orderId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("payable", payable);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task InsertActiveMergeAsync(Guid primaryTableId, Guid mergedTableId)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO table_mgmt.table_merges (
                table_merge_id, merge_group_id, primary_table_id, merged_table_id,
                status, reason, merged_by, merged_at, row_version
            ) VALUES (
                @id, @group_id, @primary_id, @merged_id,
                'Active', 'Merge test', @user_id, now(), 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("group_id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("primary_id", primaryTableId);
        cmd.Parameters.AddWithValue("merged_id", mergedTableId);
        cmd.Parameters.AddWithValue("user_id", _userId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertActiveReservationAsync(Guid tableId, Guid? orderId)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO table_mgmt.table_reservations (
                table_reservation_id, table_id, order_id, actor_id, actor_type,
                status, reason, party_size, reserved_at, row_version
            ) VALUES (
                @id, @table_id, @order_id, @actor_id, 'User',
                'Active', 'Reservation test', 2, now(), 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("table_id", tableId);
        cmd.Parameters.AddWithValue("order_id", (object?)orderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("actor_id", _userId);
        await cmd.ExecuteNonQueryAsync();
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

[CollectionDefinition(nameof(TablePointerTestFixtureDefinition), DisableParallelization = true)]
public sealed class TablePointerTestFixtureDefinition : ICollectionFixture<TablePointerTestDatabase>
{
}
