using System.Collections.Concurrent;
using ALKAROS.Idempotency.Tests.Fixtures;
using ALKAROS.Messaging;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

public sealed class InboxStoreTests : IClassFixture<StoreTestDatabase>
{
    private readonly StoreTestDatabase _database;

    public InboxStoreTests(StoreTestDatabase database)
    {
        _database = database;
    }

    private static InboxEnvelope Envelope(string source, string eventId)
        => new(source, eventId, [1, 2, 3]);

    [Fact]
    public async Task TryEnqueueNewMessageReturnsTrue()
    {
        var store = new InboxStore(_database.DataSource);
        var inserted = await store.TryEnqueueAsync(Envelope("qnb", Guid.NewGuid().ToString("N")));
        Assert.True(inserted);
    }

    [Fact]
    public async Task TryEnqueueDuplicateSourceAndEventReturnsFalse()
    {
        var store = new InboxStore(_database.DataSource);
        var envelope = Envelope("qnb", Guid.NewGuid().ToString("N"));
        Assert.True(await store.TryEnqueueAsync(envelope));
        Assert.False(await store.TryEnqueueAsync(envelope));
    }

    [Fact]
    public async Task TryEnqueueSameSourceDifferentEventIsAllowed()
    {
        var store = new InboxStore(_database.DataSource);
        var source = $"qnb-{Guid.NewGuid():N}";
        Assert.True(await store.TryEnqueueAsync(Envelope(source, "evt-1")));
        Assert.True(await store.TryEnqueueAsync(Envelope(source, "evt-2")));
    }

    [Fact]
    public async Task TryEnqueueDifferentSourceSameEventIsAllowed()
    {
        var store = new InboxStore(_database.DataSource);
        var eventId = Guid.NewGuid().ToString("N");
        Assert.True(await store.TryEnqueueAsync(Envelope("qnb", eventId)));
        Assert.True(await store.TryEnqueueAsync(Envelope("hugin", eventId)));
    }

    [Fact]
    public async Task ProcessPendingHandlerSucceedsMarksProcessed()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid().ToString("N");
        await store.TryEnqueueAsync(Envelope(source, eventId));
        var handled = new ConcurrentBag<Guid>();

        var attempted = await store.ProcessPendingAsync(
            new RecordingHandler(message =>
            {
                handled.Add(message.Id);
                return true;
            }),
            batchSize: 10);

