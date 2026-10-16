using System.Diagnostics;
using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using ALKAROS.Host.Observability.ServerDetectorsDeep;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorAnalysis;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.QrRelay.LocalConnector;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Observability.ServerDetectorsDeep.Tests;

public sealed class DeepDatabase : PgTestDatabase
{
    public DeepDatabase() : base("alkaros_rmd558_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        var file = Directory.GetFiles(
            Path.Combine(root, "database", "migrations", "V1", "V1-RMD-533"), "187-*.up.sql").Single();
        await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}

public sealed class ServerDetectorsDeepTests : IClassFixture<DeepDatabase>
{
    private readonly DeepDatabase _database;

    public ServerDetectorsDeepTests(DeepDatabase database) => _database = database;

    private sealed class FakeTunnel(RelayConnectorState state) : IRelayConnectorStatusReporter
    {
        public RelayConnectorState State { get; set; } = state;

        public Task<RelayConnectorStatus> GetCurrentStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new RelayConnectorStatus(State, null, 0, null));
    }

    private static MaintenanceJobStatusV1 Job(string name, int intervalSeconds, DateTimeOffset? lastRunAt, bool enabled = true) =>
        new(name, "d", intervalSeconds, enabled, null, "Succeeded", lastRunAt, 1, null);

    private async Task ResetAsync() =>
        await _database.ExecuteAsync("DELETE FROM observability.error_occurrences; DELETE FROM observability.error_events;");

    private Task<long> CountAsync(string category, string status = "Open") =>
        _database.ScalarAsync<long>($"SELECT count(*) FROM observability.error_events WHERE category = '{category}' AND status = '{status}';");

    private ErrorEventWriter NewWriter() => new(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));

    private static async Task<List<NpgsqlConnection>> HoldAsync(NpgsqlDataSource source, int count)
    {
        var held = new List<NpgsqlConnection>();
        for (var i = 0; i < count; i++)
            held.Add(await source.OpenConnectionAsync());
        return held;
    }

    private NpgsqlDataSource SmallPool(string? applicationName = null)
    {
        var builder = new NpgsqlConnectionStringBuilder(_database.DataSource.ConnectionString) { MaxPoolSize = 10 };
        if (applicationName is not null)
            builder.ApplicationName = applicationName;
        return NpgsqlDataSource.Create(builder.ConnectionString);
    }

    private ServerDetectorsDeepRunner Runner(
        ErrorEventWriter writer, PoolUsageReader pool, Func<IReadOnlyList<MaintenanceJobStatusV1>>? jobs = null,
        IRelayConnectorStatusReporter? tunnel = null, Func<DateTimeOffset>? clock = null) =>
        new(_database.DataSource, writer, pool, jobs, tunnel, clock: clock);

    [Fact]
    public async Task NearlyFullApplicationPoolIsOneWarningAndResolvesWhenItDrains()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var pool = new PoolUsageReader();
        await using var small = SmallPool();
        var runner = Runner(writer, pool);

        var held = await HoldAsync(small, 9);
        await runner.RunOnceAsync(CancellationToken.None);
        await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();
        (await CountAsync("db.pool_saturation")).Should().Be(1);
        (await _database.ScalarAsync<string>("SELECT severity FROM observability.error_events WHERE category = 'db.pool_saturation';")).Should().Be("Warning");

        foreach (var connection in held)
            await connection.DisposeAsync();
        await runner.RunOnceAsync(CancellationToken.None);
        (await CountAsync("db.pool_saturation", "Resolved")).Should().Be(1);
    }

    [Fact]
    public async Task TheMonitoringPoolNeverReportsOnItself()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var pool = new PoolUsageReader();
        await using var monitoring = SmallPool(MonitoringDataSource.ApplicationName);

        var held = await HoldAsync(monitoring, 9);
        await Runner(writer, pool).RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();

        (await CountAsync("db.pool_saturation")).Should().Be(0);
        foreach (var connection in held)
            await connection.DisposeAsync();
    }

    [Fact]
    public async Task SlowStatementsAreWarningOrErrorButMonitoringWritesAreIgnored()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var tap = new DbActivityTap(writer);

        await using (var warn = _database.DataSource.CreateCommand("SELECT pg_sleep(0.7) AS warn_case"))
            await warn.ExecuteNonQueryAsync();
        await using (var error = _database.DataSource.CreateCommand("SELECT pg_sleep(2.1) AS error_case"))
            await error.ExecuteNonQueryAsync();
        await using (var own = _database.DataSource.CreateCommand(
            "SELECT pg_sleep(0.7), (SELECT count(*) FROM observability.error_events)"))
            await own.ExecuteNonQueryAsync();
        await writer.FlushAsync();

        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events WHERE category = 'db.slow_query' AND severity = 'Warning';")).Should().Be(1);
        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events WHERE category = 'db.slow_query' AND severity = 'Error';")).Should().Be(1);
        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events WHERE category = 'db.slow_query' AND detail LIKE '%observability%';")).Should().Be(0);
    }

    [Fact]
    public async Task RunnerBeatsItsHeartbeatEachRoundAndIsReportedSilentOnceItStops()
    {
        var now = DateTimeOffset.UtcNow;
        var heartbeats = new DetectorHeartbeats(() => now);
        await using var writer = NewWriter();
        using var pool = new PoolUsageReader();
        using var runner = new ServerDetectorsDeepRunner(
            _database.DataSource, writer, pool, null, null, clock: () => now, interval: TimeSpan.FromSeconds(15), heartbeats: heartbeats);
        await runner.StartAsync(CancellationToken.None);
        await Task.Delay(300);

        heartbeats.Degraded().Should().BeEmpty();
        now += TimeSpan.FromSeconds(31);
        heartbeats.Degraded().Should().ContainSingle().Which.Detail.Should().StartWith("server-detectors-deep silent");

        await runner.RunOnceAsync(CancellationToken.None);
        heartbeats.Degraded().Should().BeEmpty();
        await runner.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RunnerWithoutHeartbeatsStillRuns()
    {
        await using var writer = NewWriter();
        using var pool = new PoolUsageReader();
        using var runner = new ServerDetectorsDeepRunner(_database.DataSource, writer, pool, null, null);

        await runner.RunOnceAsync(CancellationToken.None);

        runner.Failures.Should().Be(0);
    }

    private async Task RunRequestAsync(int queries)
    {
        using var source = new ActivitySource("Microsoft.AspNetCore");
        using var request = source.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn");
        request!.SetTag("http.route", "/api/v1/orders/{id}");
        for (var i = 0; i < queries; i++)
        {
            await using var command = _database.DataSource.CreateCommand($"SELECT {i} AS q");
            await command.ExecuteScalarAsync();
        }
    }

    [Fact]
    public async Task RequestWithMoreThanTwentyStatementsIsOneNPlusOneEventWithTheLastEightAsSamples()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var tap = new DbActivityTap(writer);

        await RunRequestAsync(25);
        await writer.FlushAsync();

        (await CountAsync("db.n_plus_one")).Should().Be(1);
        var detail = await _database.ScalarAsync<string>("SELECT detail FROM observability.error_events WHERE category = 'db.n_plus_one';");
        detail.Should().StartWith("/api/v1/orders/{id} ran 25 queries; last: ");
        detail.Split(" | ").Should().HaveCount(8);
    }

    [Fact]
    public async Task RequestWithTwentyStatementsOrFewerIsNotReported()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var tap = new DbActivityTap(writer);

        await RunRequestAsync(20);
        await writer.FlushAsync();

        (await CountAsync("db.n_plus_one")).Should().Be(0);
    }

    [Fact]
    public async Task ScheduledJobThatMissedTwiceItsIntervalIsAnEventAndResolvesAfterARun()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var pool = new PoolUsageReader();
        var now = DateTimeOffset.UtcNow;
        var jobs = new List<MaintenanceJobStatusV1>
        {
            Job("late", 3600, now.AddHours(-3)), Job("fresh", 3600, now.AddMinutes(-10)), Job("off", 3600, now.AddDays(-9), enabled: false),
        };
        var runner = Runner(writer, pool, () => jobs, clock: () => now);

        await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();
        (await CountAsync("background.job_missed")).Should().Be(1);
        (await _database.ScalarAsync<string>("SELECT detail FROM observability.error_events WHERE category = 'background.job_missed';")).Should().Contain("job=late");

        jobs[0] = Job("late", 3600, now);
        await runner.RunOnceAsync(CancellationToken.None);
        (await CountAsync("background.job_missed", "Resolved")).Should().Be(1);
    }

    [Fact]
    public async Task DeadQrTunnelIsAnEventAndNotConfiguredIsNot()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var pool = new PoolUsageReader();
        var tunnel = new FakeTunnel(RelayConnectorState.NotConfigured);
        var runner = Runner(writer, pool, tunnel: tunnel);

        await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();
        (await CountAsync("integration.qr_tunnel_down")).Should().Be(0);

        tunnel.State = RelayConnectorState.Restarting;
        await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();
        (await CountAsync("integration.qr_tunnel_down")).Should().Be(1);

        tunnel.State = RelayConnectorState.Running;
        await runner.RunOnceAsync(CancellationToken.None);
        (await CountAsync("integration.qr_tunnel_down", "Resolved")).Should().Be(1);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE a = 'secret' AND b = 42", "SELECT * FROM t WHERE a = '?' AND b = ?")]
    [InlineData("SELECT   1\n FROM x", "SELECT ? FROM x")]
    public void StatementShapeHidesLiteralsAndWhitespace(string statement, string expected) =>
        DbActivityTap.Shape(statement).Should().Be(expected);
}
