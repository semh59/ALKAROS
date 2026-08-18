using ALKAROS.Observability.AlertFoundation.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Observability.AlertFoundation.Tests;

[Collection(nameof(AlertTestFixtureDefinition))]
public sealed class PostgresAlertRepositoryTests : IClassFixture<AlertTestDatabase>, IAsyncLifetime
{
    private readonly AlertTestDatabase _db;
    private readonly PostgresAlertRepository _repository;
    private readonly AlertService _service;
    private Guid _userId;

    public PostgresAlertRepositoryTests(AlertTestDatabase db)
    {
        _db = db;
        _repository = new PostgresAlertRepository(_db.DataSource);
        _service = new AlertService(_repository);
    }

    public async Task InitializeAsync()
    {
        _userId = Guid.NewGuid();
        await using var connection = await _db.DataSource.OpenConnectionAsync();

        const string insertUserSql = """
            INSERT INTO identity.users (user_id, username, display_name, password_hash, active, created_at, updated_at)
            VALUES (@id, @username, @display, 'hash', true, now(), now())
            ON CONFLICT (user_id) DO NOTHING;
            """;
        await using var cmd = new NpgsqlCommand(insertUserSql, connection);
        cmd.Parameters.AddWithValue("id", _userId);
        cmd.Parameters.AddWithValue("username", $"user_{_userId:N}"[..20]);
        cmd.Parameters.AddWithValue("display", "Ops Supervisor");
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RaiseNewAlertCreatesOpenAlertAndCreatedEvent()
    {
        var printerId = Guid.NewGuid();
        var request = new RaiseAlertRequest(
            AlertType: "PrinterOffline",
            Severity: AlertSeverity.Critical,
            Title: "Kitchen Bar Printer Offline",
            Message: "Cannot establish TCP connection to printer at 192.168.1.50:9100",
            DeduplicationKey: $"PRN-OFFLINE-{printerId}",
            SourceReferenceType: "Printer",
            SourceReferenceId: printerId,
            ActorId: _userId,
            PayloadJson: "{\"ip\":\"192.168.1.50\",\"port\":9100}");

        var result = await _service.RaiseAlertAsync(request);

        result.Should().NotBeNull();
        result.IsNewAlert.Should().BeTrue();
        result.WasDeduplicated.Should().BeFalse();
        result.Alert.Status.Should().Be(AlertStatus.Open);
        result.Alert.Severity.Should().Be(AlertSeverity.Critical);
        result.Alert.RowVersion.Should().Be(1);

        // Verify direct read
        var direct = await _service.GetByIdAsync(result.Alert.AlertId);
        direct.Should().NotBeNull();
        direct!.Title.Should().Be("Kitchen Bar Printer Offline");

        // Verify events
        var events = await _service.GetEventsAsync(result.Alert.AlertId);
        events.Should().HaveCount(1);
        events[0].EventType.Should().Be(AlertEventType.Created);
        events[0].ActorId.Should().Be(_userId);
    }

    [Fact]
    public async Task RepeatedIdenticalAlertDeduplicatesIntoSingleActiveAlert()
    {
        var terminalId = Guid.NewGuid();
        var dedupKey = $"POS-SYNC-TIMEOUT-{terminalId}";

        var request1 = new RaiseAlertRequest(
            AlertType: "PosSyncTimeout",
            Severity: AlertSeverity.Warning,
            Title: "POS Sync Lagging",
            Message: "Local sync queue exceeded 50 items",
            DeduplicationKey: dedupKey,
            SourceReferenceType: "Device",
            SourceReferenceId: terminalId,
            ActorId: _userId);

        // 1. First raise -> creates new alert
        var result1 = await _service.RaiseAlertAsync(request1);
        result1.IsNewAlert.Should().BeTrue();
        result1.WasDeduplicated.Should().BeFalse();

        // 2. Second raise with same dedup key -> deduplicates into existing alert
        var request2 = new RaiseAlertRequest(
            AlertType: "PosSyncTimeout",
            Severity: AlertSeverity.Warning,
            Title: "POS Sync Lagging",
            Message: "Local sync queue exceeded 50 items (second attempt)",
            DeduplicationKey: dedupKey,
            SourceReferenceType: "Device",
            SourceReferenceId: terminalId);

        var result2 = await _service.RaiseAlertAsync(request2);
        result2.IsNewAlert.Should().BeFalse();
        result2.WasDeduplicated.Should().BeTrue();
        result2.Alert.AlertId.Should().Be(result1.Alert.AlertId);

        // 3. Third raise with same source reference
        var result3 = await _service.RaiseAlertAsync(request1);
        result3.IsNewAlert.Should().BeFalse();
        result3.WasDeduplicated.Should().BeTrue();
        result3.Alert.AlertId.Should().Be(result1.Alert.AlertId);

        // Verify total active alerts in DB for this device is strictly 1
        var activeAlerts = await _service.GetBySourceReferenceAsync("Device", terminalId);
        activeAlerts.Should().HaveCount(1);

        // Verify event log has 1 Created + 2 Deduplicated events
        var events = await _service.GetEventsAsync(result1.Alert.AlertId);
        events.Should().HaveCount(3);
        events[0].EventType.Should().Be(AlertEventType.Created);
        events[1].EventType.Should().Be(AlertEventType.Deduplicated);
        events[2].EventType.Should().Be(AlertEventType.Deduplicated);
    }

    [Fact]
    public async Task FullLifecycleTransitionsFromOpenToResolved()
    {
        var request = new RaiseAlertRequest(
            AlertType: "EInvoiceTransmissionFailure",
            Severity: AlertSeverity.Critical,
            Title: "QNB e-Invoice Delivery Failed",
            Message: "Gateway returned 503 Service Unavailable");

        var createResult = await _service.RaiseAlertAsync(request);
        var alertId = createResult.Alert.AlertId;

        // 1. Acknowledge
        var ackResult = await _service.AcknowledgeAlertAsync(new AcknowledgeAlertRequest(
            alertId,
            ExpectedRowVersion: 1,
            AcknowledgedBy: _userId,
            Reason: "Investigating gateway status"));

        ackResult.Status.Should().Be(AlertStatus.Acknowledged);
        ackResult.AcknowledgedAt.Should().NotBeNull();
        ackResult.AcknowledgedBy.Should().Be(_userId);
        ackResult.RowVersion.Should().Be(2);

        // 2. Escalate
        var escResult = await _service.EscalateAlertAsync(new EscalateAlertRequest(
            alertId,
            ExpectedRowVersion: 2,
            EscalatedBy: _userId,
            Reason: "Gateway down over 15 minutes, escalating to vendor support"));

        escResult.Status.Should().Be(AlertStatus.Escalated);
        escResult.RowVersion.Should().Be(3);

        // 3. Suppress
        var supResult = await _service.SuppressAlertAsync(new SuppressAlertRequest(
            alertId,
            ExpectedRowVersion: 3,
            SuppressedBy: _userId,
            Reason: "Vendor confirmed maintenance window"));

        supResult.Status.Should().Be(AlertStatus.Suppressed);
        supResult.RowVersion.Should().Be(4);

        // 4. Resolve
        var resResult = await _service.ResolveAlertAsync(new ResolveAlertRequest(
            alertId,
            ExpectedRowVersion: 4,
            ResolvedBy: _userId,
            ResolutionReason: "Maintenance finished, queue flushed successfully"));

        resResult.Status.Should().Be(AlertStatus.Resolved);
        resResult.ResolvedAt.Should().NotBeNull();
        resResult.ResolvedBy.Should().Be(_userId);
        resResult.ResolutionReason.Should().Be("Maintenance finished, queue flushed successfully");
        resResult.RowVersion.Should().Be(5);

        // Verify full event audit trail
        var events = await _service.GetEventsAsync(alertId);
        events.Should().HaveCount(5);
        events[0].EventType.Should().Be(AlertEventType.Created);
        events[1].EventType.Should().Be(AlertEventType.Acknowledged);
        events[2].EventType.Should().Be(AlertEventType.Escalated);
        events[3].EventType.Should().Be(AlertEventType.Suppressed);
        events[4].EventType.Should().Be(AlertEventType.Resolved);
    }

    [Fact]
    public async Task RaisingAlertAfterResolutionOpensNewAlert()
    {
        var sourceId = Guid.NewGuid();
        var dedupKey = $"BACKUP-FAIL-{sourceId}";

        var request = new RaiseAlertRequest(
            AlertType: "BackupFailed",
            Severity: AlertSeverity.Critical,
            Title: "Nightly Backup Failed",
            Message: "Disk full on backup target",
            DeduplicationKey: dedupKey,
            SourceReferenceType: "BackupJob",
            SourceReferenceId: sourceId);

        // 1. Raise and resolve
        var initial = await _service.RaiseAlertAsync(request);
        await _service.ResolveAlertAsync(new ResolveAlertRequest(
            initial.Alert.AlertId,
            ExpectedRowVersion: 1,
            ResolvedBy: _userId,
            ResolutionReason: "Cleaned up old archives"));

        // 2. Raise again -> should open NEW alert since previous was resolved
        var second = await _service.RaiseAlertAsync(request);
        second.IsNewAlert.Should().BeTrue();
        second.WasDeduplicated.Should().BeFalse();
        second.Alert.AlertId.Should().NotBe(initial.Alert.AlertId);
        second.Alert.Status.Should().Be(AlertStatus.Open);
    }

    [Fact]
    public async Task ActionOnResolvedAlertThrowsInvalidAlertStateException()
    {
        var request = new RaiseAlertRequest("TestType", AlertSeverity.Info, "Title", "Message");
        var result = await _service.RaiseAlertAsync(request);

        await _service.ResolveAlertAsync(new ResolveAlertRequest(
            result.Alert.AlertId,
            ExpectedRowVersion: 1,
            ResolvedBy: _userId,
            ResolutionReason: "Fixed"));

        // Attempting to acknowledge resolved alert
        var act = () => _service.AcknowledgeAlertAsync(new AcknowledgeAlertRequest(
            result.Alert.AlertId,
            ExpectedRowVersion: 2,
            AcknowledgedBy: _userId));

        var ex = await act.Should().ThrowAsync<InvalidAlertStateException>();
        ex.Which.CurrentStatus.Should().Be(AlertStatus.Resolved);
    }

    [Fact]
    public async Task ActionWithStaleRowVersionThrowsAlertConcurrencyException()
    {
        var request = new RaiseAlertRequest("TestType2", AlertSeverity.Info, "Title", "Message");
        var result = await _service.RaiseAlertAsync(request);

        var act = () => _service.AcknowledgeAlertAsync(new AcknowledgeAlertRequest(
            result.Alert.AlertId,
            ExpectedRowVersion: 99, // Stale version
            AcknowledgedBy: _userId));

        var ex = await act.Should().ThrowAsync<AlertConcurrencyException>();
        ex.Which.ExpectedVersion.Should().Be(99);
    }
}

/// <summary>
/// Migration tests verifying up/down cycle for 027-alerts.
/// </summary>
public sealed class PostgresAlertMigrationTests : IClassFixture<AlertTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAlertMigrationTests(AlertTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "027-alerts.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "027-alerts.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('observability.alerts')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be(DBNull.Value);
            }
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('observability.alert_events')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be(DBNull.Value);
            }
        }

        // 2. Run up.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(upSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables exist again
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('observability.alerts')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be("observability.alerts");
            }
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('observability.alert_events')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be("observability.alert_events");
            }
        }
    }
}

[CollectionDefinition(nameof(AlertTestFixtureDefinition), DisableParallelization = true)]
public sealed class AlertTestFixtureDefinition : ICollectionFixture<AlertTestDatabase>
{
}
