using ALKAROS.Observability.Foundation.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Observability.Foundation.Tests;

[Collection(nameof(ObservabilityTestFixtureDefinition))]
public sealed class PostgresHealthCheckRepositoryTests : IClassFixture<ObservabilityTestDatabase>
{
    private readonly ObservabilityTestDatabase _db;
    private readonly ObservabilityRedactionHook _redactionHook;
    private readonly PostgresHealthCheckRepository _repository;
    private readonly ObservabilityService _service;

    public PostgresHealthCheckRepositoryTests(ObservabilityTestDatabase db)
    {
        _db = db;
        _redactionHook = new ObservabilityRedactionHook();
        _repository = new PostgresHealthCheckRepository(_db.DataSource, _redactionHook);
        _service = new ObservabilityService(_repository, _redactionHook);
    }

    [Fact]
    public async Task RecordAndRetrieveHealthCheckSuccessfully()
    {
        var request = new RecordHealthCheckRequest(
            CheckType: "Database",
            Target: "PostgresPrimary",
            Status: HealthStatus.Healthy,
            RetentionPolicyId: RetentionPolicyCatalog.HotOperational7D,
            DetailsJson: "{\"latency_ms\": 2.5, \"active_connections\": 12}");

        var recorded = await _service.RecordHealthCheckAsync(request);

        recorded.Should().NotBeNull();
        recorded.CheckType.Should().Be("Database");
        recorded.Target.Should().Be("PostgresPrimary");
        recorded.Status.Should().Be(HealthStatus.Healthy);
        recorded.RetentionPolicyId.Should().Be(RetentionPolicyCatalog.HotOperational7D);

        // Fetch by ID
        var fetched = await _service.GetHealthCheckByIdAsync(recorded.HealthCheckId);
        fetched.Should().NotBeNull();
        fetched!.Target.Should().Be("PostgresPrimary");
        fetched.DetailsJson.Should().Contain("latency_ms");

        // Fetch latest by target
        var latest = await _service.GetLatestHealthChecksByTargetAsync("PostgresPrimary", limit: 5);
        latest.Should().NotBeEmpty();
        latest.Should().Contain(h => h.HealthCheckId == recorded.HealthCheckId);
    }

    [Fact]
    public async Task RecordHealthCheckRedactsSensitivePayloadBeforePersistence()
    {
        var request = new RecordHealthCheckRequest(
            CheckType: "PaymentGateway",
            Target: "QnbGateway",
            Status: HealthStatus.Degraded,
            RetentionPolicyId: RetentionPolicyCatalog.StandardOperational30D,
            DetailsJson: "{\"endpoint\":\"https://api.qnb.com\",\"api_key\":\"live_secret_key_123\",\"response_time_ms\":4500}");

        var recorded = await _service.RecordHealthCheckAsync(request);

        recorded.DetailsJson.Should().NotContain("live_secret_key_123");
        recorded.DetailsJson.Should().Contain(ObservabilityRedactionHook.RedactedPlaceholder);

        // Verify direct from DB
        var direct = await _service.GetHealthCheckByIdAsync(recorded.HealthCheckId);
        direct!.DetailsJson.Should().NotContain("live_secret_key_123");
        direct.DetailsJson.Should().Contain(ObservabilityRedactionHook.RedactedPlaceholder);
    }

    [Fact]
    public async Task RecordHealthCheckWithoutApprovedRetentionPolicyThrowsException()
    {
        var request = new RecordHealthCheckRequest(
            CheckType: "Printer",
            Target: "KitchenPrinter01",
            Status: HealthStatus.Healthy,
            RetentionPolicyId: "UNAPPROVED_CUSTOM_POLICY");

        var act = () => _service.RecordHealthCheckAsync(request);

        var ex = await act.Should().ThrowAsync<UnapprovedRetentionPolicyException>();
        ex.Which.PolicyId.Should().Be("UNAPPROVED_CUSTOM_POLICY");
    }

    [Fact]
    public async Task GetUnhealthyChecksReturnsOnlyDegradedAndUnhealthy()
    {
        await _service.RecordHealthCheckAsync(new RecordHealthCheckRequest(
            "Disk", "DiskRoot", HealthStatus.Healthy, RetentionPolicyCatalog.HotOperational7D));

        var degraded = await _service.RecordHealthCheckAsync(new RecordHealthCheckRequest(
            "Disk", "DiskBackup", HealthStatus.Degraded, RetentionPolicyCatalog.HotOperational7D, "{\"free_percent\":12}"));

        var unhealthy = await _service.RecordHealthCheckAsync(new RecordHealthCheckRequest(
            "Printer", "BarPrinter", HealthStatus.Unhealthy, RetentionPolicyCatalog.HotOperational7D, "{\"error\":\"Paper jam\"}"));

        var unhealthyList = await _service.GetUnhealthyChecksAsync();

        unhealthyList.Should().Contain(h => h.HealthCheckId == degraded.HealthCheckId);
        unhealthyList.Should().Contain(h => h.HealthCheckId == unhealthy.HealthCheckId);
        unhealthyList.Should().NotContain(h => h.Status == HealthStatus.Healthy);
    }
}

/// <summary>
/// Migration tests verifying up/down cycle for 028-health-checks.
/// </summary>
public sealed class PostgresHealthCheckMigrationTests : IClassFixture<ObservabilityTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresHealthCheckMigrationTests(ObservabilityTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "028-health-checks.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "028-health-checks.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify table dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('observability.health_checks')::text;", connection))
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

        // Verify table exists again
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('observability.health_checks')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be("observability.health_checks");
            }
        }
    }
}

[CollectionDefinition(nameof(ObservabilityTestFixtureDefinition), DisableParallelization = true)]
public sealed class ObservabilityTestFixtureDefinition : ICollectionFixture<ObservabilityTestDatabase>
{
}
