using ALKAROS.Tables.TableTransfer.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Tables.TableTransfer.Tests;

[Collection(nameof(TableTransferTestFixtureDefinition))]
public sealed class PostgresTableTransferTests : IClassFixture<TableTransferTestDatabase>, IAsyncLifetime
{
    private readonly TableTransferTestDatabase _db;
    private readonly PostgresTableTransferRepository _repository;
    private readonly TableTransferService _service;
    private Guid _userId;
    private Guid _zoneId;

    public PostgresTableTransferTests(TableTransferTestDatabase db)
    {
        _db = db;
        _repository = new PostgresTableTransferRepository(_db.DataSource);
        _service = new TableTransferService(_repository);
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
            cmd.Parameters.AddWithValue("display", "Test Waiter");
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
            cmd.Parameters.AddWithValue("name", "Main Dining");
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExecuteTransferHappyPathTransfersOrderAndBillPreservesIdentitiesAndUpdatesPointers()
    {
        var sourceTableId = await CreateTableAsync("T-101", "Occupied", 1);
        var targetTableId = await CreateTableAsync("T-102", "Available", 1);

        var orderId = await CreateOrderAsync(sourceTableId, "ORD-101", 150m);
        var billId = await CreateBillAsync(sourceTableId, orderId, "BIL-101", 150m, 0m, 0m, "Open");

        await SetTablePointersAsync(sourceTableId, orderId, billId);

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1,
            targetTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Guest requested outdoor table",
            TransferredBy: _userId);

        var result = await _service.TransferTableAsync(request);

        result.Should().NotBeNull();
        result.SourceTableId.Should().Be(sourceTableId);
        result.NewSourceRowVersion.Should().Be(2);
        result.TargetTableId.Should().Be(targetTableId);
        result.NewTargetRowVersion.Should().Be(2);
        result.TransferredOrderIds.Should().ContainSingle().Which.Should().Be(orderId);
        result.TransferredBillIds.Should().ContainSingle().Which.Should().Be(billId);

        // Verify Source Table state in DB
        var sourceTable = await GetTableAsync(sourceTableId);
        sourceTable.Status.Should().Be("Available");
        sourceTable.CurrentOrderId.Should().BeNull();
        sourceTable.CurrentBillId.Should().BeNull();
        sourceTable.RowVersion.Should().Be(2);

        // Verify Target Table state in DB
        var targetTable = await GetTableAsync(targetTableId);
        targetTable.Status.Should().Be("Occupied");
        targetTable.CurrentOrderId.Should().Be(orderId);
        targetTable.CurrentBillId.Should().Be(billId);
        targetTable.RowVersion.Should().Be(2);

        // Verify Order in DB: ID preserved, reparented to target table
        var order = await GetOrderAsync(orderId);
        order.TableId.Should().Be(targetTableId);
        order.RowVersion.Should().Be(2);

        // Verify Bill in DB: ID preserved, reparented to target table
        var bill = await GetBillAsync(billId);
        bill.TableId.Should().Be(targetTableId);
        bill.RowVersion.Should().Be(2);

        // Verify Transfer record
        var record = await _repository.GetByIdAsync(result.TransferId);
        record.Should().NotBeNull();
        record!.SourceTableId.Should().Be(sourceTableId);
        record.TargetTableId.Should().Be(targetTableId);
        record.OrderId.Should().Be(orderId);
        record.BillId.Should().Be(billId);
        record.Reason.Should().Be("Guest requested outdoor table");
        record.TransferredBy.Should().Be(_userId);

        var bySource = await _repository.GetBySourceTableAsync(sourceTableId);
        bySource.Should().Contain(r => r.Id == result.TransferId);

        var byTarget = await _repository.GetByTargetTableAsync(targetTableId);
        byTarget.Should().Contain(r => r.Id == result.TransferId);

        // Verify Audit Event
        var auditEvent = await GetLatestAuditEventAsync("Table", sourceTableId);
        auditEvent.Should().NotBeNull();
        auditEvent!.EventName.Should().Be("Table.Transferred");
        auditEvent.ActorId.Should().Be(_userId);
    }

