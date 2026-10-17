using ALKAROS.Audit.EventStore;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Observability.ServerDetectors.Tests;

public sealed class DetectorsDatabase : PgTestDatabase
{
    public DetectorsDatabase() : base("alkaros_rmd534_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        var file = Directory.GetFiles(
            Path.Combine(root, "database", "migrations", "V1", "V1-RMD-533"), "187-*.up.sql").Single();
        await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        var interactions = Directory.GetFiles(
            Path.Combine(root, "database", "migrations", "V1", "V1-RMD-541"), "191-*.up.sql").Single();
        await RunAsync(DataSource, await File.ReadAllTextAsync(interactions));
        foreach (var (task, prefix) in new[] { ("V1-OBS-001", "028-"), ("V1-OPS-002", "029-"), ("V1-RMD-538", "188-") })
        {
            var migration = Directory.GetFiles(Path.Combine(root, "database", "migrations", "V1", task), prefix + "*.up.sql").Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(migration));
        }
    }
}

public sealed class ServerDetectorsTests : IClassFixture<DetectorsDatabase>
{
    private readonly DetectorsDatabase _database;

    public ServerDetectorsTests(DetectorsDatabase database) => _database = database;

    private ErrorEventWriter NewWriter() =>
        new(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));

    private async Task<long> CountCategoryAsync(string category) =>
        await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM observability.error_events WHERE category = '{category}';");

    private Task<long> ApiOccurrencesAsync() => _database.ScalarAsync<long>(
        "SELECT COALESCE(sum(occurrence_count), 0)::bigint FROM observability.error_events WHERE category = 'api.unhandled_exception';");

    private static async Task<bool> ObserveAsync(ErrorEventWriter writer, Exception exception)
    {
        var observer = new ServerErrorObserver(writer);
        var handled = await observer.TryHandleAsync(new DefaultHttpContext(), exception, CancellationToken.None);
        await writer.FlushAsync();
        return handled;
    }

    [Fact]
    public async Task UnhandledExceptionIsRecordedOnceAsCriticalAndNotSwallowed()
    {
        await using var writer = NewWriter();

        var handled = await ObserveAsync(writer, new InvalidOperationException("boom"));

        handled.Should().BeFalse();
        (await CountCategoryAsync("api.unhandled_exception")).Should().Be(1);
        (await _database.ScalarAsync<string>(
            "SELECT severity FROM observability.error_events WHERE category = 'api.unhandled_exception';"))
            .Should().Be("Critical");
    }

    [Fact]
    public async Task ClientErrorsProduceNoEvent()
    {
        await using var writer = NewWriter();
        var before = await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events;");

        await ObserveAsync(writer, new DualScreenNotFoundException("missing"));
        await ObserveAsync(writer, new DualScreenConflictException("conflict"));

        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events;")).Should().Be(before);
    }

    [Fact]
    public async Task LockNotAvailableDatabaseFaultIsRecorded()
    {
        await using var writer = NewWriter();
        var apiBefore = await ApiOccurrencesAsync();
        var fault = new PostgresException("lock timeout", "ERROR", "ERROR", "55P03");

        await ObserveAsync(writer, new InvalidOperationException("wrapped", fault));

        (await CountCategoryAsync("db.lock_not_available")).Should().Be(1);
        (await ApiOccurrencesAsync()).Should().Be(apiBefore);
    }

    [Fact]
    public async Task ObserverFailureDoesNotBreakTheErrorHandlerChain()
    {
        await using var writer = NewWriter();
        await writer.DisposeAsync();
        var observer = new ServerErrorObserver(writer);

        var handled = await observer.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("x"), CancellationToken.None);

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task SlowRequestOverThresholdIsOneWarningAndUnderIsNone()
    {
        await using var writer = NewWriter();
        var options = new SlowRequestOptions(TimeSpan.FromMilliseconds(50));
        var slow = new SlowRequestMiddleware(async _ => await Task.Delay(120), writer, options);
        var fast = new SlowRequestMiddleware(_ => Task.CompletedTask, writer, options);

        await fast.InvokeAsync(new DefaultHttpContext());
        await writer.FlushAsync();
        (await CountCategoryAsync("api.slow_request")).Should().Be(0);

        await slow.InvokeAsync(new DefaultHttpContext());
        await writer.FlushAsync();
        (await CountCategoryAsync("api.slow_request")).Should().Be(1);
        (await _database.ScalarAsync<string>(
            "SELECT severity FROM observability.error_events WHERE category = 'api.slow_request';"))
            .Should().Be("Warning");
    }

    [Fact]
    public async Task UnhandledExceptionCarriesTheRoutePatternOfTheEndpointThatThrew()
    {
        await using var writer = NewWriter();
        var context = new DefaultHttpContext();
        var endpoint = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/e2e/orders/{id}"), 0, EndpointMetadataCollection.Empty, "e2e");
        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = new InvalidOperationException("boom"), Endpoint = endpoint });

        await new ServerErrorObserver(writer).TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);
        await writer.FlushAsync();

        (await _database.ScalarAsync<long>(
            "SELECT count(*) FROM observability.error_events WHERE category = 'api.unhandled_exception' AND detail LIKE '%/e2e/orders/{id}%';"))
            .Should().Be(1);
        await _database.ExecuteAsync("DELETE FROM observability.error_events WHERE detail LIKE '%/e2e/orders/{id}%';");
    }

    [Fact]
    public async Task ServerErrorAnsweredByTheEndpointIsRecordedOnce()
    {
        await using var writer = NewWriter();
        var middleware = new SlowRequestMiddleware(context =>
        {
            context.Response.StatusCode = 500;
            return Task.CompletedTask;
        }, writer);

        await middleware.InvokeAsync(new DefaultHttpContext());
        await writer.FlushAsync();

        (await CountCategoryAsync("api.server_error_response")).Should().Be(1);
    }

    [Fact]
    public async Task ServerErrorAlreadyReportedByTheExceptionObserverIsNotReportedTwice()
    {
        await using var writer = NewWriter();
        var observer = new ServerErrorObserver(writer);
        var middleware = new SlowRequestMiddleware(async context =>
        {
            await observer.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);
            context.Response.StatusCode = 500;
        }, writer);

        await middleware.InvokeAsync(new DefaultHttpContext());
        await writer.FlushAsync();

        (await CountCategoryAsync("api.unhandled_exception")).Should().Be(1);
        (await CountCategoryAsync("api.server_error_response")).Should().Be(0);
    }

    [Fact]
    public async Task ClientErrorAndSuccessAreNotReportedAsServerErrors()
    {
        await using var writer = NewWriter();
        foreach (var status in new[] { 200, 404 })
        {
            var middleware = new SlowRequestMiddleware(context =>
            {
                context.Response.StatusCode = status;
                return Task.CompletedTask;
            }, writer);
            await middleware.InvokeAsync(new DefaultHttpContext());
        }

        await writer.FlushAsync();

        (await CountCategoryAsync("api.server_error_response")).Should().Be(0);
    }

    [Fact]
    public async Task RetentionRunPurgesOldRowsAndWritesOneAuditEvent()
    {
        await _database.ExecuteAsync(
            """
            INSERT INTO observability.error_events (fingerprint, layer, severity, category, first_seen_at, last_seen_at, occurrence_count)
            VALUES ('old-fp', 'Api', 'Error', 'api.old', now() - interval '200 days', now() - interval '200 days', 1),
                   ('new-fp', 'Api', 'Error', 'api.new', now(), now(), 1);
            """);
        var audit = new RecordingAudit();
        var service = new ErrorRetentionHostedService(new MonitoringDataSource(_database.DataSource), audit);

        await service.RunOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        (await CountCategoryAsync("api.old")).Should().Be(0);
        (await CountCategoryAsync("api.new")).Should().Be(1);
        audit.Events.Should().ContainSingle(e => e.EventName == "monitoring.retention.run");
    }

    [Fact]
    public async Task RetentionRunAlsoPurgesHealthSnapshotsHealthChecksAndNotificationRows()
    {
        await _database.ExecuteAsync(
            """
            INSERT INTO operations.system_health_snapshots (snapshot_id, database_status, disk_status, last_backup_status, free_disk_bytes, database_size_bytes, captured_at)
            VALUES (gen_random_uuid(), 'Healthy', 'Healthy', 'Healthy', 1, 1, now() - interval '31 days'),
                   (gen_random_uuid(), 'Healthy', 'Healthy', 'Healthy', 1, 1, now() - interval '29 days');
            INSERT INTO observability.notification_queue (fingerprint, layer, category, severity, kind, enqueued_at, next_attempt_at)
            VALUES ('q-old', 'Api', 'c', 'Error', 'Digest', now() - interval '31 days', now()),
                   ('q-new', 'Api', 'c', 'Error', 'Digest', now() - interval '29 days', now());
            INSERT INTO observability.notification_suppressions (fingerprint, last_sent_at)
            VALUES ('s-old', now() - interval '31 days'), ('s-new', now() - interval '29 days');
            INSERT INTO observability.health_checks (health_check_id, check_type, target, status, checked_at, retention_policy_id)
            VALUES (gen_random_uuid(), 't', 'hot-old', 'Healthy', now() - interval '7 days 1 hour', 'HOT_7D'),
                   (gen_random_uuid(), 't', 'hot-new', 'Healthy', now() - interval '6 days 23 hours', 'HOT_7D'),
                   (gen_random_uuid(), 't', 'op-old', 'Healthy', now() - interval '30 days 1 hour', 'OPERATIONAL_30D'),
                   (gen_random_uuid(), 't', 'op-new', 'Healthy', now() - interval '29 days 23 hours', 'OPERATIONAL_30D'),
                   (gen_random_uuid(), 't', 'au-old', 'Healthy', now() - interval '90 days 1 hour', 'AUDIT_90D'),
                   (gen_random_uuid(), 't', 'au-new', 'Healthy', now() - interval '89 days 23 hours', 'AUDIT_90D'),
                   (gen_random_uuid(), 't', 'ex-old', 'Healthy', now() - interval '365 days 1 hour', 'audit_365d'),
                   (gen_random_uuid(), 't', 'ex-new', 'Healthy', now() - interval '364 days 23 hours', 'audit_365d'),
                   (gen_random_uuid(), 't', 'lc-old', 'Healthy', now() - interval '7 days 1 hour', ' hot_7d '),
                   (gen_random_uuid(), 't', 'lc-new', 'Healthy', now() - interval '6 days 23 hours', ' hot_7d '),
                   (gen_random_uuid(), 't', 'zz-old', 'Healthy', now() - interval '400 days', 'UNKNOWN'),
                   (gen_random_uuid(), 't', 'zz-new', 'Healthy', now() - interval '300 days', 'UNKNOWN');
            INSERT INTO observability.health_checks (health_check_id, check_type, target, status, checked_at, retention_policy_id)
            SELECT gen_random_uuid(), 't', 'bulk', 'Healthy', now() - interval '10 days', 'HOT_7D' FROM generate_series(1, 5001);
            """);
        var audit = new RecordingAudit();
        var service = new ErrorRetentionHostedService(new MonitoringDataSource(_database.DataSource), audit);

        await service.RunOnceAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        (await _database.ScalarAsync<long>("SELECT count(*) FROM operations.system_health_snapshots;")).Should().Be(1);
        (await _database.ScalarAsync<string>("SELECT string_agg(fingerprint, ',') FROM observability.notification_queue;")).Should().Be("q-new");
        (await _database.ScalarAsync<string>("SELECT string_agg(fingerprint, ',') FROM observability.notification_suppressions;")).Should().Be("s-new");
        (await _database.ScalarAsync<string>("SELECT string_agg(target, ',' ORDER BY target) FROM observability.health_checks;"))
            .Should().Be("au-new,ex-new,hot-new,lc-new,op-new,zz-new");
        audit.Events.Last().AfterStateJson.Should().Contain("observability.health_checks\":5007")
            .And.Contain("operations.system_health_snapshots\":1");
    }

    private sealed class RecordingAudit : IAuditEventStore
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task AppendBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AuditEvent>> GetByAggregateAsync(string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
