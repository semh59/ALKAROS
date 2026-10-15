using ALKAROS.Observability.AlertFoundation;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Observability.ErrorEvents.Tests;

public sealed class ErrorEventsDatabase : PgTestDatabase
{
    public ErrorEventsDatabase() : base("alkaros_rmd533_") { }

    protected override async Task ApplySqlAsync()
    {
        var root = FindRepositoryRoot();
        var file = Directory.GetFiles(
            Path.Combine(root, "database", "migrations", "V1", "V1-RMD-533"), "187-*.up.sql").Single();
        await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

public sealed class RecordingAlerts : IAlertService
{
    public List<RaiseAlertRequest> Raised { get; } = [];

    public Task<AlertRaiseResult> RaiseAlertAsync(RaiseAlertRequest request, CancellationToken cancellationToken = default)
    {
        Raised.Add(request);
        return Task.FromResult<AlertRaiseResult>(null!);
    }

    public Task<AlertRecord?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<AlertRecord>> GetActiveAlertsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<AlertRecord>> GetBySourceReferenceAsync(string sourceReferenceType, Guid sourceReferenceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<AlertEventRecord>> GetEventsAsync(Guid alertId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AlertRecord> AcknowledgeAlertAsync(AcknowledgeAlertRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AlertRecord> EscalateAlertAsync(EscalateAlertRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AlertRecord> SuppressAlertAsync(SuppressAlertRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AlertRecord> ResolveAlertAsync(ResolveAlertRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

public sealed class ErrorEventWriterTests : IClassFixture<ErrorEventsDatabase>
{
    private readonly ErrorEventsDatabase _database;

    public ErrorEventWriterTests(ErrorEventsDatabase database) => _database = database;

    private ErrorEventWriter NewWriter(int capacity = 5000, int maxBatch = 500, IErrorEventChangeSink? sink = null, NpgsqlDataSource? source = null) =>
        new(source ?? _database.DataSource, capacity, maxBatch, TimeSpan.FromMinutes(5), sink);

    private async Task<(long Count, string Severity, string Status, int Occurrences)> ReadAsync(string fingerprint)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            SELECT e.occurrence_count, e.severity, e.status,
                   (SELECT count(*) FROM observability.error_occurrences o WHERE o.fingerprint = e.fingerprint)::int
            FROM observability.error_events e WHERE e.fingerprint = $1;
            """);
        command.Parameters.AddWithValue(fingerprint);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return (reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3));
    }

    [Fact]
    public void TheSameErrorWithDifferentIdsAndNumbersHasOneFingerprint()
    {
        var prints = Enumerable.Range(0, 20)
            .Select(i => ErrorFingerprint.Compute(
                ErrorLayer.Printer, "printer.offline", $"printer {i} unreachable for order {Guid.NewGuid()} after {i * 7} seconds"))
            .Distinct()
            .ToList();

        prints.Should().HaveCount(1);
        ErrorFingerprint.Compute(ErrorLayer.Printer, "printer.timeout", "x").Should()
            .NotBe(ErrorFingerprint.Compute(ErrorLayer.Printer, "printer.offline", "x"));
    }

    [Fact]
    public async Task AStormOfOneErrorIsOneUpsertWithTheFullCount()
    {
        await using var writer = NewWriter();
        for (var i = 0; i < 500; i++)
            writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Api, ErrorSeverity.Error, "storm.case", $"failed {i}"));

        await writer.FlushAsync();

        var print = ErrorFingerprint.Compute(ErrorLayer.Api, "storm.case", "failed 0");
        var row = await ReadAsync(print);
        row.Count.Should().Be(500);
        row.Occurrences.Should().Be(1);
    }

    [Fact]
    public async Task AFullQueueDropsAndCountsAndTheCounterIsPersisted()
    {
        await using var writer = NewWriter(capacity: 10);
        var accepted = 0;
        for (var i = 0; i < 25; i++)
            if (writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Api, ErrorSeverity.Warning, "drop.case")))
                accepted++;

        accepted.Should().Be(10);
        writer.DroppedPending.Should().Be(15);

        await writer.FlushAsync();

        var stored = await _database.ScalarAsync<long>(
            "SELECT value FROM observability.error_event_counters WHERE name = 'dropped_events'");
        stored.Should().Be(15);
        writer.DroppedPending.Should().Be(0);
    }

    [Fact]
    public async Task SeverityOnlyGoesUpAndARecurrenceReopensAResolvedEvent()
    {
        await using var writer = NewWriter();
        var print = ErrorFingerprint.Compute(ErrorLayer.Kitchen, "kitchen.delay", null);
        writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Kitchen, ErrorSeverity.Error, "kitchen.delay"));
        await writer.FlushAsync();
        writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Kitchen, ErrorSeverity.Warning, "kitchen.delay"));
        await writer.FlushAsync();
        (await ReadAsync(print)).Severity.Should().Be("Error");

        await _database.ExecuteAsync(
            "UPDATE observability.error_events SET status = 'Resolved', resolved_at = now() WHERE fingerprint = @f",
            ("f", print));
        writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Kitchen, ErrorSeverity.Critical, "kitchen.delay"));
        await writer.FlushAsync();

        var row = await ReadAsync(print);
        row.Severity.Should().Be("Critical");
        row.Status.Should().Be("Open");
        row.Count.Should().Be(3);
    }

    [Fact]
    public async Task APersonalValueInTheDetailIsNeverStored()
    {
        await using var writer = NewWriter();
        writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Payment, ErrorSeverity.Error, "payment.fail", "kart 4111 1111 1111 1111 reddedildi"));

        await writer.FlushAsync();

        var details = await _database.ScalarAsync<string>(
            "SELECT string_agg(coalesce(detail, ''), ',') FROM observability.error_events WHERE category = 'payment.fail'");
        details.Should().NotContain("4111");
    }

    [Fact]
    public async Task ADatabaseFailureNeverReachesTheCallerAndTheLossIsCounted()
    {
        await using var broken = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=x;Password=x;Timeout=1;Pooling=false");
        await using var writer = NewWriter(source: broken);
        for (var i = 0; i < 7; i++)
            writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Database, ErrorSeverity.Critical, "db.down"));

        var act = () => writer.FlushAsync();

        await act.Should().NotThrowAsync();
        writer.FailedPending.Should().Be(7);
    }

    [Fact]
    public async Task OneAlertIsRaisedForANewErrorAgainOnEscalationAndNeverForRepeats()
    {
        var alerts = new RecordingAlerts();
        await using var writer = NewWriter(sink: new AlertErrorEventSink(alerts));
        var input = (ErrorSeverity s) => new ErrorEventInput(ErrorLayer.Integration, s, "yemeksepeti.timeout");

        writer.TryEnqueue(input(ErrorSeverity.Error));
        await writer.FlushAsync();
        writer.TryEnqueue(input(ErrorSeverity.Error));
        await writer.FlushAsync();
        alerts.Raised.Should().HaveCount(1);

        writer.TryEnqueue(input(ErrorSeverity.Critical));
        await writer.FlushAsync();

        alerts.Raised.Should().HaveCount(2);
        alerts.Raised[1].Severity.Should().Be(AlertSeverity.Critical);
        var expected = "error:" + ErrorFingerprint.Compute(ErrorLayer.Integration, "yemeksepeti.timeout", null);
        alerts.Raised.Select(r => r.DeduplicationKey).Should().OnlyContain(k => k == expected);
    }

    [Fact]
    public async Task InfoAndWarningNeverRaiseAnAlert()
    {
        var alerts = new RecordingAlerts();
        await using var writer = NewWriter(sink: new AlertErrorEventSink(alerts));
        writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Browser, ErrorSeverity.Warning, "ui.slow"));
        writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Browser, ErrorSeverity.Info, "ui.note"));

        await writer.FlushAsync();

        alerts.Raised.Should().BeEmpty();
    }
}
