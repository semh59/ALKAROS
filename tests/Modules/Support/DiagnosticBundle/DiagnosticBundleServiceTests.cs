using System.Text.Json;
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

        Assert.All(results.Where((_, i) => i % 2 == 0), r => Assert.Contains("marker alpha", IncidentEntry(r).RedactedDetailsJson));
        Assert.All(results.Where((_, i) => i % 2 == 1), r => Assert.Contains("marker beta", IncidentEntry(r).RedactedDetailsJson));
        Assert.Equal(4, results.Select(r => r.BundleId).Distinct().Count());
    }

    // A concurrent request may already have appended its own provenance event
    // ("support.diagnostic_bundle.generated") under the same correlation id, so
    // the bundle can legitimately also contain that record; the seeded incident
    // event is the one this test is about.
    private static DiagnosticBundleLogEntry IncidentEntry(DiagnosticBundleResult result) =>
        result.LogEntries.Single(e => e.EventName != "support.diagnostic_bundle.generated");

    // The next tests target a design gap found by an independent 2026-09-22
    // audit: BeforeStateJson/AfterStateJson/MetadataJson are themselves
    // already-serialized JSON TEXT (see AuditEvent.cs / e.g.
    // BillingSplitApplication.cs writing to these fields). RedactEntry used
    // to embed them as opaque STRING leaves of the outer envelope JSON, and
    // ObservabilityRedactionHook.RedactNode only ever descends into
    // JsonObject/JsonArray nodes - a JsonValue (string) leaf is left alone,
    // so a sensitive key nested INSIDE one of those JSON-text fields (e.g.
    // {"password":"..."}) was never visited by either redaction pass.
    //
    // In production this is normally caught first by IAuditSanitizer at
    // write time (PostgresAuditEventStore.AppendAsync), which is why these
    // tests go around the real Postgres-backed IAuditEventStore with a
    // fake, unsanitized one: the point is to isolate and prove
    // DiagnosticBundleService's OWN redaction, as a second independent
    // layer, actually descends into nested JSON text rather than relying
    // solely on the audit store having already scrubbed it.
    private sealed class UnsanitizedFakeAuditEventStore : IAuditEventStore
    {
        private readonly List<AuditEvent> _events = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            _events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task AppendBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
        {
            _events.AddRange(auditEvents);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> GetByAggregateAsync(
            string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>(
                _events.Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId).ToList());

        public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(
            string correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>(
                _events.Where(e => e.CorrelationId == correlationId).ToList());
    }

    [Fact]
    public async Task RedactsASensitiveKeyNestedInsideTheBeforeStateJsonText()
    {
        const string seededPassword = "gizli-deger-123";
        var nestedBeforeStateJson = $$"""{"password":"{{seededPassword}}"}""";

        var fakeStore = new UnsanitizedFakeAuditEventStore();
        var redactionHook = new ObservabilityRedactionHook();
        var healthCheckRepository = new PostgresHealthCheckRepository(_database.DataSource, redactionHook);
        var observabilityService = new ObservabilityService(healthCheckRepository, redactionHook);
        var service = new DiagnosticBundleService(
            observabilityService,
            redactionHook,
            new SecretPatternScanner(),
            fakeStore);

        await fakeStore.AppendAsync(new AuditEvent(
            id: Guid.NewGuid(),
            eventName: "order.updated",
            aggregateType: "Order",
            aggregateId: Guid.NewGuid(),
            actorType: "Waiter",
            correlationId: "corr-1",
            beforeStateJson: nestedBeforeStateJson,
            occurredAt: DateTimeOffset.UtcNow));

        var result = await service.GenerateAsync(Request());

        var entry = Assert.Single(result.LogEntries);
        Assert.DoesNotContain(seededPassword, entry.RedactedDetailsJson);
        Assert.Contains(ObservabilityRedactionHook.RedactedPlaceholder, entry.RedactedDetailsJson);
    }

    [Fact]
    public async Task RedactsASecretPatternValueNestedInsideTheAfterStateJsonText()
    {
        // Same nested-JSON-text gap as above, but for the value-pattern
        // scanner pass rather than the key-name pass: a token-shaped value
        // under a non-sensitive key, nested inside AfterStateJson.
        const string seededToken = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
        var nestedAfterStateJson = $$"""{"note":"issued session {{seededToken}}"}""";

        var fakeStore = new UnsanitizedFakeAuditEventStore();
        var redactionHook = new ObservabilityRedactionHook();
        var healthCheckRepository = new PostgresHealthCheckRepository(_database.DataSource, redactionHook);
        var observabilityService = new ObservabilityService(healthCheckRepository, redactionHook);
        var service = new DiagnosticBundleService(
            observabilityService,
            redactionHook,
            new SecretPatternScanner(),
            fakeStore);

        await fakeStore.AppendAsync(new AuditEvent(
            id: Guid.NewGuid(),
            eventName: "order.updated",
            aggregateType: "Order",
            aggregateId: Guid.NewGuid(),
            actorType: "Waiter",
            correlationId: "corr-1",
            afterStateJson: nestedAfterStateJson,
            occurredAt: DateTimeOffset.UtcNow));

        var result = await service.GenerateAsync(Request());

        var entry = Assert.Single(result.LogEntries);
        Assert.DoesNotContain(seededToken, entry.RedactedDetailsJson);
        Assert.Contains(SecretPatternScanner.Placeholder, entry.RedactedDetailsJson);
    }

    [Fact]
    public void RevertAndConfirmEmbeddingBeforeAfterMetadataAsOpaqueStringsWouldLeakANestedPassword()
    {
        // Reproduces the pre-fix RedactEntry behavior exactly: Before/After/
        // Metadata embedded as raw opaque strings in the outer envelope,
        // with no per-field nested parse/redact step first. Proves the
        // nested-JSON-text redaction added in
        // DiagnosticBundleService.RedactNestedStateJson is load-bearing,
        // not a no-op - without it, the two existing passes alone do not
        // catch this.
        const string seededPassword = "gizli-deger-123";
        var nestedBeforeStateJson = $$"""{"password":"{{seededPassword}}"}""";

        var redactionHook = new ObservabilityRedactionHook();
        var secretScanner = new SecretPatternScanner();

        var preFixOuterJson = JsonSerializer.Serialize(new
        {
            AggregateType = "Order",
            AggregateId = Guid.NewGuid(),
            ActorType = "Waiter",
            Reason = (string?)null,
            Before = nestedBeforeStateJson,
            After = (string?)null,
            Metadata = (string?)null,
        });

        // Pass 1 (key-based) then pass 2 (value-pattern) - the same two
        // passes RedactEntry has always applied to the outer envelope.
        var keyRedacted = redactionHook.RedactJson(preFixOuterJson);
        var (valueRedacted, _) = secretScanner.Scan(keyRedacted);

        Assert.Contains(seededPassword, valueRedacted);
    }
}
