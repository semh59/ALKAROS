using ALKAROS.Tables.TableMerge.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Tables.TableMerge.Tests;

[Collection(nameof(TableMergeTestFixtureDefinition))]
public sealed class PostgresTableMergeTests : IClassFixture<TableMergeTestDatabase>, IAsyncLifetime
{
    private readonly TableMergeTestDatabase _db;
    private readonly PostgresTableMergeRepository _repository;
    private readonly TableMergeService _service;
    private Guid _userId;
    private Guid _zoneId;

    public PostgresTableMergeTests(TableMergeTestDatabase db)
    {
        _db = db;
        _repository = new PostgresTableMergeRepository(_db.DataSource);
        _service = new TableMergeService(_repository);
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
            cmd.Parameters.AddWithValue("name", "Main Hall");
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ThreeTableMergeAndUnmergeExecutesCleanlyPreservesHistoryAndRestoresState()
    {
        // 1. Create 3 tables: T-1 (Primary), T-2 (Participant 1), T-3 (Participant 2)
        var primaryId = await CreateTableAsync("M-101", "Occupied", 1);
        var part1Id = await CreateTableAsync("M-102", "Occupied", 1);
        var part2Id = await CreateTableAsync("M-103", "Occupied", 1);

        var order1Id = await CreateOrderAsync(primaryId, "ORD-M1", 100m);
        var bill1Id = await CreateBillAsync(primaryId, order1Id, "BIL-M1", 100m, 0m, 0m, "Open");
        await SetTablePointersAsync(primaryId, order1Id, bill1Id);

        var order2Id = await CreateOrderAsync(part1Id, "ORD-M2", 200m);
        var bill2Id = await CreateBillAsync(part1Id, order2Id, "BIL-M2", 200m, 0m, 0m, "Open");
        await SetTablePointersAsync(part1Id, order2Id, bill2Id);

        var order3Id = await CreateOrderAsync(part2Id, "ORD-M3", 300m);
        var bill3Id = await CreateBillAsync(part2Id, order3Id, "BIL-M3", 300m, 0m, 0m, "Open");
        await SetTablePointersAsync(part2Id, order3Id, bill3Id);

        // 2. Execute 3-Table Merge
        var mergeRequest = new TableMergeRequest(
            primaryId,
            ExpectedPrimaryRowVersion: 1,
            new[]
            {
                new TableMergeParticipant(part1Id, ExpectedRowVersion: 1),
                new TableMergeParticipant(part2Id, ExpectedRowVersion: 1)
            },
            Reason: "Large party of 12",
            MergedBy: _userId);

        var mergeResult = await _service.MergeTablesAsync(mergeRequest);

        mergeResult.Should().NotBeNull();
        mergeResult.PrimaryTableId.Should().Be(primaryId);
        mergeResult.NewPrimaryRowVersion.Should().Be(2);
        mergeResult.MergedTableIds.Should().HaveCount(2).And.Contain(new[] { part1Id, part2Id });
        mergeResult.NewParticipantRowVersions[part1Id].Should().Be(2);
        mergeResult.NewParticipantRowVersions[part2Id].Should().Be(2);
        mergeResult.ConsolidatedOrderIds.Should().Contain(new[] { order1Id, order2Id, order3Id });
        mergeResult.ConsolidatedBillIds.Should().Contain(new[] { bill1Id, bill2Id, bill3Id });

        // Verify Primary Table in DB: Occupied, has pointers
        var primaryTableAfterMerge = await GetTableAsync(primaryId);
        primaryTableAfterMerge.Status.Should().Be("Occupied");
        primaryTableAfterMerge.CurrentOrderId.Should().Be(order1Id);
        primaryTableAfterMerge.CurrentBillId.Should().Be(bill1Id);
        primaryTableAfterMerge.RowVersion.Should().Be(2);

        // Verify Participant Tables in DB: Occupied, pointers cleared
        var part1TableAfterMerge = await GetTableAsync(part1Id);
        part1TableAfterMerge.Status.Should().Be("Occupied");
        part1TableAfterMerge.CurrentOrderId.Should().BeNull();
        part1TableAfterMerge.CurrentBillId.Should().BeNull();
        part1TableAfterMerge.RowVersion.Should().Be(2);

        var part2TableAfterMerge = await GetTableAsync(part2Id);
        part2TableAfterMerge.Status.Should().Be("Occupied");
        part2TableAfterMerge.CurrentOrderId.Should().BeNull();
        part2TableAfterMerge.CurrentBillId.Should().BeNull();
        part2TableAfterMerge.RowVersion.Should().Be(2);

        // Verify Orders and Bills reparented to Primary Table
        (await GetOrderAsync(order2Id)).TableId.Should().Be(primaryId);
        (await GetOrderAsync(order3Id)).TableId.Should().Be(primaryId);
        (await GetBillAsync(bill2Id)).TableId.Should().Be(primaryId);
        (await GetBillAsync(bill3Id)).TableId.Should().Be(primaryId);

        // Verify Merge Records in DB
        var groupRecords = await _repository.GetByGroupIdAsync(mergeResult.MergeGroupId);
        groupRecords.Should().HaveCount(2);
        groupRecords.Should().OnlyContain(r => r.Status == TableMergeStatus.Active && r.IsActive);
        groupRecords.Should().Contain(r => r.MergedTableId == part1Id && r.OriginalOrderId == order2Id && r.OriginalBillId == bill2Id);
        groupRecords.Should().Contain(r => r.MergedTableId == part2Id && r.OriginalOrderId == order3Id && r.OriginalBillId == bill3Id);

        // Verify Audit Event
        var mergeAudit = await GetLatestAuditEventAsync("Table", primaryId);
        mergeAudit.Should().NotBeNull();
        mergeAudit!.EventName.Should().Be("Table.Merged");
        mergeAudit.ActorId.Should().Be(_userId);

        // 3. Execute Unmerge (Undo)
        var unmergeRequest = new TableUnmergeRequest(
            mergeResult.MergeGroupId,
            ExpectedPrimaryRowVersion: 2,
            new[]
            {
                new TableMergeParticipant(part1Id, ExpectedRowVersion: 2),
                new TableMergeParticipant(part2Id, ExpectedRowVersion: 2)
            },
            Reason: "Party split into separate tables",
            UnmergedBy: _userId);

        var unmergeResult = await _service.UnmergeTablesAsync(unmergeRequest);

        unmergeResult.Should().NotBeNull();
        unmergeResult.PrimaryTableId.Should().Be(primaryId);
        unmergeResult.NewPrimaryRowVersion.Should().Be(3);
        unmergeResult.NewParticipantRowVersions[part1Id].Should().Be(3);
        unmergeResult.NewParticipantRowVersions[part2Id].Should().Be(3);
        unmergeResult.RestoredOrderIds.Should().Contain(new[] { order2Id, order3Id });
        unmergeResult.RestoredBillIds.Should().Contain(new[] { bill2Id, bill3Id });

        // Verify Primary Table restored
        var primaryTableAfterUnmerge = await GetTableAsync(primaryId);
        primaryTableAfterUnmerge.Status.Should().Be("Occupied");
        primaryTableAfterUnmerge.CurrentOrderId.Should().Be(order1Id);
        primaryTableAfterUnmerge.CurrentBillId.Should().Be(bill1Id);
        primaryTableAfterUnmerge.RowVersion.Should().Be(3);

        // Verify Participant Tables restored
        var part1TableAfterUnmerge = await GetTableAsync(part1Id);
        part1TableAfterUnmerge.Status.Should().Be("Occupied");
        part1TableAfterUnmerge.CurrentOrderId.Should().Be(order2Id);
        part1TableAfterUnmerge.CurrentBillId.Should().Be(bill2Id);
        part1TableAfterUnmerge.RowVersion.Should().Be(3);

        var part2TableAfterUnmerge = await GetTableAsync(part2Id);
        part2TableAfterUnmerge.Status.Should().Be("Occupied");
        part2TableAfterUnmerge.CurrentOrderId.Should().Be(order3Id);
        part2TableAfterUnmerge.CurrentBillId.Should().Be(bill3Id);
        part2TableAfterUnmerge.RowVersion.Should().Be(3);

        // Verify Orders and Bills restored back to original tables
        (await GetOrderAsync(order2Id)).TableId.Should().Be(part1Id);
        (await GetOrderAsync(order3Id)).TableId.Should().Be(part2Id);
        (await GetBillAsync(bill2Id)).TableId.Should().Be(part1Id);
        (await GetBillAsync(bill3Id)).TableId.Should().Be(part2Id);

        // Verify Merge Records status is now Unmerged and history is preserved
        var groupRecordsAfterUnmerge = await _repository.GetByGroupIdAsync(mergeResult.MergeGroupId);
        groupRecordsAfterUnmerge.Should().HaveCount(2);
        groupRecordsAfterUnmerge.Should().OnlyContain(r => r.Status == TableMergeStatus.Unmerged && !r.IsActive);
        groupRecordsAfterUnmerge.Should().OnlyContain(r => r.UnmergedAt.HasValue && r.UnmergedBy == _userId);

        // Verify Audit Event for Unmerge
        var unmergeAudit = await GetLatestAuditEventAsync("Table", primaryId);
        unmergeAudit.Should().NotBeNull();
        unmergeAudit!.EventName.Should().Be("Table.Unmerged");
        unmergeAudit.ActorId.Should().Be(_userId);
    }

    [Fact]
    public async Task MergeWithPaidBillOnParticipantThrowsPaymentPolicyRequiredExceptionAndRollsBack()
    {
        var primaryId = await CreateTableAsync("M-201", "Occupied", 1);
        var partId = await CreateTableAsync("M-202", "Occupied", 1);

        var order1Id = await CreateOrderAsync(primaryId, "ORD-P1", 100m);
        var bill1Id = await CreateBillAsync(primaryId, order1Id, "BIL-P1", 100m, 0m, 0m, "Open");
        await SetTablePointersAsync(primaryId, order1Id, bill1Id);

        var order2Id = await CreateOrderAsync(partId, "ORD-P2", 150m);
        var bill2Id = await CreateBillAsync(partId, order2Id, "BIL-P2", 150m, allocatedAmount: 0m, paidAmount: 50m, status: "PartiallyPaid");
        await SetTablePointersAsync(partId, order2Id, bill2Id);

        var request = new TableMergeRequest(
            primaryId,
            ExpectedPrimaryRowVersion: 1,
            new[] { new TableMergeParticipant(partId, 1) },
            Reason: "Merge with partial payment",
            MergedBy: _userId);

        var act = () => _service.MergeTablesAsync(request);

        var ex = await act.Should().ThrowAsync<PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(bill2Id);

        // Verify rollback: tables, orders and bills unchanged
        (await GetTableAsync(primaryId)).RowVersion.Should().Be(1);
        (await GetTableAsync(partId)).RowVersion.Should().Be(1);
        (await GetOrderAsync(order2Id)).TableId.Should().Be(partId);
        (await GetBillAsync(bill2Id)).TableId.Should().Be(partId);
    }

    [Fact]
    public async Task MergeWithAllocatedBillOnParticipantThrowsPaymentPolicyRequiredException()
    {
        var primaryId = await CreateTableAsync("M-301", "Occupied", 1);
        var partId = await CreateTableAsync("M-302", "Occupied", 1);

        var orderId = await CreateOrderAsync(partId, "ORD-A1", 120m);
        var billId = await CreateBillAsync(partId, orderId, "BIL-A1", 120m, allocatedAmount: 40m, paidAmount: 0m, status: "Open");
        await SetTablePointersAsync(partId, orderId, billId);

        var request = new TableMergeRequest(
            primaryId,
            ExpectedPrimaryRowVersion: 1,
            new[] { new TableMergeParticipant(partId, 1) },
            Reason: "Merge with allocated bill",
            MergedBy: _userId);

        var act = () => _service.MergeTablesAsync(request);

        var ex = await act.Should().ThrowAsync<PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(billId);
    }

    [Fact]
    public async Task MergeWithBillAllocationRowThrowsPaymentPolicyRequiredException()
    {
        var primaryId = await CreateTableAsync("M-401", "Occupied", 1);
        var partId = await CreateTableAsync("M-402", "Occupied", 1);

        var orderId = await CreateOrderAsync(partId, "ORD-BA1", 120m);
        var billId = await CreateBillAsync(partId, orderId, "BIL-BA1", 120m, 0m, 0m, "Open");
        await SetTablePointersAsync(partId, orderId, billId);

        await InsertBillAllocationAsync(billId, 60m);

        var request = new TableMergeRequest(
            primaryId,
            ExpectedPrimaryRowVersion: 1,
            new[] { new TableMergeParticipant(partId, 1) },
            Reason: "Merge with allocation rows",
            MergedBy: _userId);

        var act = () => _service.MergeTablesAsync(request);

        var ex = await act.Should().ThrowAsync<PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(billId);
    }

    [Theory]
    [InlineData("Reserved")]
    [InlineData("Cleaning")]
    [InlineData("OutOfService")]
    public async Task MergeWithInvalidParticipantStateThrowsInvalidTableMergeStateException(string invalidState)
    {
        var primaryId = await CreateTableAsync($"M-501-{invalidState}", "Occupied", 1);
        var partId = await CreateTableAsync($"M-502-{invalidState}", invalidState, 1);

        var request = new TableMergeRequest(
            primaryId,
            ExpectedPrimaryRowVersion: 1,
            new[] { new TableMergeParticipant(partId, 1) },
            Reason: "Invalid state merge",
            MergedBy: _userId);

        var act = () => _service.MergeTablesAsync(request);

        var ex = await act.Should().ThrowAsync<InvalidTableMergeStateException>();
        ex.Which.TableId.Should().Be(partId);
        ex.Which.ActualState.Should().Be(invalidState);
    }

    [Fact]
    public async Task MergeWithStalePrimaryRowVersionThrowsTableMergeConcurrencyException()
    {
        var primaryId = await CreateTableAsync("M-601", "Occupied", 2); // DB is version 2
        var partId = await CreateTableAsync("M-602", "Occupied", 1);

        var request = new TableMergeRequest(
            primaryId,
            ExpectedPrimaryRowVersion: 1, // Stale version 1
            new[] { new TableMergeParticipant(partId, 1) },
            Reason: "Stale version test",
            MergedBy: _userId);

        var act = () => _service.MergeTablesAsync(request);

        var ex = await act.Should().ThrowAsync<TableMergeConcurrencyException>();
        ex.Which.TableId.Should().Be(primaryId);
        ex.Which.ExpectedVersion.Should().Be(1);
        ex.Which.ActualVersion.Should().Be(2);
    }

    [Fact]
    public async Task MergeWithStaleParticipantRowVersionThrowsTableMergeConcurrencyException()
    {
        var primaryId = await CreateTableAsync("M-701", "Occupied", 1);
        var partId = await CreateTableAsync("M-702", "Occupied", 3); // DB is version 3

        var request = new TableMergeRequest(
            primaryId,
            ExpectedPrimaryRowVersion: 1,
            new[] { new TableMergeParticipant(partId, 1) }, // Stale version 1
            Reason: "Stale part version test",
            MergedBy: _userId);

        var act = () => _service.MergeTablesAsync(request);

        var ex = await act.Should().ThrowAsync<TableMergeConcurrencyException>();
        ex.Which.TableId.Should().Be(partId);
        ex.Which.ExpectedVersion.Should().Be(1);
        ex.Which.ActualVersion.Should().Be(3);
    }

    [Fact]
    public async Task MergeWithNonExistentPrimaryTableThrowsTableNotFoundException()
    {
        var nonExistentId = Guid.NewGuid();
        var partId = await CreateTableAsync("M-802", "Occupied", 1);

        var request = new TableMergeRequest(
            nonExistentId,
            ExpectedPrimaryRowVersion: 1,
            new[] { new TableMergeParticipant(partId, 1) },
            Reason: "Missing primary",
            MergedBy: _userId);

        var act = () => _service.MergeTablesAsync(request);

        var ex = await act.Should().ThrowAsync<TableNotFoundException>();
        ex.Which.TableId.Should().Be(nonExistentId);
    }

    [Fact]
    public async Task UnmergeNonExistentGroupThrowsMergeRecordNotFoundException()
    {
        var nonExistentGroupId = Guid.NewGuid();
        var request = new TableUnmergeRequest(
            nonExistentGroupId,
            ExpectedPrimaryRowVersion: 1,
            new[] { new TableMergeParticipant(Guid.NewGuid(), 1) },
            Reason: "Missing group",
            UnmergedBy: _userId);

        var act = () => _service.UnmergeTablesAsync(request);

        var ex = await act.Should().ThrowAsync<MergeRecordNotFoundException>();
        ex.Which.MergeGroupId.Should().Be(nonExistentGroupId);
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
/// Validates the up/down migration cycle of 024-table-merges.
/// </summary>
public sealed class PostgresTableMergeMigrationTests : IClassFixture<TableMergeTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTableMergeMigrationTests(TableMergeTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "024-table-merges.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "024-table-merges.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify table is dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('table_mgmt.table_merges')::text;", connection))
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
        await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('table_mgmt.table_merges')::text;", connection))
        {
            var result = await checkCmd.ExecuteScalarAsync();
            result.Should().NotBeNull();
            result.Should().NotBe(DBNull.Value);
            result.Should().Be("table_mgmt.table_merges");
        }
    }
}

[CollectionDefinition(nameof(TableMergeTestFixtureDefinition), DisableParallelization = true)]
public sealed class TableMergeTestFixtureDefinition : ICollectionFixture<TableMergeTestDatabase>
{
}
