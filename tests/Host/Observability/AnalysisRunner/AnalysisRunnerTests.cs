using ALKAROS.Host.Observability.AnalysisRunner;
using ALKAROS.Observability.ErrorAnalysis;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Observability.AnalysisRunner.Tests;

public sealed class RunnerDatabase : PgTestDatabase
{
    public RunnerDatabase() : base("alkaros_rmd564_") { }

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

public sealed class AnalysisRunnerTests : IClassFixture<RunnerDatabase>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 3, 0, TimeSpan.Zero);
    private readonly RunnerDatabase _database;

    public AnalysisRunnerTests(RunnerDatabase database) => _database = database;

    private async Task<string> SeedAsync(string name, int weeksOfHistory, int currentBurst)
    {
        var detail = $"GET {name}";
        var fingerprint = ErrorFingerprint.Compute(ErrorLayer.Api, "api.slow_request", detail);
        await _database.ExecuteAsync(
            """
            INSERT INTO observability.error_events
                (fingerprint, layer, severity, category, detail, first_seen_at, last_seen_at, occurrence_count)
            VALUES (@f, 'Api', 'Warning', 'api.slow_request', @detail, @first, @seen, 50);
            """,
            ("f", fingerprint), ("detail", detail), ("first", Now.UtcDateTime.AddDays(-70)), ("seen", Now.UtcDateTime.AddMinutes(-1)));
        for (var week = 1; week <= weeksOfHistory; week++)
        {
            await _database.ExecuteAsync(
                "INSERT INTO observability.error_occurrences (fingerprint, occurred_at, severity, occurrences) VALUES (@f, @at, 'Warning', 12);",
                ("f", fingerprint), ("at", Now.UtcDateTime.AddDays(-7 * week).AddMinutes(-3)));
        }

        await _database.ExecuteAsync(
            "INSERT INTO observability.error_occurrences (fingerprint, occurred_at, severity, occurrences) VALUES (@f, @at, 'Warning', @n);",
            ("f", fingerprint), ("at", Now.UtcDateTime.AddMinutes(-1)), ("n", currentBurst));
        return fingerprint;
    }

    private AnalysisRunnerHostedService Runner(ErrorEventWriter writer, DetectorHeartbeats? heartbeats = null) =>
        new(_database.DataSource, writer, heartbeats ?? new DetectorHeartbeats(() => Now), clock: () => Now);

    private async Task<string> SeverityOfAsync(string fingerprint) =>
        await _database.ScalarAsync<string>(
            $"SELECT severity FROM observability.error_events WHERE fingerprint = '{fingerprint}';");

    [Fact]
    public async Task AbnormallyFrequentErrorIsReReportedOneLevelHigher()
    {
        var fp = await SeedAsync("esc", weeksOfHistory: 5, currentBurst: 30);
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5), clock: () => Now);

        var escalated = await Runner(writer).RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();

        escalated.Should().BeGreaterThan(0);
        (await SeverityOfAsync(fp)).Should().Be("Error");
    }

    [Fact]
    public async Task ErrorWithoutEnoughHistoryIsNotEscalated()
    {
        var fp = await SeedAsync("thin", weeksOfHistory: 1, currentBurst: 30);
        await _database.ExecuteAsync(
            "UPDATE observability.error_events SET first_seen_at = @t WHERE fingerprint = @f;", ("t", Now.UtcDateTime.AddDays(-3)), ("f", fp));
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5), clock: () => Now);

        await Runner(writer).RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();

        (await SeverityOfAsync(fp)).Should().Be("Warning");
    }

    [Fact]
    public async Task SilentRegisteredDetectorBecomesADegradedEvent()
    {
        var clock = Now;
        var heartbeats = new DetectorHeartbeats(() => clock);
        heartbeats.Register("other-detector", TimeSpan.FromSeconds(30));
        clock = clock.AddMinutes(5);
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5), clock: () => clock);
        var runner = new AnalysisRunnerHostedService(_database.DataSource, writer, heartbeats, clock: () => clock);

        await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();

        (await _database.ScalarAsync<long>(
            "SELECT count(*) FROM observability.error_events WHERE category = 'monitoring.detector_degraded';")).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FailingRoundIsCountedAndTheLoopKeepsRunning()
    {
        await using var broken = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=2;Pooling=false");
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));
        using var runner = new AnalysisRunnerHostedService(
            broken, writer, new DetectorHeartbeats(), TimeSpan.FromMilliseconds(30));

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
}
