using ALKAROS.Messaging.Tests.Fixtures;
using ALKAROS.Messaging;
using Xunit;

namespace ALKAROS.Messaging.Tests;

public sealed class RetryScheduleIntegrationTests : IClassFixture<StoreTestDatabase>
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);

    private readonly StoreTestDatabase _database;

    public RetryScheduleIntegrationTests(StoreTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task OutboxSecondFailureUsesTwiceTheBaseDelay()
    {
        await _database.ResetTablesAsync();
        var store = new OutboxStore(_database.DataSource, BaseDelay);
        var eventType = $"retry-{Guid.NewGuid():N}";
        var message = await store.EnqueueAsync(new OutboxEnvelope(
            eventType,
            "Order",
            Guid.NewGuid(),
            [1]));

        var firstDue = await FailAndReadOutboxDueAsync(store);
        await _database.ForceRetryDueAsync("outbox_messages", message.Id);
        var secondDue = await FailAndReadOutboxDueAsync(store);
        await _database.ForceRetryDueAsync("outbox_messages", message.Id);
        await store.DispatchAsync(new FailingOutboxSink(), batchSize: 1);

        AssertDelay(firstDue, BaseDelay);
        AssertDelay(secondDue, BaseDelay + BaseDelay);
        Assert.True(await _database.ScalarAsync<bool>(
            "SELECT next_retry_at IS NULL FROM outbox_messages;"));
    }

    [Fact]
    public async Task AnEventTypeWithAProfileUsesItsOwnBudgetAndCap()
    {
        await _database.ResetTablesAsync();
        var eventType = $"profiled-{Guid.NewGuid():N}";
        var profile = new OutboxRetryProfile(eventType, maxAttempts: 5, baseDelay: TimeSpan.FromSeconds(20), maxDelay: TimeSpan.FromSeconds(50));
        var store = new OutboxStore(_database.DataSource, TimeSpan.FromSeconds(1), retryProfiles: [profile]);
        var message = await store.EnqueueAsync(new OutboxEnvelope(eventType, "Order", Guid.NewGuid(), [1]));

        var waits = new List<DateTime>();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            waits.Add(await FailAndReadOutboxDueAsync(store));
            await _database.ForceRetryDueAsync("outbox_messages", message.Id);
        }

        await store.DispatchAsync(new FailingOutboxSink(), batchSize: 1);

        // 20 s, 40 s, then capped at 50 s; still retried after the default budget's third failure.
        AssertDelay(waits[0], TimeSpan.FromSeconds(20));
        AssertDelay(waits[1], TimeSpan.FromSeconds(40));
        AssertDelay(waits[2], TimeSpan.FromSeconds(50));
        AssertDelay(waits[3], TimeSpan.FromSeconds(50));
        Assert.Equal("dead", await _database.ScalarAsync<string>("SELECT status FROM outbox_messages;"));
        Assert.Equal(5, await _database.ScalarAsync<int>("SELECT attempt_count FROM outbox_messages;"));
    }

    [Fact]
    public async Task AnEventTypeWithoutAProfileKeepsTheDefaultBudget()
    {
        await _database.ResetTablesAsync();
        var profiled = new OutboxRetryProfile("someone-else", maxAttempts: 12, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30));
        var store = new OutboxStore(_database.DataSource, BaseDelay, retryProfiles: [profiled]);
        var message = await store.EnqueueAsync(new OutboxEnvelope($"plain-{Guid.NewGuid():N}", "Order", Guid.NewGuid(), [1]));

        var firstDue = await FailAndReadOutboxDueAsync(store);
        for (var attempt = 2; attempt <= RetryPolicy.MaxAttempts; attempt++)
        {
            await _database.ForceRetryDueAsync("outbox_messages", message.Id);
            await store.DispatchAsync(new FailingOutboxSink(), batchSize: 1);
        }

        AssertDelay(firstDue, BaseDelay);
        Assert.Equal("dead", await _database.ScalarAsync<string>("SELECT status FROM outbox_messages;"));
    }

    [Fact]
    public void AnEventTypeCannotHaveTwoProfilesAndAProfileMustBeCoherent()
    {
        var first = new OutboxRetryProfile("twice", 3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        var second = new OutboxRetryProfile("twice", 4, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        Assert.Throws<ArgumentException>(() => new OutboxStore(_database.DataSource, retryProfiles: [first, second]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OutboxRetryProfile("x", 0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OutboxRetryProfile("x", 1, TimeSpan.Zero, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OutboxRetryProfile("x", 1, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentException>(() => new OutboxRetryProfile(" ", 1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));
    }

    private async Task<DateTime> FailAndReadOutboxDueAsync(OutboxStore store)
    {
        await store.DispatchAsync(new FailingOutboxSink(), batchSize: 1);
        return await _database.ScalarAsync<DateTime>("SELECT next_retry_at FROM outbox_messages;");
    }

    private static void AssertDelay(DateTime due, TimeSpan expectedDelay)
    {
        var remaining = due - DateTime.UtcNow;
        Assert.InRange(remaining, expectedDelay - TimeSpan.FromSeconds(3), expectedDelay + TimeSpan.FromSeconds(1));
    }

    private sealed class FailingOutboxSink : IOutboxDeliverySink
    {
        public Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }
}
