using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Observability.PostgresInsight.Tests;

public sealed class InsightDatabase : PgTestDatabase
{
    public InsightDatabase() : base("alkaros_rmd546_") { }

    protected override Task ApplySqlAsync() => Task.CompletedTask;
}

public sealed class PostgresInsightTests : IClassFixture<InsightDatabase>
{
    private readonly InsightDatabase _database;

    public PostgresInsightTests(InsightDatabase database) => _database = database;

    private PostgresInsightService Service(PostgresInsightOptions? options = null) => new(_database.DataSource, options);

    [Fact]
    public async Task MissingStatementExtensionIsReportedAsOffWithoutError()
    {
        var snapshot = await Service().ReadAsync();

        snapshot.StatementStatsAvailable.Should().BeFalse();
        snapshot.SlowStatements.Should().BeEmpty();
    }

    [Fact]
    public async Task ArtificialSlowQueryIsListedWhileRunning()
    {
        var running = _database.ScalarAsync<int>("SELECT 1 FROM (SELECT pg_sleep(4)) s;");

        await Task.Delay(1500);
        var snapshot = await Service().ReadAsync();
        await running;

        snapshot.LongRunning.Should().Contain(q => q.Query.Contains("pg_sleep") && q.Seconds >= 1);
    }

    [Fact]
    public async Task LockChainShowsTheBlockedSessionAndRaisesAnErrorEvent()
    {
        await _database.ExecuteAsync("CREATE TABLE lock_probe (id int primary key);");
        await _database.ExecuteAsync("INSERT INTO lock_probe VALUES (1);");
        await using var holder = await _database.DataSource.OpenConnectionAsync();
        await using var begin = new NpgsqlCommand("BEGIN; UPDATE lock_probe SET id = 1 WHERE id = 1;", holder);
        await begin.ExecuteNonQueryAsync();
        var blocked = Task.Run(async () =>
        {
            await using var waiter = await _database.DataSource.OpenConnectionAsync();
            await using var update = new NpgsqlCommand("UPDATE lock_probe SET id = 1 WHERE id = 1;", waiter);
            await update.ExecuteNonQueryAsync();
        });

        await Task.Delay(1500);
        var service = Service();
        var snapshot = await service.ReadAsync();
        await using (var commit = new NpgsqlCommand("COMMIT;", holder))
            await commit.ExecuteNonQueryAsync();
        await blocked;

        snapshot.Blocked.Should().NotBeEmpty();
        service.ToEvents(snapshot).Should().Contain(e => e.Category == "db.lock_wait" && e.Severity == ErrorSeverity.Error);
    }

    [Fact]
    public void ThresholdsDecideWhichFindingsBecomeEvents()
    {
        var service = Service();
        var calm = new PostgresInsightSnapshot(
            true, [new SlowStatement("select 1", 10, 5, 50)], [new RunningQuery(1, 2, "active", "q")], [], 10, 100, []);
        var hot = new PostgresInsightSnapshot(
            true,
            [new SlowStatement("select slow", 3, 900, 2700)],
            [new RunningQuery(1, 45, "active", "q")],
            [],
            90,
            100,
            [new BloatedTable("public.t", 5000, 0.4)]);

        service.ToEvents(calm).Should().BeEmpty();
        service.ToEvents(hot).Select(e => e.Category).Should().BeEquivalentTo(
            "db.long_running_query", "db.connection_saturation", "db.slow_statement", "db.table_bloat");
    }
}