        Assert.Equal(1, attempted);
        var status = await _database.ScalarAsync<string>(
            $"SELECT status FROM inbox_messages WHERE source = '{source}' AND external_event_id = '{eventId}';");
        Assert.Equal("processed", status);
        Assert.Single(handled);
    }

    [Fact]
    public async Task ProcessPendingHandlerReturnsFalseSchedulesRetry()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid().ToString("N");
        await store.TryEnqueueAsync(Envelope(source, eventId));

        var attempted = await store.ProcessPendingAsync(
            new RecordingHandler(_ => false),
            batchSize: 10);

        Assert.Equal(1, attempted);
        var attemptCount = await _database.ScalarAsync<int>(
            $"SELECT attempt_count FROM inbox_messages WHERE source = '{source}' AND external_event_id = '{eventId}';");
        var nextRetryAt = await _database.ScalarAsync<DateTime>(
            $"SELECT next_retry_at FROM inbox_messages WHERE source = '{source}' AND external_event_id = '{eventId}';");
        Assert.Equal(1, attemptCount);
        Assert.True(nextRetryAt > DateTime.UtcNow.AddSeconds(-1));
    }

    [Fact]
    public async Task ProcessPendingHandlerThrowsCountsAsFailure()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid().ToString("N");
        await store.TryEnqueueAsync(Envelope(source, eventId));

        var attempted = await store.ProcessPendingAsync(
            new RecordingHandler((Func<InboxMessage, Task<bool>>)(_ => throw new InvalidOperationException("boom"))),
            batchSize: 10);

        Assert.Equal(1, attempted);
        var attemptCount = await _database.ScalarAsync<int>(
            $"SELECT attempt_count FROM inbox_messages WHERE source = '{source}' AND external_event_id = '{eventId}';");
        var lastError = await _database.ScalarAsync<string>(
            $"SELECT last_error FROM inbox_messages WHERE source = '{source}' AND external_event_id = '{eventId}';");
        Assert.Equal(1, attemptCount);
        Assert.Equal("handler failure", lastError);
    }

    [Fact]
    public async Task ProcessPendingThreeFailuresMovesToDeadLetter()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var messageId = await EnqueueSingleAsync(store);
        var handler = new RecordingHandler(_ => false);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await store.ProcessPendingAsync(handler, batchSize: 10);
            await _database.ForceRetryDueAsync("inbox_messages", messageId);
        }

        var status = await _database.ScalarAsync<string>(
            $"SELECT status FROM inbox_messages WHERE id = '{messageId}';");
        var attemptCount = await _database.ScalarAsync<int>(
            $"SELECT attempt_count FROM inbox_messages WHERE id = '{messageId}';");
        Assert.Equal("dead", status);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task ProcessPendingDeadLetterMessageIsNotClaimedAgain()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var messageId = await EnqueueSingleAsync(store);
        var handler = new RecordingHandler(_ => false);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await store.ProcessPendingAsync(handler, batchSize: 10);
            await _database.ForceRetryDueAsync("inbox_messages", messageId);
        }

        var attempted = await store.ProcessPendingAsync(handler, batchSize: 10);

        Assert.Equal(0, attempted);
    }

    [Fact]
    public async Task ProcessPendingRespectsBatchSize()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        for (var i = 0; i < 5; i++)
            await store.TryEnqueueAsync(Envelope(source, $"evt-{i}"));

        var first = await store.ProcessPendingAsync(
            new RecordingHandler(_ => true), batchSize: 2);
        var second = await store.ProcessPendingAsync(
            new RecordingHandler(_ => true), batchSize: 2);

        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task ProcessPendingConcurrentDispatchersClaimEachMessageOnce()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        for (var i = 0; i < 8; i++)
            await store.TryEnqueueAsync(Envelope(source, $"evt-{i}"));

        var handled = new ConcurrentBag<Guid>();
        var handler = new RecordingHandler(message =>
        {
            handled.Add(message.Id);
            return true;
        });

        var first = store.ProcessPendingAsync(handler, batchSize: 8);
        var second = store.ProcessPendingAsync(handler, batchSize: 8);
        await Task.WhenAll(first, second);

        Assert.Equal(8, handled.Count);
        Assert.Equal(8, handled.Distinct().Count());
        var processed = await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM inbox_messages WHERE source = '{source}' AND status = 'processed';");
        Assert.Equal(8, processed);
    }

    [Fact]
    public async Task ProcessExpiredLeaseIsReclaimedAndProcessed()
    {
        var store = new InboxStore(_database.DataSource, leaseTimeout: TimeSpan.FromSeconds(1));
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid().ToString("N");
        var messageId = await store.TryEnqueueAsync(Envelope(source, eventId))
            ? await _database.ScalarAsync<Guid>(
                "SELECT id FROM inbox_messages ORDER BY received_at DESC LIMIT 1;")
            : throw new InvalidOperationException("Enqueue failed.");
        await _database.ExecuteAsync(
            "UPDATE inbox_messages SET status = 'in_flight', claimed_at = now() - interval '2 seconds';");
        var handled = new ConcurrentBag<Guid>();

        var attempted = await store.ProcessPendingAsync(
            new RecordingHandler(message =>
            {
                handled.Add(message.Id);
                return true;
            }),
            batchSize: 10);

        Assert.Equal(1, attempted);
        Assert.Single(handled);
        var status = await _database.ScalarAsync<string>(
            $"SELECT status FROM inbox_messages WHERE source = '{source}' AND external_event_id = '{eventId}';");
        Assert.Equal("processed", status);
    }

    [Fact]
    public async Task ProcessHandlerRunsOutsideTheClaimTransaction()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid().ToString("N");
        await store.TryEnqueueAsync(Envelope(source, eventId));
        var observedStatus = string.Empty;

        var attempted = await store.ProcessPendingAsync(
            new RecordingHandler(async message =>
            {
                observedStatus = await _database.ScalarAsync<string>(
                    $"SELECT status FROM inbox_messages WHERE id = '{message.Id}';");
                return true;
            }),
            batchSize: 10);

        Assert.Equal(1, attempted);
        Assert.Equal("in_flight", observedStatus);
    }

    [Fact]
    public async Task ProcessLeaseLostBeforeMarkThrowsInsteadOfSkippingSilently()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid().ToString("N");
        await store.TryEnqueueAsync(Envelope(source, eventId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProcessPendingAsync(
            new RecordingHandler(async message =>
            {
                await _database.ExecuteAsync(
                    "UPDATE inbox_messages SET status = 'pending', claimed_at = NULL WHERE id = @id;",
                    ("id", message.Id));
                return true;
            }),
            batchSize: 10));
    }

    [Fact]
    public async Task ProcessStaleLeaseGenerationCannotFinalizeCurrentLease()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var source = $"qnb-{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid().ToString("N");
        await store.TryEnqueueAsync(Envelope(source, eventId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProcessPendingAsync(
            new RecordingHandler(async message =>
            {
                await _database.ExecuteAsync(
                    "UPDATE inbox_messages SET lease_generation = lease_generation + 1 WHERE id = @id;",
                    ("id", message.Id));
                return true;
            }),
            batchSize: 10));
    }

    private async Task<Guid> EnqueueSingleAsync(InboxStore store)
        => await store.TryEnqueueAsync(
            Envelope($"qnb-{Guid.NewGuid():N}", Guid.NewGuid().ToString("N")))
            ? await _database.ScalarAsync<Guid>(
                "SELECT id FROM inbox_messages ORDER BY received_at DESC LIMIT 1;")
            : throw new InvalidOperationException("Enqueue failed.");

    private sealed class RecordingHandler : IInboxHandler
    {
        private readonly Func<InboxMessage, CancellationToken, Task<bool>> _handler;

        public RecordingHandler(Func<InboxMessage, bool> handler)
        {
            _handler = (message, _) => Task.FromResult(handler(message));
        }

        public RecordingHandler(Func<InboxMessage, Task<bool>> handler)
        {
            _handler = (message, _) => handler(message);
        }

        public Task<bool> HandleAsync(InboxMessage message, CancellationToken cancellationToken)
            => _handler(message, cancellationToken);
    }
}
