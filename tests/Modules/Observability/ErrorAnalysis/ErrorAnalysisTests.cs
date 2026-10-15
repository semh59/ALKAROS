using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Observability.ErrorAnalysis.Tests;

public sealed class AnalysisDatabase : PgTestDatabase
{
    public AnalysisDatabase() : base("alkaros_rmd536_") { }

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

public sealed class ErrorAnalysisTests : IClassFixture<AnalysisDatabase>
{
    private readonly AnalysisDatabase _database;

    public ErrorAnalysisTests(AnalysisDatabase database) => _database = database;

    [Fact]
    public void SingleEventOnANearZeroSeriesNeverEscalates()
    {
        var baseline = new ErrorBaseline(8, 0.02, 0.1);

        SeverityAnalyzer.Evaluate(baseline, 1).Escalate.Should().BeFalse();
        SeverityAnalyzer.Evaluate(baseline, 4).Escalate.Should().BeFalse();
    }

    [Fact]
    public void TenTimesTheBaselineEscalates()
    {
        var baseline = new ErrorBaseline(8, 2.0, 0.8);

        var decision = SeverityAnalyzer.Evaluate(baseline, 20);

        decision.Escalate.Should().BeTrue();
        decision.ZScore.Should().BeGreaterThan(SeverityAnalyzer.ZThreshold);
    }

    [Fact]
    public void ZeroSpreadBaselineUsesTheDeviationFloor()
    {
        var decision = SeverityAnalyzer.Evaluate(new ErrorBaseline(8, 3.5, 0.0), 5);

        decision.Escalate.Should().BeFalse();
        decision.ZScore.Should().BeApproximately(1.5, 0.001);
    }

    [Fact]
    public void ThinHistoryNeverEscalatesEvenWithALargeCount()
    {
        SeverityAnalyzer.Evaluate(new ErrorBaseline(2, 1, 0.5), 500).Escalate.Should().BeFalse();
    }

    [Fact]
    public void NormalVariationWithinTheBaselineDoesNotEscalate()
    {
        SeverityAnalyzer.Evaluate(new ErrorBaseline(8, 10, 4), 14).Escalate.Should().BeFalse();
    }

    [Theory]
    [InlineData(ErrorSeverity.Warning, ErrorSeverity.Error)]
    [InlineData(ErrorSeverity.Error, ErrorSeverity.Critical)]
    [InlineData(ErrorSeverity.Critical, ErrorSeverity.Critical)]
    public void EscalationRaisesOneLevelAndStopsAtCritical(ErrorSeverity from, ErrorSeverity expected) =>
        SeverityAnalyzer.Escalated(from).Should().Be(expected);

    [Fact]
    public void SilentDetectorBecomesADegradedEventAfterTwoIntervals()
    {
        var now = DateTimeOffset.UtcNow;
        var heartbeats = new DetectorHeartbeats(() => now);
        heartbeats.Register("outbox-watch", TimeSpan.FromSeconds(30));
        heartbeats.Register("insight", TimeSpan.FromSeconds(60));

        now = now.AddSeconds(50);
        heartbeats.Beat("insight");
        heartbeats.Degraded().Should().BeEmpty();

        now = now.AddSeconds(30);
        var degraded = heartbeats.Degraded();

        degraded.Should().ContainSingle().Which.Detail.Should().StartWith("outbox-watch");
        degraded[0].Category.Should().Be("monitoring.detector_degraded");
    }

    [Fact]
    public async Task BaselineReaderFindsSameHourHistoryAndTheCurrentBurst()
    {
        var now = new DateTimeOffset(2026, 10, 14, 12, 3, 0, TimeSpan.Zero);
        await _database.ExecuteAsync(
            """
            INSERT INTO observability.error_events (fingerprint, layer, severity, category, first_seen_at, last_seen_at, occurrence_count)
            VALUES ('burst-fp', 'Api', 'Warning', 'api.test', @first, @now, 100);
            """,
            ("first", now.UtcDateTime.AddDays(-60)), ("now", now.UtcDateTime));
        for (var week = 1; week <= 5; week++)
        {
            await _database.ExecuteAsync(
                "INSERT INTO observability.error_occurrences (fingerprint, occurred_at, severity, occurrences) VALUES ('burst-fp', @at, 'Warning', 12);",
                ("at", now.UtcDateTime.AddDays(-7 * week).AddMinutes(-3)));
        }

        await _database.ExecuteAsync(
            "INSERT INTO observability.error_occurrences (fingerprint, occurred_at, severity, occurrences) VALUES ('burst-fp', @at, 'Warning', 30);",
            ("at", now.UtcDateTime.AddMinutes(-1)));

        var reader = new ErrorBaselineReader(_database.DataSource);
        var (baseline, current) = await reader.ReadAsync("burst-fp", now);

        current.Should().Be(30);
        baseline.WeeksOfHistory.Should().BeGreaterThanOrEqualTo(5);
        baseline.MeanPerWindow.Should().BeApproximately(5 * 12 / 8.0 / 12.0, 0.01);
        SeverityAnalyzer.Evaluate(baseline, current).Escalate.Should().BeTrue();
    }
}
