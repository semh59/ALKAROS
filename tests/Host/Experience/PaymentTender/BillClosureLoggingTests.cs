using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Host.DualScreen;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

public sealed class BillClosureLoggingTests
{
    [Fact]
    public async Task AFailedClosureIsLoggedAndTheAnswerIsNotClosed()
    {
        var logs = new CapturingLoggerFactory();

        var closed = await DualScreenApplication.TryCloseBillAsync(
            new ThrowingClosure(), null!, Guid.NewGuid(), null!, logs, Guid.NewGuid(), CancellationToken.None);

        Assert.False(closed);
        Assert.Contains("BillCloseFailed", logs.Events);
    }

    [Fact]
    public async Task AClosedBillWhoseOrderCannotBeCompletedStaysClosedAndIsLogged()
    {
        var logs = new CapturingLoggerFactory();

        // The order hand-over and the display push both fail here (null collaborators); the bill itself closed.
        var closed = await DualScreenApplication.TryCloseBillAsync(
            new FixedClosure(BillClosureOutcome.Closed), null!, Guid.NewGuid(), null!, logs, Guid.NewGuid(), CancellationToken.None);

        Assert.True(closed);
        Assert.Contains("BillClosedOrderNotCompleted", logs.Events);
        Assert.Contains("BillClosedDisplayPushFailed", logs.Events);
    }

    [Fact]
    public async Task ABillThatIsNotSettledYetLogsNothing()
    {
        var logs = new CapturingLoggerFactory();

        var closed = await DualScreenApplication.TryCloseBillAsync(
            new FixedClosure(BillClosureOutcome.NotSatisfied), null!, Guid.NewGuid(), null!, logs, Guid.NewGuid(), CancellationToken.None);

        Assert.False(closed);
        Assert.Empty(logs.Events);
    }

    private sealed class ThrowingClosure : IBillClosureService
    {
        public Task<BillClosureResult> TryCloseAsync(Guid billId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("closure failed");
    }

    private sealed class FixedClosure(BillClosureOutcome outcome) : IBillClosureService
    {
        public Task<BillClosureResult> TryCloseAsync(Guid billId, CancellationToken cancellationToken = default)
            => Task.FromResult(new BillClosureResult(outcome, []));
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<string> Events { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Capture(Events);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class Capture(List<string> events) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => events.Add(eventId.Name ?? string.Empty);
        }
    }
}
