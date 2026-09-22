using ALKAROS.Observability.Foundation;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Observability.StructuredLogging.Tests;

public sealed class StructuredEventLoggerTests
{
    [Fact]
    public void EmitWritesEventNameSeverityAndFieldsThroughTheLoggerPipeline()
    {
        var capture = new CapturingLogger<StructuredEventLogger>();
        var sampler = new FixedWindowEventSampler(TimeSpan.FromMinutes(1), 100);
        var sut = new StructuredEventLogger(capture, new ObservabilityRedactionHook(), sampler);

        sut.Emit("order.accepted", LogSeverity.Info, actor: "waiter-1", providerReference: "QNB-INV-42",
            payload: new Dictionary<string, object?> { ["order_id"] = "ORD-1" });

        capture.Entries.Should().ContainSingle();
        var entry = capture.Entries[0];
        entry.Level.Should().Be(LogLevel.Information);
        entry.Fields["EventName"].Should().Be("order.accepted");
        entry.Fields["Actor"].Should().Be("waiter-1");
        entry.Fields["ProviderReference"].Should().Be("QNB-INV-42");
        entry.Message.Should().Contain("order.accepted");
    }

    [Theory]
    [InlineData(LogSeverity.Info, LogLevel.Information)]
    [InlineData(LogSeverity.Warning, LogLevel.Warning)]
    [InlineData(LogSeverity.Error, LogLevel.Error)]
    [InlineData(LogSeverity.Critical, LogLevel.Critical)]
    public void EmitMapsSeverityToTheMatchingLogLevel(LogSeverity severity, LogLevel expected)
    {
        var capture = new CapturingLogger<StructuredEventLogger>();
        var sut = new StructuredEventLogger(capture, new ObservabilityRedactionHook(), new FixedWindowEventSampler(TimeSpan.FromMinutes(1), 100));

        sut.Emit("payment.captured", severity);

        capture.Entries.Should().ContainSingle().Which.Level.Should().Be(expected);
    }

