using ALKAROS.Messaging.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Messaging.Tests;

public sealed class OutboxResilienceTests : IClassFixture<StoreTestDatabase>
{
    private readonly StoreTestDatabase _database;

    public OutboxResilienceTests(StoreTestDatabase database)
    {
        _database = database;
    }

    private async Task<string> EnqueueAsync(OutboxStore store)
    {
        var eventType = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(new OutboxEnvelope(eventType, "Order", Guid.NewGuid(), [1]));
        await _database.ExecuteAsync(
            "UPDATE outbox_messages SET created_at = now() + make_interval(secs => nextval('outbox_test_order')) WHERE event_type = @t;",
            ("t", eventType));
        return eventType;
    }

    private Task<string> StatusAsync(string eventType)
        => _database.ScalarAsync<string>($"SELECT status FROM outbox_messages WHERE event_type = '{eventType}';");

    private async Task ResetAsync()
    {
        await _database.ResetTablesAsync();
        await _database.ExecuteAsync("CREATE SEQUENCE IF NOT EXISTS outbox_test_order;");
    }

    [Fact]
    public async Task ASinkThatTimesOutIsAFailedAttemptAndDoesNotStallTheBatch()
    {
        await ResetAsync();
        var store = new OutboxStore(_database.DataSource, deliveryTimeout: TimeSpan.FromMilliseconds(200));
        var slow = await EnqueueAsync(store);
        var fast = await EnqueueAsync(store);

        var attempted = await store.DispatchAsync(new Sink(async (message, token) =>
        {
            if (message.EventType == slow)
                await Task.Delay(Timeout.Infinite, token);
            return true;
        }), batchSize: 10);

        Assert.Equal(2, attempted);
        Assert.Equal("pending", await StatusAsync(slow));
        Assert.Equal(1, await _database.ScalarAsync<int>($"SELECT attempt_count FROM outbox_messages WHERE event_type = '{slow}';"));
        Assert.Equal("dispatched", await StatusAsync(fast));
    }

    [Fact]
    public async Task TheStoredErrorNamesTheExceptionTypeButNeverItsText()
    {
        await ResetAsync();
        var store = new OutboxStore(_database.DataSource);
        var eventType = await EnqueueAsync(store);
        Exception? reported = null;
        store = new OutboxStore(_database.DataSource, onDeliveryFailure: (_, ex) => reported = ex);

        await store.DispatchAsync(new Sink((_, _) => throw new InvalidOperationException("password=hunter2")), batchSize: 10);

        var lastError = await _database.ScalarAsync<string>($"SELECT last_error FROM outbox_messages WHERE event_type = '{eventType}';");
        Assert.Equal("handler failure: InvalidOperationException", lastError);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public async Task AnExpiredLeaseCountsAsAnAttemptAndTheMessageIsDeliveredAgain()
    {
        await ResetAsync();
        var store = new OutboxStore(_database.DataSource);
        var eventType = await EnqueueAsync(store);
        await _database.ExecuteAsync(
            $"UPDATE outbox_messages SET status = 'in_flight', claimed_at = now() - interval '1 hour', attempt_count = 1 WHERE event_type = '{eventType}';");
        var calls = 0;

        await store.DispatchAsync(new Sink((_, _) => { calls++; return Task.FromResult(true); }), batchSize: 10);

        Assert.Equal(1, calls);
        Assert.Equal("dispatched", await StatusAsync(eventType));
        Assert.Equal(2, await _database.ScalarAsync<int>($"SELECT attempt_count FROM outbox_messages WHERE event_type = '{eventType}';"));
    }

    [Fact]
    public async Task AMessageThatKeepsLosingItsLeaseDiesInsteadOfLoopingForever()
    {
        await ResetAsync();
        var store = new OutboxStore(_database.DataSource);
        var eventType = await EnqueueAsync(store);
        await _database.ExecuteAsync(
            $"UPDATE outbox_messages SET status = 'in_flight', claimed_at = now() - interval '1 hour', attempt_count = {RetryPolicy.MaxAttempts - 1} WHERE event_type = '{eventType}';");
        var calls = 0;

        await store.DispatchAsync(new Sink((_, _) => { calls++; return Task.FromResult(true); }), batchSize: 10);

        Assert.Equal(0, calls);
        Assert.Equal("dead", await StatusAsync(eventType));
        Assert.Equal("lease expired", await _database.ScalarAsync<string>($"SELECT last_error FROM outbox_messages WHERE event_type = '{eventType}';"));
    }

    [Fact]
    public async Task ShutdownAfterADeliveryStillRecordsItAndFreesTheRestOfTheBatch()
    {
        await ResetAsync();
        var store = new OutboxStore(_database.DataSource);
        var first = await EnqueueAsync(store);
        var second = await EnqueueAsync(store);
        using var shutdown = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DispatchAsync(new Sink((message, _) =>
        {
            shutdown.Cancel();
            return Task.FromResult(true);
        }), batchSize: 10, shutdown.Token));

        Assert.Equal("dispatched", await StatusAsync(first));
        Assert.Equal("pending", await StatusAsync(second));
        Assert.Equal(0, await _database.ScalarAsync<int>($"SELECT attempt_count FROM outbox_messages WHERE event_type = '{second}';"));
    }