    [Fact]
    public async Task ExecuteTransferPaymentDataOnBillThrowsPaymentPolicyRequiredExceptionAndRollsBack()
    {
        var sourceTableId = await CreateTableAsync("T-201", "Occupied", 1);
        var targetTableId = await CreateTableAsync("T-202", "Available", 1);

        var orderId = await CreateOrderAsync(sourceTableId, "ORD-201", 200m);
        var billId = await CreateBillAsync(sourceTableId, orderId, "BIL-201", 200m, allocatedAmount: 0m, paidAmount: 50m, status: "PartiallyPaid");
        await SetTablePointersAsync(sourceTableId, orderId, billId);

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1,
            targetTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Transfer attempt with payment",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(billId);

        // Verify rollback: source table still Occupied, target still Available, order/bill untouched
        var sourceTable = await GetTableAsync(sourceTableId);
        sourceTable.Status.Should().Be("Occupied");
        sourceTable.CurrentOrderId.Should().Be(orderId);
        sourceTable.CurrentBillId.Should().Be(billId);
        sourceTable.RowVersion.Should().Be(1);

        var targetTable = await GetTableAsync(targetTableId);
        targetTable.Status.Should().Be("Available");
        targetTable.CurrentOrderId.Should().BeNull();
        targetTable.RowVersion.Should().Be(1);

        var order = await GetOrderAsync(orderId);
        order.TableId.Should().Be(sourceTableId);

        var bill = await GetBillAsync(billId);
        bill.TableId.Should().Be(sourceTableId);
    }

    [Fact]
    public async Task ExecuteTransferAllocatedBillThrowsPaymentPolicyRequiredException()
    {
        var sourceTableId = await CreateTableAsync("T-301", "Occupied", 1);
        var targetTableId = await CreateTableAsync("T-302", "Available", 1);

        var orderId = await CreateOrderAsync(sourceTableId, "ORD-301", 100m);
        var billId = await CreateBillAsync(sourceTableId, orderId, "BIL-301", 100m, allocatedAmount: 50m, paidAmount: 0m, status: "Open");
        await SetTablePointersAsync(sourceTableId, orderId, billId);

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1,
            targetTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Transfer attempt with allocation",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(billId);
    }

    [Fact]
    public async Task ExecuteTransferBillWithBillAllocationsRowThrowsPaymentPolicyRequiredException()
    {
        var sourceTableId = await CreateTableAsync("T-401", "Occupied", 1);
        var targetTableId = await CreateTableAsync("T-402", "Available", 1);

        var orderId = await CreateOrderAsync(sourceTableId, "ORD-401", 100m);
        var billId = await CreateBillAsync(sourceTableId, orderId, "BIL-401", 100m, allocatedAmount: 0m, paidAmount: 0m, status: "Open");
        await SetTablePointersAsync(sourceTableId, orderId, billId);

        // Insert allocation row into billing.bill_allocations
        await InsertBillAllocationAsync(billId, 50m);

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1,
            targetTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Transfer attempt with split row",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(billId);
    }

    [Theory]
    [InlineData("Occupied")]
    [InlineData("Reserved")]
    [InlineData("Cleaning")]
    [InlineData("OutOfService")]
    public async Task ExecuteTransferTargetTableNotAvailableThrowsInvalidTargetTableStateException(string targetState)
    {
        var sourceTableId = await CreateTableAsync($"T-501-{targetState}", "Occupied", 1);
        var targetTableId = await CreateTableAsync($"T-502-{targetState}", targetState, 1);

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1,
            targetTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Move to busy target",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<InvalidTargetTableStateException>();
        ex.Which.TableId.Should().Be(targetTableId);
        ex.Which.ActualState.Should().Be(targetState);
    }

    [Theory]
    [InlineData("Available")]
    [InlineData("Reserved")]
    [InlineData("Cleaning")]
    [InlineData("OutOfService")]
    public async Task ExecuteTransferSourceTableNotOccupiedThrowsInvalidSourceTableStateException(string sourceState)
    {
        var sourceTableId = await CreateTableAsync($"T-601-{sourceState}", sourceState, 1);
        var targetTableId = await CreateTableAsync($"T-602-{sourceState}", "Available", 1);

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1,
            targetTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Move from non-occupied source",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<InvalidSourceTableStateException>();
        ex.Which.TableId.Should().Be(sourceTableId);
        ex.Which.ActualState.Should().Be(sourceState);
    }

