using System.Collections.Concurrent;
using ALKAROS.Idempotency.Tests.Fixtures;
using ALKAROS.Messaging;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

public sealed class OutboxStoreTests : IClassFixture<StoreTestDatabase>
{
    private readonly StoreTestDatabase _database;

    public OutboxStoreTests(StoreTestDatabase database)
    {
        _database = database;
    }

    private static OutboxEnvelope Envelope(string eventType)
        => new(eventType, "Order", Guid.NewGuid(), [1, 2, 3]);

    [Fact]
    public async Task EnqueuePersistsPendingMessage()
    {
        var store = new OutboxStore(_database.DataSource);
        var envelope = Envelope(Guid.NewGuid().ToString("N"));

        var message = await store.EnqueueAsync(envelope);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(envelope.EventType, message.EventType);
        Assert.Equal(envelope.AggregateType, message.AggregateType);
        Assert.Equal(envelope.AggregateId, message.AggregateId);
        Assert.Equal(envelope.PayloadEnvelope, message.PayloadEnvelope);
        Assert.Equal(0, message.AttemptCount);
        Assert.Null(message.DispatchedAt);
        Assert.Null(message.LastError);
    }

    [Fact]
    public async Task DispatchHandlerSucceedsMarksDispatched()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(Envelope(eventType));
        var delivered = new ConcurrentBag<Guid>();

        var attempted = await store.DispatchAsync(
            new RecordingSink(message =>
            {
                delivered.Add(message.Id);
                return true;
            }),
            batchSize: 10);

        Assert.Equal(1, attempted);
        Assert.Single(delivered);
        var status = await _database.ScalarAsync<string>(
            $"SELECT status FROM outbox_messages WHERE event_type = '{eventType}';");
        var dispatchedAt = await _database.ScalarAsync<DateTime>(
            $"SELECT dispatched_at FROM outbox_messages WHERE event_type = '{eventType}';");
        Assert.Equal("dispatched", status);
        Assert.True(dispatchedAt > DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task DispatchHandlerReturnsFalseSchedulesRetry()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(Envelope(eventType));

        var attempted = await store.DispatchAsync(
            new RecordingSink(_ => false),
            batchSize: 10);

        Assert.Equal(1, attempted);
        var attemptCount = await _database.ScalarAsync<int>(
            $"SELECT attempt_count FROM outbox_messages WHERE event_type = '{eventType}';");
        var status = await _database.ScalarAsync<string>(
            $"SELECT status FROM outbox_messages WHERE event_type = '{eventType}';");
        Assert.Equal(1, attemptCount);
        Assert.Equal("pending", status);
    }

    [Fact]
    public async Task DispatchHandlerThrowsCountsAsFailure()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(Envelope(eventType));

        var attempted = await store.DispatchAsync(
            new RecordingSink((Func<OutboxMessage, Task<bool>>)(_ => throw new InvalidOperationException("boom"))),
            batchSize: 10);

