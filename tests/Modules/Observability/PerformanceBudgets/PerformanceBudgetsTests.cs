using ALKAROS.Observability.ErrorEvents;
using ALKAROS.Observability.PerformanceBudgets;
using ALKAROS.Observability.PerformanceMetrics;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Observability.PerformanceBudgets.Tests;

public sealed class BudgetDatabase : PgTestDatabase
{
    public BudgetDatabase() : base("alkaros_rmd544_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        foreach (var (folder, pattern) in new[] { ("V1-RMD-533", "187-*.up.sql"), ("V1-RMD-543", "189-*.up.sql"), ("V1-RMD-544", "190-*.up.sql") })
        {
            var file = Directory.GetFiles(Path.Combine(root, "database", "migrations", "V1", folder), pattern).Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public sealed class PerformanceBudgetsTests : IClassFixture<BudgetDatabase>
{
    // Wednesday 10:07, so "the same time on earlier days" falls on earlier days at 10:07 as well.
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 7, 30, TimeSpan.Zero);

    private readonly BudgetDatabase _database;

    public PerformanceBudgetsTests(BudgetDatabase database) => _database = database;

    private async Task ResetAsync() => await _database.ExecuteAsync(
        "DELETE FROM observability.perf_minute_summaries; DELETE FROM observability.perf_budgets; DELETE FROM observability.perf_releases; DELETE FROM observability.error_occurrences; DELETE FROM observability.error_events;");

    private async Task SeedAsync(string name, DateTimeOffset minute, int count, double milliseconds, string metric = PerformanceMetric.Request)
    {
        await using var recorder = new PerformanceRecorder(_database.DataSource, TimeSpan.FromHours(1), clock: () => minute);
        for (var i = 0; i < count; i++)
            recorder.Record(metric, name, milliseconds);
        await recorder.FlushAsync(includeCurrentMinute: true);
    }

    private async Task SeedRangeAsync(string name, DateTimeOffset from, int minutes, int perMinute, double milliseconds)
    {
        for (var m = 0; m < minutes; m++)
            await SeedAsync(name, from.AddMinutes(m), perMinute, milliseconds);
    }

    private async Task EvaluateAsync(DateTimeOffset? releaseSeen = null, PerformanceBudgetOptions? options = null)
    {
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));
        var evaluator = new PerformanceBudgetEvaluator(_database.DataSource, writer, options, () => Now, releaseSeen);
        await evaluator.EvaluateAsync(CancellationToken.None);
        await writer.FlushAsync();
    }

    private Task<string> StatusAsync(string category) => _database.ScalarAsync<string>(
        $"SELECT coalesce((SELECT status FROM observability.error_events WHERE category = '{category}' ORDER BY last_seen_at DESC LIMIT 1), 'None');");

    private static DateTimeOffset Minute(int minutesAgo) => new DateTimeOffset(2026, 10, 14, 10, 7, 0, TimeSpan.Zero).AddMinutes(-minutesAgo);

    [Fact]
    public async Task BudgetBreachHeldForThreeMinutesIsOneEventAndEvaluatingAgainDoesNotAddRows()
    {
        await ResetAsync();
        for (var ago = 1; ago <= 3; ago++)
            await SeedAsync("GET /slow", Minute(ago), 10, 1500);

        await EvaluateAsync();
        await EvaluateAsync();

        (await StatusAsync(PerformanceBudgetEvaluator.BudgetCategory)).Should().Be("Open");
        (await _database.ScalarAsync<long>($"SELECT count(*) FROM observability.error_events WHERE category = '{PerformanceBudgetEvaluator.BudgetCategory}';")).Should().Be(1);
    }

    [Fact]
    public async Task TwoSlowMinutesOrASlowMinuteBetweenFastOnesIsNotAnAlarm()
    {
        await ResetAsync();
        await SeedAsync("GET /two", Minute(1), 10, 1500);
        await SeedAsync("GET /two", Minute(2), 10, 1500);
        await SeedAsync("GET /gap", Minute(1), 10, 1500);
        await SeedAsync("GET /gap", Minute(2), 10, 50);
        await SeedAsync("GET /gap", Minute(3), 10, 1500);

        await EvaluateAsync();

        (await StatusAsync(PerformanceBudgetEvaluator.BudgetCategory)).Should().Be("None");
    }

    [Fact]
    public async Task OneStraySlowCallPerMinuteIsNotEnoughSamplesForAnAlarm()
    {
        await ResetAsync();
        for (var ago = 1; ago <= 3; ago++)
            await SeedAsync("GET /stray", Minute(ago), 1, 5000);

        await EvaluateAsync();

        (await StatusAsync(PerformanceBudgetEvaluator.BudgetCategory)).Should().Be("None");
    }

    [Fact]
    public async Task BreachResolvesWhenTheActionIsFastAgain()
    {
        await ResetAsync();
        for (var ago = 1; ago <= 3; ago++)
            await SeedAsync("GET /heal", Minute(ago), 10, 1500);
        await EvaluateAsync();
        await _database.ExecuteAsync("DELETE FROM observability.perf_minute_summaries;");
        for (var ago = 1; ago <= 3; ago++)
            await SeedAsync("GET /heal", Minute(ago), 10, 50);

        await EvaluateAsync();

        (await StatusAsync(PerformanceBudgetEvaluator.BudgetCategory)).Should().Be("Resolved");
    }

