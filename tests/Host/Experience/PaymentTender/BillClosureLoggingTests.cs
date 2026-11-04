using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

public sealed class BillClosureLoggingTests
{
    [Fact]
    public async Task AFailedClosureIsLoggedAndTheAnswerIsNotClosedButTheDisplayStillHearsOfThePayment()
    {
        var logs = new CapturingLoggerFactory();
        var hub = new RecordingHub();

        var closed = await DualScreenApplication.TryCloseBillAsync(
            new ThrowingClosure(), null!, Guid.NewGuid(), hub, logs, Guid.NewGuid(), CancellationToken.None);

        Assert.False(closed);
        Assert.Equal(["BillCloseFailed"], logs.Events);
        Assert.Contains("PaymentChanged", Assert.Single(hub.Pushes).Argument);
    }

    [Fact]
    public async Task AClosedBillWhoseOrderCannotBeCompletedStaysClosedAndIsLogged()
    {
        var logs = new CapturingLoggerFactory();
        var hub = new RecordingHub();

        // The order hand-over fails here (null settlement service); the bill itself closed.
        var closed = await DualScreenApplication.TryCloseBillAsync(
            new FixedClosure(BillClosureOutcome.Closed), null!, Guid.NewGuid(), hub, logs, Guid.NewGuid(), CancellationToken.None);

        Assert.True(closed);
        Assert.Equal(["BillClosedOrderNotCompleted"], logs.Events);
        Assert.Contains("BillSettled", Assert.Single(hub.Pushes).Argument);
    }

    [Fact]
    public async Task ABillThatIsNotSettledYetStillTellsTheDisplayWhatChanged()
    {
        var logs = new CapturingLoggerFactory();
        var hub = new RecordingHub();
        var terminalId = Guid.NewGuid();

        var closed = await DualScreenApplication.TryCloseBillAsync(
            new FixedClosure(BillClosureOutcome.NotSatisfied), null!, Guid.NewGuid(), hub, logs, terminalId, CancellationToken.None);

        Assert.False(closed);
        Assert.Empty(logs.Events);
        var push = Assert.Single(hub.Pushes);
        Assert.Equal(DualScreenApplication.TerminalGroup(terminalId), push.Group);
        Assert.Equal(CustomerDisplayHub.SnapshotChanged, push.Method);
        Assert.Contains("PaymentChanged", push.Argument);
    }

    [Fact]
    public async Task AFailedDisplayPushIsLoggedAndNeverThrows()
    {
        var logs = new CapturingLoggerFactory();

        await DualScreenApplication.NotifyCustomerDisplayAsync(
            null!, logs, Guid.NewGuid(), Guid.NewGuid(), "PaymentChanged", CancellationToken.None);

        Assert.Equal(["BillClosedDisplayPushFailed"], logs.Events);
    }

    private sealed class RecordingHub : IHubContext<CustomerDisplayHub>
    {
        public List<(string Group, string Method, string Argument)> Pushes { get; } = [];

        public IHubClients Clients => new RecordingClients(Pushes);

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class RecordingClients(List<(string Group, string Method, string Argument)> pushes) : IHubClients
    {
        public IClientProxy Group(string groupName) => new RecordingProxy(groupName, pushes);

        public IClientProxy All => throw new NotSupportedException();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Client(string connectionId) => throw new NotSupportedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IClientProxy User(string userId) => throw new NotSupportedException();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingProxy(string group, List<(string Group, string Method, string Argument)> pushes) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            pushes.Add((group, method, System.Text.Json.JsonSerializer.Serialize(args[0])));
            return Task.CompletedTask;
        }
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
