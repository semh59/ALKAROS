using ALKAROS.Audit.EventStore;
using ALKAROS.Observability.Foundation;
using ALKAROS.Support.DiagnosticBundle.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Support.DiagnosticBundle.Tests;

public sealed class DiagnosticBundleServiceTests : IAsyncLifetime
{
    private readonly DiagnosticBundleTestDatabase _database = new();
    private PostgresAuditEventStore _auditStore = null!;
    private ObservabilityService _observabilityService = null!;
    private DiagnosticBundleService _service = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _auditStore = new PostgresAuditEventStore(_database.DataSource);
        var redactionHook = new ObservabilityRedactionHook();
        var healthCheckRepository = new PostgresHealthCheckRepository(_database.DataSource, redactionHook);
        _observabilityService = new ObservabilityService(healthCheckRepository, redactionHook);
        _service = new DiagnosticBundleService(
            _observabilityService,
            redactionHook,
            new SecretPatternScanner(),
            _auditStore);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static DiagnosticBundleRequest Request(
        IReadOnlyList<string>? correlationIds = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string reason = "investigating a customer-reported order failure") =>
        new(
            RequestedByActorId: "support-agent-1",
            CorrelationIds: correlationIds ?? ["corr-1"],
            WindowStart: start ?? DateTimeOffset.UtcNow.AddHours(-1),
            WindowEnd: end ?? DateTimeOffset.UtcNow.AddHours(1),
            Reason: reason);

    private Task SeedAuditEventAsync(string correlationId, string metadataJson, DateTimeOffset? occurredAt = null) =>
        _auditStore.AppendAsync(new AuditEvent(
            id: Guid.NewGuid(),
            eventName: "order.accepted",
            aggregateType: "Order",
            aggregateId: Guid.NewGuid(),
            actorType: "Waiter",
            correlationId: correlationId,
            metadataJson: metadataJson,
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow));

    [Fact]
    public async Task SeededSecretValueNeverAppearsInTheGeneratedBundle()
    {
        const string seededCardNumber = "4111111111111111";
        await SeedAuditEventAsync("corr-1", $$"""{"note":"customer called about card {{seededCardNumber}}"}""");

        var result = await _service.GenerateAsync(Request());

        var entry = Assert.Single(result.LogEntries);
        Assert.DoesNotContain(seededCardNumber, entry.RedactedDetailsJson);
        Assert.Contains(SecretPatternScanner.Placeholder, entry.RedactedDetailsJson);
    }

    [Fact]
    public void RevertAndConfirmWithoutTheValuePatternScannerTheSeededCardNumberWouldLeak()
    {
        // Same scenario as above, but wired directly to the redaction hook
        // alone (no ISecretPatternScanner pass) - proves the scan in
        // DiagnosticBundleService.RedactEntry is load-bearing, not a no-op.
        const string seededCardNumber = "4111111111111111";
        var redactionHook = new ObservabilityRedactionHook();
        var rawJson = $$"""{"note":"customer called about card {{seededCardNumber}}"}""";

        var keyRedactedOnly = redactionHook.RedactJson(rawJson);

        Assert.Contains(seededCardNumber, keyRedactedOnly);
    }

    [Fact]
    public async Task RedactsFieldsNamedForKnownSensitiveKeys()
    {
        await SeedAuditEventAsync("corr-1", """{"password":"hunter2","note":"login retried"}""");

        var result = await _service.GenerateAsync(Request());

        var entry = Assert.Single(result.LogEntries);
        Assert.DoesNotContain("hunter2", entry.RedactedDetailsJson);
    }

    [Fact]
    public async Task OnlyIncludesEventsInsideTheRequestedWindow()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedAuditEventAsync("corr-1", "{\"note\":\"inside window\"}", now);
        await SeedAuditEventAsync("corr-1", "{\"note\":\"too old\"}", now.AddDays(-10));

        var result = await _service.GenerateAsync(Request(
            start: now.AddHours(-1),
            end: now.AddHours(1)));