    [Fact]
    public async Task ExecuteTransferStaleSourceRowVersionThrowsTableTransferConcurrencyException()
    {
        var sourceTableId = await CreateTableAsync("T-701", "Occupied", 2); // DB has version 2
        var targetTableId = await CreateTableAsync("T-702", "Available", 1);

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1, // Expecting 1 (stale)
            targetTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Stale source transfer",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<TableTransferConcurrencyException>();
        ex.Which.TableId.Should().Be(sourceTableId);
        ex.Which.ExpectedVersion.Should().Be(1);
        ex.Which.ActualVersion.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteTransferStaleTargetRowVersionThrowsTableTransferConcurrencyException()
    {
        var sourceTableId = await CreateTableAsync("T-801", "Occupied", 1);
        var targetTableId = await CreateTableAsync("T-802", "Available", 3); // DB has version 3

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1,
            targetTableId,
            ExpectedTargetRowVersion: 1, // Expecting 1 (stale)
            Reason: "Stale target transfer",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<TableTransferConcurrencyException>();
        ex.Which.TableId.Should().Be(targetTableId);
        ex.Which.ExpectedVersion.Should().Be(1);
        ex.Which.ActualVersion.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteTransferSameSourceAndTargetThrowsSameTableTransferException()
    {
        var sameTableId = Guid.NewGuid();

        var request = new TableTransferRequest(
            sameTableId,
            ExpectedSourceRowVersion: 1,
            sameTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Same table move",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        await act.Should().ThrowAsync<SameTableTransferException>();
    }

    [Fact]
    public async Task ExecuteTransferSourceTableNotFoundThrowsTableNotFoundException()
    {
        var nonExistentSourceId = Guid.NewGuid();
        var targetTableId = await CreateTableAsync("T-902", "Available", 1);

        var request = new TableTransferRequest(
            nonExistentSourceId,
            ExpectedSourceRowVersion: 1,
            targetTableId,
            ExpectedTargetRowVersion: 1,
            Reason: "Missing source",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<TableNotFoundException>();
        ex.Which.TableId.Should().Be(nonExistentSourceId);
    }

    [Fact]
    public async Task ExecuteTransferTargetTableNotFoundThrowsTableNotFoundException()
    {
        var sourceTableId = await CreateTableAsync("T-903", "Occupied", 1);
        var nonExistentTargetId = Guid.NewGuid();

        var request = new TableTransferRequest(
            sourceTableId,
            ExpectedSourceRowVersion: 1,
            nonExistentTargetId,
            ExpectedTargetRowVersion: 1,
            Reason: "Missing target",
            TransferredBy: _userId);

        var act = () => _service.TransferTableAsync(request);

        var ex = await act.Should().ThrowAsync<TableNotFoundException>();
        ex.Which.TableId.Should().Be(nonExistentTargetId);
    }

    // Helper methods for database seeding and asserting
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

    private async Task<Guid> CreateBillAsync(
        Guid tableId,
        Guid orderId,
        string billNumber,
        decimal payable,
        decimal allocatedAmount,
        decimal paidAmount,
        string status)
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
                0, 0, @payable, @allocated, @paid, 0, 'TRY', now(), now(), now(), 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("bill_number", billNumber);
        cmd.Parameters.AddWithValue("table_id", tableId);
        cmd.Parameters.AddWithValue("order_id", orderId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("payable", payable);
        cmd.Parameters.AddWithValue("allocated", allocatedAmount);
        cmd.Parameters.AddWithValue("paid", paidAmount);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task InsertBillAllocationAsync(Guid billId, decimal amount)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO billing.bill_allocations (
                bill_allocation_id, bill_id, owner_type, owner_reference,
                allocated_amount, tax_amount, created_at, row_version
            ) VALUES (
                @id, @bill_id, 'Person', 'Guest 1', @amount, 0, now(), 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("bill_id", billId);
        cmd.Parameters.AddWithValue("amount", amount);
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

    private async Task<(Guid? TableId, long RowVersion)> GetOrderAsync(Guid orderId)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = "SELECT table_id, row_version FROM orders.orders WHERE order_id = @id;";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", orderId);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (
            reader.IsDBNull(0) ? null : reader.GetGuid(0),
            reader.GetInt64(1));
    }

    private async Task<(Guid? TableId, long RowVersion)> GetBillAsync(Guid billId)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        const string sql = "SELECT table_id, row_version FROM billing.bills WHERE bill_id = @id;";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", billId);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (
            reader.IsDBNull(0) ? null : reader.GetGuid(0),
            reader.GetInt64(1));
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
/// Validates the up/down migration cycle of 023-table-transfers.
/// </summary>
public sealed class PostgresTableTransferMigrationTests : IClassFixture<TableTransferTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTableTransferMigrationTests(TableTransferTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "023-table-transfers.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "023-table-transfers.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify table is dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('table_mgmt.table_transfers')::text;", connection))
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
        await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('table_mgmt.table_transfers')::text;", connection))
        {
            var result = await checkCmd.ExecuteScalarAsync();
            result.Should().NotBeNull();
            result.Should().NotBe(DBNull.Value);
            result.Should().Be("table_mgmt.table_transfers");
        }
    }
}

[CollectionDefinition(nameof(TableTransferTestFixtureDefinition), DisableParallelization = true)]
public sealed class TableTransferTestFixtureDefinition : ICollectionFixture<TableTransferTestDatabase>
{
}

