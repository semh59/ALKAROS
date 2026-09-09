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
