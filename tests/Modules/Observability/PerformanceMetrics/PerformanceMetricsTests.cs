using System.Diagnostics;
using ALKAROS.Observability.PerformanceMetrics;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Observability.PerformanceMetrics.Tests;

public sealed class MetricsDatabase : PgTestDatabase
{
    public MetricsDatabase() : base("alkaros_rmd543_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        var file = Directory.GetFiles(
            Path.Combine(root, "database", "migrations", "V1", "V1-RMD-543"), "189-*.up.sql").Single();
        await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}

public sealed class PerformanceMetricsTests : IClassFixture<MetricsDatabase>
{
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 8, 0, 20, TimeSpan.Zero);

    private readonly MetricsDatabase _database;
    private DateTimeOffset _now = Start;

    public PerformanceMetricsTests(MetricsDatabase database) => _database = database;

    private PerformanceRecorder NewRecorder(Func<CancellationToken, Task<long>>? queueDepth = null) =>
        new(_database.DataSource, TimeSpan.FromMinutes(30), clock: () => _now, queueDepth: queueDepth);

    private Task<int> ClearAsync() => _database.ExecuteAsync("DELETE FROM observability.perf_minute_summaries;");

    private Task<IReadOnlyList<PerformanceSummary>> ReadAsync(string metric, string? name = null) =>
        new PerformanceReader(_database.DataSource).QueryAsync(metric, _now.AddHours(-3), _now.AddHours(3), name);

    private static double Exact(double[] sorted, double fraction) => sorted[(int)Math.Ceiling(fraction * sorted.Length) - 1];

    [Theory]
    [InlineData("lognormal")]
    [InlineData("uniform")]
    [InlineData("exponential")]
    public async Task PercentilesOfTenThousandKnownSamplesAreWithinFivePercent(string shape)
    {
        await ClearAsync();
        var random = new Random(42);
        var samples = Enumerable.Range(0, 10_000).Select(_ => shape switch
        {
            "lognormal" => Math.Exp(4 + 1.2 * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble())),
            "uniform" => 5 + random.NextDouble() * 995,
            _ => -80 * Math.Log(1 - random.NextDouble()) + 2,
        }).ToArray();
        await using var recorder = NewRecorder();
        foreach (var sample in samples)
            recorder.Record(PerformanceMetric.Request, "GET /a", sample);
        await recorder.FlushAsync(includeCurrentMinute: true);
        Array.Sort(samples);

        var summary = (await ReadAsync(PerformanceMetric.Request, "GET /a")).Should().ContainSingle().Subject;

        summary.Count.Should().Be(10_000);
        foreach (var (fraction, actual) in new[] { (0.50, summary.P50), (0.95, summary.P95), (0.99, summary.P99) })
            Math.Abs(actual - Exact(samples, fraction)).Should().BeLessThan(Exact(samples, fraction) * 0.05, $"p{fraction * 100}");
    }

    [Fact]
    public async Task PercentilesComeFromSummedBucketsNotFromAveragedMinutePercentiles()
    {
        await ClearAsync();
        await using var recorder = NewRecorder();
        for (var i = 0; i < 1000; i++)
            recorder.Record(PerformanceMetric.Request, "GET /b", 10);
        _now = _now.AddMinutes(1);
        for (var i = 0; i < 20; i++)
            recorder.Record(PerformanceMetric.Request, "GET /b", 1000);
        await recorder.FlushAsync(includeCurrentMinute: true);

        var summary = (await ReadAsync(PerformanceMetric.Request, "GET /b")).Single();

        summary.Count.Should().Be(1020);
        summary.P95.Should().BeLessThan(20);
        summary.P99.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task DistinctNamesPerMinuteAreCappedAndNoSampleIsLost()
    {
        await ClearAsync();
        await using var recorder = NewRecorder();
        for (var i = 0; i < 5000; i++)
            recorder.Record(PerformanceMetric.DbCommand, $"SELECT {i} FROM t{i}", 5);
        await recorder.FlushAsync(includeCurrentMinute: true);

        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.perf_minute_summaries;")).Should().BeLessThanOrEqualTo(PerformanceRecorder.MaxRowsPerMinute);
        (await _database.ScalarAsync<long>("SELECT sum(sample_count)::bigint FROM observability.perf_minute_summaries;")).Should().Be(5000);
        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.perf_minute_summaries WHERE name = 'other';")).Should().Be(1);
    }

    [Fact]
    public async Task FlushingTheSameMinuteTwiceAddsUpInsteadOfOverwriting()
    {
        await ClearAsync();
        await using var recorder = NewRecorder();
        for (var i = 0; i < 60; i++)
            recorder.Record(PerformanceMetric.Request, "GET /c", 20);
        await recorder.FlushAsync(includeCurrentMinute: true);
        for (var i = 0; i < 40; i++)
            recorder.Record(PerformanceMetric.Request, "GET /c", 2000, failed: i < 7);
        await recorder.FlushAsync(includeCurrentMinute: true);

        var summary = (await ReadAsync(PerformanceMetric.Request, "GET /c")).Single();

        summary.Count.Should().Be(100);
        summary.Errors.Should().Be(7);
        summary.ErrorRate.Should().BeApproximately(0.07, 1e-9);
        summary.Max.Should().Be(2000);
        summary.P50.Should().BeLessThan(30);
        summary.P95.Should().BeGreaterThan(1500);
        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.perf_minute_summaries WHERE name = 'GET /c';")).Should().Be(1);
    }

    [Fact]
    public async Task CurrentMinuteIsKeptUntilItEnds()
    {
        await ClearAsync();
        await using var recorder = NewRecorder();
        recorder.Record(PerformanceMetric.Request, "GET /d", 5);

        await recorder.FlushAsync();
        (await ReadAsync(PerformanceMetric.Request, "GET /d")).Should().BeEmpty();

        _now = _now.AddMinutes(1);
        await recorder.FlushAsync();
        (await ReadAsync(PerformanceMetric.Request, "GET /d")).Should().ContainSingle();
    }

    [Fact]
    public async Task FailedWriteKeepsTheSamplesForTheNextFlush()
    {
        await ClearAsync();
        await using var recorder = NewRecorder();
        for (var i = 0; i < 25; i++)
            recorder.Record(PerformanceMetric.Request, "GET /e", 7);
        await _database.ExecuteAsync("ALTER TABLE observability.perf_minute_summaries RENAME TO perf_hidden;");
        try
        {
            await recorder.FlushAsync(includeCurrentMinute: true);
            recorder.FailedFlushes.Should().Be(1);
        }
        finally
        {
            await _database.ExecuteAsync("ALTER TABLE observability.perf_hidden RENAME TO perf_minute_summaries;");
        }

        await recorder.FlushAsync(includeCurrentMinute: true);

        (await ReadAsync(PerformanceMetric.Request, "GET /e")).Single().Count.Should().Be(25);
    }

    [Fact]
    public async Task QueueDepthIsSampledAsAMetricAndSamplerFailuresAreCounted()
    {
        await ClearAsync();
        await using var recorder = NewRecorder(_ => Task.FromResult(42L));
        await recorder.SampleQueueAsync(CancellationToken.None);
        await recorder.FlushAsync(includeCurrentMinute: true);

        var summary = (await ReadAsync(PerformanceMetric.QueueDepth, "outbox")).Single();
        summary.Count.Should().Be(1);
        summary.Max.Should().Be(42);

        await using var broken = NewRecorder(_ => throw new InvalidOperationException("no table"));
        await broken.SampleQueueAsync(CancellationToken.None);
        broken.SamplerFailures.Should().Be(1);
    }

    [Fact]
    public async Task RequestAndDatabaseActivitiesFeedTheRecorderButMonitoringStatementsDoNot()
    {
        await ClearAsync();
        _now = DateTimeOffset.UtcNow;
        await using var recorder = NewRecorder();
        using var feeder = new ActivityPerformanceFeeder(recorder);
        using var source = new ActivitySource("Microsoft.AspNetCore");
        using (var request = source.StartActivity("Microsoft.AspNetCore.Hosting.HttpRequestIn"))
        {
            request!.SetTag("http.request.method", "GET");
            request.SetTag("http.route", "/api/v1/feed");
            request.SetTag("http.response.status_code", 500);
            await using var own = _database.DataSource.CreateCommand("SELECT count(*) FROM observability.perf_minute_summaries");
            await own.ExecuteScalarAsync();
            await using var app = _database.DataSource.CreateCommand("SELECT 12345, 'secret'");
            await app.ExecuteScalarAsync();
        }

        await recorder.FlushAsync(includeCurrentMinute: true);

        var request2 = (await ReadAsync(PerformanceMetric.Request, "GET /api/v1/feed")).Single();
        request2.Errors.Should().Be(1);
        var commands = await ReadAsync(PerformanceMetric.DbCommand);
        commands.Should().ContainSingle().Which.Name.Should().Be("SELECT ?, '?'");
    }

    [Fact]
    public void EmptyHistogramHasNoPercentileAndBucketIndexIsBounded()
    {
        LogHistogram.Percentile(new long[LogHistogram.BucketCount], 0.95).Should().Be(0);
        LogHistogram.IndexOf(0).Should().Be(0);
        LogHistogram.IndexOf(double.MaxValue).Should().Be(LogHistogram.BucketCount - 1);
        LogHistogram.IndexOf(double.NaN).Should().Be(0);
    }
}