    [Fact]
    public async Task ALostLeaseFreesTheUntriedRestOfTheBatch()
    {
        await ResetAsync();
        var store = new OutboxStore(_database.DataSource);
        var first = await EnqueueAsync(store);
        var second = await EnqueueAsync(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.DispatchAsync(new Sink(async (message, _) =>
        {
            await _database.ExecuteAsync(
                "UPDATE outbox_messages SET status = 'pending', claimed_at = NULL WHERE id = @id;", ("id", message.Id));
            return true;
        }), batchSize: 10));

        Assert.Equal("pending", await StatusAsync(second));
    }

    [Fact]
    public async Task TheLeaseIsRenewedBeforeEachDeliveryAndALostOneIsSkipped()
    {
        await ResetAsync();
        var store = new OutboxStore(_database.DataSource);
        var first = await EnqueueAsync(store);
        var second = await EnqueueAsync(store);
        var third = await EnqueueAsync(store);
        var delivered = new List<string>();
        var secondClaimAgeSeconds = 0.0;

        await store.DispatchAsync(new Sink(async (message, _) =>
        {
            delivered.Add(message.EventType);
            if (message.EventType == first)
            {
                await _database.ExecuteAsync(
                    $"UPDATE outbox_messages SET claimed_at = now() - interval '4 minutes' WHERE event_type = '{second}';");
                await _database.ExecuteAsync(
                    $"UPDATE outbox_messages SET lease_generation = lease_generation + 1 WHERE event_type = '{third}';");
            }
            else if (message.EventType == second)
            {
                secondClaimAgeSeconds = await _database.ScalarAsync<double>(
                    $"SELECT extract(epoch FROM now() - claimed_at)::float8 FROM outbox_messages WHERE event_type = '{second}';");
            }

            return true;
        }), batchSize: 10);

        Assert.Equal([first, second], delivered);
        Assert.True(secondClaimAgeSeconds < 60, "The lease of the second message should have been renewed.");
        Assert.Equal("dispatched", await StatusAsync(second));
        Assert.Equal("in_flight", await StatusAsync(third));
    }

    [Fact]
    public async Task PurgeDeletesOldDeliveredAndOldDeadMessagesOnly()
    {
        await ResetAsync();
        var store = new OutboxStore(_database.DataSource);
        var oldDelivered = await EnqueueAsync(store);
        var newDelivered = await EnqueueAsync(store);
        var oldDead = await EnqueueAsync(store);
        var newDead = await EnqueueAsync(store);
        var oldPending = await EnqueueAsync(store);
        await _database.ExecuteAsync(
            $"UPDATE outbox_messages SET status = 'dispatched', dispatched_at = now() - interval '15 days' WHERE event_type = '{oldDelivered}';");
        await _database.ExecuteAsync(
            $"UPDATE outbox_messages SET status = 'dispatched', dispatched_at = now() - interval '13 days' WHERE event_type = '{newDelivered}';");
        await _database.ExecuteAsync(
            $"UPDATE outbox_messages SET status = 'dead', created_at = now() - interval '91 days' WHERE event_type = '{oldDead}';");
        await _database.ExecuteAsync(
            $"UPDATE outbox_messages SET status = 'dead', created_at = now() - interval '89 days' WHERE event_type = '{newDead}';");
        await _database.ExecuteAsync(
            $"UPDATE outbox_messages SET created_at = now() - interval '200 days' WHERE event_type = '{oldPending}';");

        var deleted = await store.PurgeAsync();

        Assert.Equal(2, deleted);
        Assert.Equal(3, await _database.ScalarAsync<long>("SELECT count(*) FROM outbox_messages;"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 16)]
    [InlineData(6, 30)]
    [InlineData(1000, 30)]
    public void TheWaitAfterRepeatedFaultsDoublesUpToTheCap(int faults, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), OutboxDispatchPacing.DelayAfter(faults));

    private sealed class Sink : IOutboxDeliverySink
    {
        private readonly Func<OutboxMessage, CancellationToken, Task<bool>> _handler;

        public Sink(Func<OutboxMessage, CancellationToken, Task<bool>> handler) => _handler = handler;

        public Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
            => _handler(message, cancellationToken);
    }
}
