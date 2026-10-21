using ALKAROS.Messaging.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Messaging.Tests;

public sealed class OutboxOrderingTests : IClassFixture<StoreTestDatabase>
{
    private readonly StoreTestDatabase _database;

    public OutboxOrderingTests(StoreTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void AnEventWithoutAnAggregateIdCannotBeQueued()
        => Assert.Throws<ArgumentException>(() => new OutboxEnvelope("t", "Order", Guid.Empty, [1]));

    [Fact]
    public async Task MessagesAreDeliveredOldestFirst()
    {
        await _database.ResetTablesAsync();
        var store = new OutboxStore(_database.DataSource);
        var types = Enumerable.Range(0, 5).Select(i => $"e{i}-{Guid.NewGuid():N}").ToList();
        // Queued newest first, so heap order is the reverse of the age order the claim must restore.
        for (var i = types.Count - 1; i >= 0; i--)
        {
            await store.EnqueueAsync(new OutboxEnvelope(types[i], "Order", Guid.NewGuid(), [1]));
            await _database.ExecuteAsync(
                "UPDATE outbox_messages SET created_at = now() - make_interval(secs => @age) WHERE event_type = @t;",
                ("age", (double)(types.Count - i)), ("t", types[i]));
        }

        var delivered = new List<string>();
        await store.DispatchAsync(new Sink(message => { delivered.Add(message.EventType); return true; }), batchSize: 10);

        Assert.Equal(types, delivered);
    }

    [Fact]
    public async Task AMessageWhoseRetryTimeHasNotComeIsNotClaimed()
    {
        await _database.ResetTablesAsync();
        var store = new OutboxStore(_database.DataSource);
        var due = Guid.NewGuid().ToString("N");
        var later = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(new OutboxEnvelope(due, "Order", Guid.NewGuid(), [1]));
        await store.EnqueueAsync(new OutboxEnvelope(later, "Order", Guid.NewGuid(), [1]));
        await _database.ExecuteAsync(
            $"UPDATE outbox_messages SET next_retry_at = now() + interval '1 hour' WHERE event_type = '{later}';");

        var delivered = new List<string>();
        await store.DispatchAsync(new Sink(message => { delivered.Add(message.EventType); return true; }), batchSize: 10);

        Assert.Equal([due], delivered);
    }

    [Fact]
    public async Task AFailedMessageWaitsTheDoublingBackoffBeforeItsNextAttempt()
    {
        await _database.ResetTablesAsync();
        var store = new OutboxStore(_database.DataSource, baseDelay: TimeSpan.FromSeconds(100));
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(new OutboxEnvelope(eventType, "Order", Guid.NewGuid(), [1]));
        var waits = new List<double>();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await store.DispatchAsync(new Sink(_ => false), batchSize: 10);
            waits.Add(await _database.ScalarAsync<double>(
                $"SELECT extract(epoch FROM next_retry_at - now())::float8 FROM outbox_messages WHERE event_type = '{eventType}';"));
            await _database.ExecuteAsync(
                $"UPDATE outbox_messages SET next_retry_at = now() - interval '1 second' WHERE event_type = '{eventType}';");
        }

        Assert.InRange(waits[0], 95, 100);
        Assert.InRange(waits[1], 195, 200);
    }

    private sealed class Sink : IOutboxDeliverySink
    {
        private readonly Func<OutboxMessage, bool> _handler;

        public Sink(Func<OutboxMessage, bool> handler) => _handler = handler;

        public Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
            => Task.FromResult(_handler(message));
    }
}
