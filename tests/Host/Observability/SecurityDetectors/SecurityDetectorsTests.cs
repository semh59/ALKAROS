using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Identity.Authorization;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Host.Observability.SecurityDetectors.Tests;

public sealed class SecurityDatabase : PgTestDatabase
{
    public SecurityDatabase() : base("alkaros_rmd554_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        foreach (var (folder, pattern) in new[] { ("V1-RMD-533", "187-*.up.sql"), ("V1-FND-002", "003-*.up.sql") })
        {
            var file = Directory.GetFiles(Path.Combine(root, "database", "migrations", "V1", folder), pattern).Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public sealed class SecurityDetectorsTests : IClassFixture<SecurityDatabase>
{
    private readonly SecurityDatabase _database;

    public SecurityDetectorsTests(SecurityDatabase database) => _database = database;

    private ErrorEventWriter NewWriter() => new(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));

    private Task<long> CountAsync(string category) =>
        _database.ScalarAsync<long>($"SELECT count(*) FROM observability.error_events WHERE category = '{category}';");

    [Fact]
    public async Task TenFailedLoginsInAMinuteProduceOneEventWithTerminalLabelOnly()
    {
        await using var writer = NewWriter();
        var detectors = new SecurityBurstDetectors(writer);
        var label = SecurityBurstDetectors.TerminalLabel(Guid.NewGuid());

        var fired = Enumerable.Range(0, 10).Select(_ => detectors.LoginFailures.Record(label)).ToList();
        await writer.FlushAsync();

        fired.Count(f => f).Should().Be(1);
        (await CountAsync("security.login_bruteforce")).Should().Be(1);
        (await _database.ScalarAsync<string>(
            "SELECT last_terminal_label FROM observability.error_events WHERE category = 'security.login_bruteforce';"))
            .Should().Be(label);
    }

    [Fact]
    public async Task NineFailuresOrSlowFailuresProduceNoEvent()
    {
        await using var writer = NewWriter();
        var now = DateTimeOffset.UtcNow;
        var detector = new BurstDetector(
            writer, ErrorLayer.Security, ErrorSeverity.Error, "security.test_slow", 10, TimeSpan.FromSeconds(60), () => now);

        for (var i = 0; i < 15; i++)
        {
            detector.Record().Should().BeFalse();
            now = now.AddSeconds(10);
        }

        await writer.FlushAsync();
        (await CountAsync("security.test_slow")).Should().Be(0);
    }

    private sealed class CountingSink : IDenialEventSink
    {
        public int Recorded { get; private set; }

        public Task RecordAsync(DenialEvent denialEvent, CancellationToken cancellationToken = default)
        {
            Recorded++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DenialBurstIsReportedAndEveryDenialStillReachesTheDurableSink()
    {
        await using var writer = NewWriter();
        var inner = new CountingSink();
        var sink = new ObservingDenialEventSink(inner, new SecurityBurstDetectors(writer));

        for (var i = 0; i < 20; i++)
            await sink.RecordAsync(new DenialEvent(null, "orders.void", "denied", DateTimeOffset.UtcNow));
        await writer.FlushAsync();

        inner.Recorded.Should().Be(20);
        (await CountAsync("security.denial_burst")).Should().Be(1);
    }

    [Fact]
    public async Task NewDeadLetterProducesOneEventAndUnchangedCountProducesNone()
    {
        await using var writer = NewWriter();
        var watcher = new OutboxDeadLetterWatcher(new MonitoringDataSource(_database.DataSource), writer);

        (await watcher.CheckOnceAsync(CancellationToken.None)).Should().BeFalse();
        await _database.ExecuteAsync(
            "INSERT INTO outbox_messages (event_type, aggregate_type, aggregate_id, payload_envelope, status) " +
            "VALUES ('x', 'y', gen_random_uuid(), decode('00', 'hex'), 'dead');");

        (await watcher.CheckOnceAsync(CancellationToken.None)).Should().BeTrue();
        (await watcher.CheckOnceAsync(CancellationToken.None)).Should().BeFalse();
        await writer.FlushAsync();
        (await CountAsync("outbox.dead_letter")).Should().Be(1);
    }
}