    [Theory]
    [InlineData("OrderAccepted")]
    [InlineData("order accepted")]
    [InlineData("order")]
    [InlineData("")]
    [InlineData(" ")]
    public void EmitRejectsEventNamesThatDoNotFollowTheDottedLowercaseConvention(string eventName)
    {
        var sut = new StructuredEventLogger(
            new CapturingLogger<StructuredEventLogger>(),
            new ObservabilityRedactionHook(),
            new FixedWindowEventSampler(TimeSpan.FromMinutes(1), 100));

        var act = () => sut.Emit(eventName, LogSeverity.Info);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmitCorrelatesOrderKitchenAndPaymentEventsUnderOneCorrelationIdWithoutLeakingPlaintextSecrets()
    {
        var capture = new CapturingLogger<StructuredEventLogger>();
        var sut = new StructuredEventLogger(capture, new ObservabilityRedactionHook(), new FixedWindowEventSampler(TimeSpan.FromMinutes(1), 100));

        using (CorrelationContext.BeginScope(correlationId: "corr-e2e-1", initialStep: "order.accept"))
        {
            sut.Emit("order.accepted", LogSeverity.Info,
                payload: new Dictionary<string, object?> { ["order_id"] = "ORD-9", ["card_number"] = "4111111111111111" });

            CorrelationContext.AddTraceStep("kitchen.fire");
            sut.Emit("kitchen.ticket.fired", LogSeverity.Info,
                payload: new Dictionary<string, object?> { ["ticket_id"] = "TCK-9" });

            CorrelationContext.AddTraceStep("payment.capture");
            sut.Emit("payment.captured", LogSeverity.Info, providerReference: "TOKEN-TERM-7",
                payload: new Dictionary<string, object?> { ["amount"] = 350.50m, ["cvv"] = "999", ["auth_token"] = "secret-jwt" });
        }

        capture.Entries.Should().HaveCount(3);
        capture.Entries.Select(e => e.Fields["CorrelationId"]).Distinct().Should().ContainSingle().Which.Should().Be("corr-e2e-1");
        capture.Entries.Select(e => (string)e.Fields["EventName"]!).Should().Equal("order.accepted", "kitchen.ticket.fired", "payment.captured");

        var paymentPayload = (string)capture.Entries[2].Fields["Payload"]!;
        paymentPayload.Should().NotContain("999");
        paymentPayload.Should().NotContain("secret-jwt");
        paymentPayload.Should().Contain(ObservabilityRedactionHook.RedactedPlaceholder);
    }

    [Fact]
    public void EmitFallsBackToFreshIdsWhenNoCorrelationScopeIsActive()
    {
        CorrelationContext.Current.Should().BeNull();
        var capture = new CapturingLogger<StructuredEventLogger>();
        var sut = new StructuredEventLogger(capture, new ObservabilityRedactionHook(), new FixedWindowEventSampler(TimeSpan.FromMinutes(1), 100));

        sut.Emit("system.startup", LogSeverity.Info);

        var correlationId = (string)capture.Entries.Single().Fields["CorrelationId"]!;
        correlationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EmitSkipsTheSinkWhenTheLogLevelIsDisabled()
    {
        var capture = new CapturingLogger<StructuredEventLogger>(minimumLevel: LogLevel.Warning);
        var sut = new StructuredEventLogger(capture, new ObservabilityRedactionHook(), new FixedWindowEventSampler(TimeSpan.FromMinutes(1), 100));

        sut.Emit("order.accepted", LogSeverity.Info);

        capture.Entries.Should().BeEmpty();
    }
}

public sealed class FixedWindowEventSamplerTests
{
    [Fact]
    public void ShouldEmitAllowsTheFirstOccurrenceAndCapsRepeatsWithinTheWindow()
    {
        var sampler = new FixedWindowEventSampler(TimeSpan.FromSeconds(1), maxPerWindow: 2);
        var start = DateTimeOffset.UtcNow;

        sampler.ShouldEmit("high.volume", start).Should().BeTrue();
        sampler.ShouldEmit("high.volume", start.AddMilliseconds(100)).Should().BeTrue();
        sampler.ShouldEmit("high.volume", start.AddMilliseconds(200)).Should().BeFalse();
        sampler.ShouldEmit("high.volume", start.AddMilliseconds(300)).Should().BeFalse();
    }

    [Fact]
    public void ShouldEmitResetsTheCounterOnceTheWindowElapses()
    {
        var sampler = new FixedWindowEventSampler(TimeSpan.FromSeconds(1), maxPerWindow: 1);
        var start = DateTimeOffset.UtcNow;

        sampler.ShouldEmit("high.volume", start).Should().BeTrue();
        sampler.ShouldEmit("high.volume", start.AddMilliseconds(500)).Should().BeFalse();
        sampler.ShouldEmit("high.volume", start.AddSeconds(1.1)).Should().BeTrue();
    }

    [Fact]
    public void ShouldEmitTracksEachEventNameIndependently()
    {
        var sampler = new FixedWindowEventSampler(TimeSpan.FromSeconds(1), maxPerWindow: 1);
        var now = DateTimeOffset.UtcNow;

        sampler.ShouldEmit("order.accepted", now).Should().BeTrue();
        sampler.ShouldEmit("payment.captured", now).Should().BeTrue();
        sampler.ShouldEmit("order.accepted", now.AddMilliseconds(50)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    public void ConstructorRejectsANonPositiveWindow(int seconds, int maxPerWindow)
    {
        var act = () => new FixedWindowEventSampler(TimeSpan.FromSeconds(seconds), maxPerWindow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ConstructorRejectsANonPositiveLimit()
    {
        var act = () => new FixedWindowEventSampler(TimeSpan.FromSeconds(1), maxPerWindow: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

public sealed class EventNameConventionTests
{
    [Theory]
    [InlineData("order.accepted", true)]
    [InlineData("payment.captured", true)]
    [InlineData("kitchen.ticket.fired", true)]
    [InlineData("OrderAccepted", false)]
    [InlineData("order_accepted", false)]
    [InlineData("order", false)]
    [InlineData("order.", false)]
    [InlineData(".accepted", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidEnforcesTheDottedLowercaseConvention(string? eventName, bool expected)
    {
        EventNameConvention.IsValid(eventName).Should().Be(expected);
    }
}

/// <summary>
/// Minimal in-memory <see cref="ILogger{T}"/> that records the structured
/// state fields (not just the rendered message) so tests can assert on
/// individual field values, mirroring how a real structured-logging
/// provider reads <c>IReadOnlyList&lt;KeyValuePair&lt;string, object?&gt;&gt;</c> state.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly LogLevel _minimumLevel;
    private readonly List<CapturedEntry> _entries = new();

    public CapturingLogger(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    public IReadOnlyList<CapturedEntry> Entries => _entries;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var fields = new Dictionary<string, object?>();
        if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
                fields[pair.Key] = pair.Value;
        }

        _entries.Add(new CapturedEntry(logLevel, fields, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }

    internal sealed record CapturedEntry(LogLevel Level, IReadOnlyDictionary<string, object?> Fields, string Message);
}