    [Fact]
    public async Task ExplicitBudgetOverridesTheDefaultAndCanCoverOtherMetrics()
    {
        await ResetAsync();
        await _database.ExecuteAsync(
            "INSERT INTO observability.perf_budgets (metric, name, p95_ms) VALUES ('request', 'GET /report', 5000), ('db_command', 'SELECT ?', 20);");
        for (var ago = 1; ago <= 3; ago++)
        {
            await SeedAsync("GET /report", Minute(ago), 10, 1500);
            await SeedAsync("SELECT ?", Minute(ago), 10, 100, PerformanceMetric.DbCommand);
        }

        await EvaluateAsync();

        (await _database.ScalarAsync<long>($"SELECT count(*) FROM observability.error_events WHERE category = '{PerformanceBudgetEvaluator.BudgetCategory}' AND detail LIKE '%GET /report%';")).Should().Be(0);
        (await _database.ScalarAsync<long>($"SELECT count(*) FROM observability.error_events WHERE category = '{PerformanceBudgetEvaluator.BudgetCategory}' AND detail LIKE 'db_command%';")).Should().Be(1);
    }

    private async Task SeedBaselineAsync(string name, int days, int perMinute, double milliseconds)
    {
        for (var day = 1; day <= days; day++)
            await SeedRangeAsync(name, Minute(15).AddDays(-day), 15, perMinute, milliseconds);
    }

    [Fact]
    public async Task FiftyPercentSlowerThanTheSameTimeOnEarlierDaysIsASlowdownAndARegressionAfterANewRelease()
    {
        await ResetAsync();
        await SeedBaselineAsync("GET /orders", 7, 2, 100);
        await SeedRangeAsync("GET /orders", Minute(15), 15, 3, 160);

        await EvaluateAsync();
        (await StatusAsync(PerformanceBudgetEvaluator.SlowdownCategory)).Should().Be("Open");
        (await StatusAsync(PerformanceBudgetEvaluator.RegressionCategory)).Should().Be("None");

        await _database.ExecuteAsync("DELETE FROM observability.error_events;");
        await EvaluateAsync(releaseSeen: Now.AddHours(-3));
        (await StatusAsync(PerformanceBudgetEvaluator.RegressionCategory)).Should().Be("Open");
        (await StatusAsync(PerformanceBudgetEvaluator.SlowdownCategory)).Should().Be("None");
    }

    [Fact]
    public async Task ReleaseOlderThanTheWindowIsAPlainSlowdown()
    {
        await ResetAsync();
        await SeedBaselineAsync("GET /old", 7, 2, 100);
        await SeedRangeAsync("GET /old", Minute(15), 15, 3, 160);

        await EvaluateAsync(releaseSeen: Now.AddDays(-3));

        (await StatusAsync(PerformanceBudgetEvaluator.RegressionCategory)).Should().Be("None");
        (await StatusAsync(PerformanceBudgetEvaluator.SlowdownCategory)).Should().Be("Open");
    }

    [Fact]
    public async Task ThirtyPercentSlowerOrTooFewSamplesIsNotASlowdown()
    {
        await ResetAsync();
        await SeedBaselineAsync("GET /mild", 7, 2, 100);
        await SeedRangeAsync("GET /mild", Minute(15), 15, 3, 125);
        await SeedBaselineAsync("GET /thin", 1, 2, 100);
        await SeedRangeAsync("GET /thin", Minute(15), 15, 3, 400);
        await SeedBaselineAsync("GET /quiet", 7, 2, 100);
        await SeedRangeAsync("GET /quiet", Minute(15), 5, 3, 400);

        await EvaluateAsync();

        (await StatusAsync(PerformanceBudgetEvaluator.SlowdownCategory)).Should().Be("None");
    }

    [Fact]
    public async Task SlowdownResolvesWhenSpeedIsBackToNormal()
    {
        await ResetAsync();
        await SeedBaselineAsync("GET /back", 7, 2, 100);
        await SeedRangeAsync("GET /back", Minute(15), 15, 3, 400);
        await EvaluateAsync();
        await _database.ExecuteAsync("DELETE FROM observability.perf_minute_summaries WHERE minute >= @from;", ("from", Minute(15).UtcDateTime));
        await SeedRangeAsync("GET /back", Minute(15), 15, 3, 100);

        await EvaluateAsync();

        (await StatusAsync(PerformanceBudgetEvaluator.SlowdownCategory)).Should().Be("Resolved");
    }

    [Fact]
    public async Task FirstSightingOfAVersionNeverMoves()
    {
        await ResetAsync();
        var first = await new ReleaseRegistry(_database.DataSource, () => Now).RegisterAsync("1.2.3+abc");
        var again = await new ReleaseRegistry(_database.DataSource, () => Now.AddDays(2)).RegisterAsync("1.2.3+abc");
        var other = await new ReleaseRegistry(_database.DataSource, () => Now.AddDays(2)).RegisterAsync("1.2.4+def");

        again.Should().Be(first);
        other.Should().BeAfter(first);
    }
}
