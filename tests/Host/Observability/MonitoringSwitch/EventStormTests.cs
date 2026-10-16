using System.Diagnostics;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Observability.MonitoringSwitch.Tests;

public sealed class StormDatabase : PgTestDatabase
{
    public StormDatabase() : base("alkaros_rmd571_") { }

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

/// <summary>
/// An event storm goes through the writer's bounded queue and the monitoring pool only: the application pool's
/// connection use must stay what the same workload needs without a storm. The run length comes from
/// ALKAROS_STORM_SECONDS (default 5); the numbers are written to ALKAROS_STORM_REPORT when set.
/// </summary>
public sealed class EventStormTests : IClassFixture<StormDatabase>
{
    private const string MainApplication = "alkaros-main-storm-test";
    private const int EventsPerSecond = 1000;

    private readonly StormDatabase _database;

    public EventStormTests(StormDatabase database) => _database = database;

    private static int Seconds =>
        int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_STORM_SECONDS"), out var seconds) && seconds > 0 ? seconds : 5;

    private (NpgsqlDataSource Main, NpgsqlDataSource Monitoring) Pools()
    {
        var connection = _database.DataSource.ConnectionString;
        var main = new NpgsqlConnectionStringBuilder(connection) { ApplicationName = MainApplication, MaxPoolSize = 20 };
        var password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD");
        if (password is not null)
            main.Password = password;
        var monitoring = new NpgsqlConnectionStringBuilder(main.ConnectionString);
        return (NpgsqlDataSource.Create(main.ConnectionString), MonitoringDataSource.Create(monitoring.ConnectionString));
    }

    private static async Task<(int Main, int Monitoring)> SampleConnectionsAsync(NpgsqlDataSource probe)
    {
        await using var command = probe.CreateCommand(
            "SELECT count(*) FILTER (WHERE application_name = @main), count(*) FILTER (WHERE application_name = @monitoring) FROM pg_stat_activity WHERE datname = current_database();");
        command.Parameters.AddWithValue("main", MainApplication);
        command.Parameters.AddWithValue("monitoring", MonitoringDataSource.ApplicationName);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return ((int)reader.GetInt64(0), (int)reader.GetInt64(1));
    }

    private static async Task<(int MaxMain, int MaxMonitoring, long Queries)> RunPhaseAsync(
        NpgsqlDataSource main, NpgsqlDataSource probe, int seconds, Func<CancellationToken, Task>? storm)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        long queries = 0;
        var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                await using var command = main.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync();
                Interlocked.Increment(ref queries);
            }
        })).ToList();
        var stormTask = storm is null ? Task.CompletedTask : storm(stop.Token);

        int maxMain = 0, maxMonitoring = 0;
        while (!stop.IsCancellationRequested)
        {
            var (m, mon) = await SampleConnectionsAsync(probe);
            maxMain = Math.Max(maxMain, m);
            maxMonitoring = Math.Max(maxMonitoring, mon);
            await Task.Delay(100);
        }

        await Task.WhenAll(workers);
        await stormTask;
        return (maxMain, maxMonitoring, queries);
    }

    [Fact]
    public async Task ThousandEventsPerSecondNeverTouchesTheApplicationPoolAndStaysInsideTheMonitoringPool()
    {
        using var probeSource = NpgsqlDataSource.Create(_database.DataSource.ConnectionString + ";Application Name=storm-probe;Password=" +
            (Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD") ?? ""));
        var (main, monitoring) = Pools();
        using var mainSource = main;
        using var monitoringSource = monitoring;
        await using var writer = new ErrorEventWriter(monitoringSource, flushInterval: TimeSpan.FromMilliseconds(500));
        writer.Start();
        await RunPhaseAsync(main, probeSource, 1, null);
        var baseline = await RunPhaseAsync(main, probeSource, Seconds, null);

        long enqueued = 0;
        long refused = 0;
        var storm = await RunPhaseAsync(main, probeSource, Seconds, async token =>
        {
            var clock = Stopwatch.StartNew();
            long sent = 0;
            while (!token.IsCancellationRequested)
            {
                var due = (long)(clock.Elapsed.TotalSeconds * EventsPerSecond);
                for (; sent < due; sent++)
                {
                    var input = new ErrorEventInput(ErrorLayer.Api, ErrorSeverity.Warning, $"storm.category_{sent % 200}", $"detail {sent % 200}");
                    if (writer.TryEnqueue(input))
                        Interlocked.Increment(ref enqueued);
                    else
                        Interlocked.Increment(ref refused);
                }

                await Task.Delay(1, CancellationToken.None);
            }
        });
        await writer.FlushAsync();

        var report = $"seconds={Seconds} baselineMaxMainConnections={baseline.MaxMain} stormMaxMainConnections={storm.MaxMain} " +
            $"stormMaxMonitoringConnections={storm.MaxMonitoring} baselineQueries={baseline.Queries} stormQueries={storm.Queries} " +
            $"enqueued={enqueued} refusedFullQueue={refused} droppedPending={writer.DroppedPending} failedPending={writer.FailedPending}";
        var path = Environment.GetEnvironmentVariable("ALKAROS_STORM_REPORT");
        if (path is not null)
            await File.WriteAllTextAsync(path, report);

        enqueued.Should().BeGreaterThan(EventsPerSecond * Seconds / 2, report);
        storm.MaxMain.Should().BeLessThanOrEqualTo(baseline.MaxMain, report);
        storm.MaxMonitoring.Should().BeLessThanOrEqualTo(MonitoringDataSource.MaxConnections, report);
        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events WHERE category LIKE 'storm.%';"))
            .Should().BeGreaterThan(0, report);
    }
}