        Assert.Equal(1, attempted);
        var lastError = await _database.ScalarAsync<string>(
            $"SELECT last_error FROM outbox_messages WHERE event_type = '{eventType}';");
        Assert.Equal("boom", lastError);
    }

    [Fact]
    public async Task DispatchThreeFailuresMovesToDeadLetter()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(Envelope(eventType));
        var sink = new RecordingSink(_ => false);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await store.DispatchAsync(sink, batchSize: 10);
            await _database.ForceRetryDueAsync("outbox_messages", await GetMessageIdAsync(eventType));
        }

        var status = await _database.ScalarAsync<string>(
            $"SELECT status FROM outbox_messages WHERE event_type = '{eventType}';");
        var attemptCount = await _database.ScalarAsync<int>(
            $"SELECT attempt_count FROM outbox_messages WHERE event_type = '{eventType}';");
        Assert.Equal("dead", status);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task DispatchDeadLetterMessageIsNotClaimedAgain()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(Envelope(eventType));
        var sink = new RecordingSink(_ => false);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await store.DispatchAsync(sink, batchSize: 10);
            await _database.ForceRetryDueAsync("outbox_messages", await GetMessageIdAsync(eventType));
        }

        var attempted = await store.DispatchAsync(sink, batchSize: 10);

        Assert.Equal(0, attempted);
    }

    [Fact]
    public async Task DispatchRespectsBatchSize()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var prefix = $"oc-{Guid.NewGuid():N}";
        for (var i = 0; i < 5; i++)
            await store.EnqueueAsync(Envelope($"{prefix}-{i}"));

        var first = await store.DispatchAsync(new RecordingSink(_ => true), batchSize: 2);
        var second = await store.DispatchAsync(new RecordingSink(_ => true), batchSize: 2);

        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task DispatchConcurrentDispatchersDeliverEachMessageOnce()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var prefix = $"oc-{Guid.NewGuid():N}";
        for (var i = 0; i < 8; i++)
            await store.EnqueueAsync(Envelope($"{prefix}-{i}"));

        var delivered = new ConcurrentBag<Guid>();
        var sink = new RecordingSink(message =>
        {
            delivered.Add(message.Id);
            return true;
        });

        var first = store.DispatchAsync(sink, batchSize: 8);
        var second = store.DispatchAsync(sink, batchSize: 8);
        await Task.WhenAll(first, second);

        Assert.Equal(8, delivered.Count);
        Assert.Equal(8, delivered.Distinct().Count());
        var dispatched = await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM outbox_messages WHERE event_type LIKE '{prefix}%' AND status = 'dispatched';");
        Assert.Equal(8, dispatched);
    }

    [Fact]
    public async Task DispatchExpiredLeaseIsReclaimedAndDelivered()
    {
        var store = new OutboxStore(_database.DataSource, leaseTimeout: TimeSpan.FromSeconds(1));
        await _database.ResetTablesAsync();
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(Envelope(eventType));
        await _database.ExecuteAsync(
            "UPDATE outbox_messages SET status = 'in_flight', claimed_at = now() - interval '2 seconds';");
        var delivered = new ConcurrentBag<Guid>();

        var attempted = await store.DispatchAsync(
            new RecordingSink(message =>
            {
                delivered.Add(message.Id);
                return true;
            }),
            batchSize: 10);

        Assert.Equal(1, attempted);
        Assert.Single(delivered);
        var status = await _database.ScalarAsync<string>(
            $"SELECT status FROM outbox_messages WHERE event_type = '{eventType}';");
        Assert.Equal("dispatched", status);
    }

    [Fact]
    public async Task DispatchHandlerRunsOutsideTheClaimTransaction()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(Envelope(eventType));
        var observedStatus = string.Empty;

        var attempted = await store.DispatchAsync(
            new RecordingSink(async message =>
            {
                observedStatus = await _database.ScalarAsync<string>(
                    $"SELECT status FROM outbox_messages WHERE id = '{message.Id}';");
                return true;
            }),
            batchSize: 10);

        Assert.Equal(1, attempted);
        Assert.Equal("in_flight", observedStatus);
    }

    [Fact]
    public async Task DispatchLeaseLostBeforeMarkThrowsInsteadOfSkippingSilently()
    {
        var store = new OutboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(Envelope(eventType));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.DispatchAsync(
            new RecordingSink(async message =>
            {
                await _database.ExecuteAsync(
                    "UPDATE outbox_messages SET status = 'pending', claimed_at = NULL WHERE id = @id;",
                    ("id", message.Id));
                return true;
            }),
            batchSize: 10));
    }

    private async Task<Guid> GetMessageIdAsync(string eventType)
        => await _database.ScalarAsync<Guid>(
            $"SELECT id FROM outbox_messages WHERE event_type = '{eventType}';");

    private sealed class RecordingSink : IOutboxDeliverySink
    {
        private readonly Func<OutboxMessage, CancellationToken, Task<bool>> _handler;

        public RecordingSink(Func<OutboxMessage, bool> handler)
        {
            _handler = (message, _) => Task.FromResult(handler(message));
        }

        public RecordingSink(Func<OutboxMessage, Task<bool>> handler)
        {
            _handler = (message, _) => handler(message);
        }

        public Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
            => _handler(message, cancellationToken);
    }
}
