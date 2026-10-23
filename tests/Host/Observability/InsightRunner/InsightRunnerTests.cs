using ALKAROS.Host.Observability.InsightRunner;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.Observability.PostgresInsight;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Observability.InsightRunner.Tests;

public sealed class RunnerDatabase : PgTestDatabase
{
    public RunnerDatabase() : base("alkaros_rmd563_") { }

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

public sealed class InsightRunnerTests : IClassFixture<RunnerDatabase>
{
    private readonly RunnerDatabase _database;

    public InsightRunnerTests(RunnerDatabase database) => _database = database;

    [Fact]
    public async Task RealLockChainBecomesAStoredLockWaitEvent()
    {
        await _database.ExecuteAsync("CREATE TABLE runner_probe (id int primary key);");
        await _database.ExecuteAsync("INSERT INTO runner_probe VALUES (1);");
        await using var holder = await _database.DataSource.OpenConnectionAsync();
        await using (var begin = new NpgsqlCommand("BEGIN; UPDATE runner_probe SET id = 1 WHERE id = 1;", holder))
            await begin.ExecuteNonQueryAsync();
        var blocked = Task.Run(async () =>
        {
            await using var waiter = await _database.DataSource.OpenConnectionAsync();
            await using var update = new NpgsqlCommand("UPDATE runner_probe SET id = 1 WHERE id = 1;", waiter);
            await update.ExecuteNonQueryAsync();
        });
        await WaitUntilBlockedAsync(_database.DataSource);
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));
        var runner = new InsightRunnerHostedService(new PostgresInsightService(_database.DataSource), writer);

        var raised = await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();
        await using (var commit = new NpgsqlCommand("COMMIT;", holder))
            await commit.ExecuteNonQueryAsync();
        await blocked;

        raised.Should().BeGreaterThan(0);
        (await _database.ScalarAsync<long>(
            "SELECT count(*) FROM observability.error_events WHERE category = 'db.lock_wait' AND layer = 'Database';"))
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FailingRoundIsCountedAndTheLoopKeepsRunning()
    {
        await using var broken = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=2;Pooling=false");
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));
        using var runner = new InsightRunnerHostedService(
            new PostgresInsightService(broken), writer, TimeSpan.FromMilliseconds(30));

        await runner.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => runner.Failures > 1);
        await runner.StopAsync(CancellationToken.None);

        runner.Failures.Should().BeGreaterThan(1);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition did not become true.");
            await Task.Delay(10);
        }
    }

    /// <summary>Waits until some session of this database is blocked on a lock, instead of guessing how long that takes.</summary>
    private static async Task WaitUntilBlockedAsync(NpgsqlDataSource dataSource)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = dataSource.CreateCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock';");
            if ((long)(await command.ExecuteScalarAsync())! > 0) return;
            await Task.Delay(10);
        }

        throw new TimeoutException("No session started waiting for a lock.");
    }
}