        var entry = Assert.Single(result.LogEntries);
        Assert.Contains("inside window", entry.RedactedDetailsJson);
    }

    [Fact]
    public async Task CollectsEntriesAcrossMultipleSelectedCorrelationIds()
    {
        await SeedAuditEventAsync("corr-a", "{\"note\":\"first incident\"}");
        await SeedAuditEventAsync("corr-b", "{\"note\":\"second incident\"}");

        var result = await _service.GenerateAsync(Request(correlationIds: ["corr-a", "corr-b"]));

        Assert.Equal(2, result.LogEntries.Count);
    }

    [Fact]
    public async Task RejectsAWindowLargerThanTheMaximum()
    {
        var now = DateTimeOffset.UtcNow;
        var request = Request(start: now.AddDays(-40), end: now);

        var exception = await Assert.ThrowsAsync<DiagnosticBundleException>(() => _service.GenerateAsync(request));

        Assert.Equal(DiagnosticBundleFailureReason.TimeWindowTooLarge, exception.Reason);
    }

    [Fact]
    public void RejectsAnEmptyCorrelationIdSelection()
    {
        var exception = Assert.Throws<DiagnosticBundleException>(
            () => Request(correlationIds: []).Validate());

        Assert.Equal(DiagnosticBundleFailureReason.NoCorrelationIdsProvided, exception.Reason);
    }

    [Fact]
    public async Task RejectsABundleThatWouldExceedTheSizeLimit()
    {
        // Short, space-separated words (not one long token/digit run) so the
        // value-pattern scanner does not itself collapse this note down to a
        // handful of placeholder bytes before the size check runs.
        var oversizedNote = string.Join(' ', Enumerable.Repeat("lorem ipsum dolor", 12_000));
        for (var i = 0; i < 30; i++)
            await SeedAuditEventAsync("corr-1", $"{{\"note\":\"{oversizedNote}\"}}");

        var exception = await Assert.ThrowsAsync<DiagnosticBundleException>(
            () => _service.GenerateAsync(Request()));

        Assert.Equal(DiagnosticBundleFailureReason.SizeLimitExceeded, exception.Reason);
    }

    [Fact]
    public async Task RecordsItsOwnGenerationOnTheAuditTrailForProvenance()
    {
        await SeedAuditEventAsync("corr-1", "{\"note\":\"whatever\"}");

        var result = await _service.GenerateAsync(Request(reason: "PROVENANCE_TEST_REASON"));

        var provenance = await _auditStore.GetByCorrelationIdAsync("corr-1");
        var record = Assert.Single(provenance, e => e.EventName == "support.diagnostic_bundle.generated");
        Assert.Equal(result.BundleId, record.AggregateId);
        Assert.Equal("SupportUser", record.ActorType);
        Assert.Contains("PROVENANCE_TEST_REASON", record.Reason);
    }

    [Fact]
    public async Task IncludesTheCurrentUnhealthySystemStatus()
    {
        await _observabilityService.RecordHealthCheckAsync(new RecordHealthCheckRequest(
            CheckType: "database",
            Target: "primary-postgres",
            Status: HealthStatus.Unhealthy,
            RetentionPolicyId: RetentionPolicyCatalog.HotOperational7D));
        await SeedAuditEventAsync("corr-1", "{\"note\":\"whatever\"}");

        var result = await _service.GenerateAsync(Request());

        Assert.Equal(1, result.SystemStatus.UnhealthyCheckCount);
        Assert.Contains("primary-postgres", result.SystemStatus.UnhealthyTargets);
    }

    [Fact]
    public async Task GeneratesACorrectAndIndependentBundleUnderConcurrentRequests()
    {
        await SeedAuditEventAsync("corr-a", "{\"note\":\"incident marker alpha\"}");
        await SeedAuditEventAsync("corr-b", "{\"note\":\"incident marker beta\"}");

        var tasks = new[]
        {
            _service.GenerateAsync(Request(correlationIds: ["corr-a"])),
            _service.GenerateAsync(Request(correlationIds: ["corr-b"])),
            _service.GenerateAsync(Request(correlationIds: ["corr-a"])),
            _service.GenerateAsync(Request(correlationIds: ["corr-b"])),
        };

        var results = await Task.WhenAll(tasks);

        Assert.All(results.Where((_, i) => i % 2 == 0), r => Assert.Contains("marker alpha", r.LogEntries.Single().RedactedDetailsJson));
        Assert.All(results.Where((_, i) => i % 2 == 1), r => Assert.Contains("marker beta", r.LogEntries.Single().RedactedDetailsJson));
        Assert.Equal(4, results.Select(r => r.BundleId).Distinct().Count());
    }
}
