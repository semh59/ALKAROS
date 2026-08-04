using ALKAROS.Idempotency.Tests.Fixtures;
using ALKAROS.Messaging;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

/// <summary>
/// Mandates the inbox at-least-once redelivery contract: the same message is
/// delivered to the handler more than once (retry path and expired-lease
/// redelivery path), the redelivery carries the attempt information, and an
/// idempotency-keyed handler never produces a second side effect.
/// </summary>
public sealed class InboxRedeliveryContractTests : IClassFixture<StoreTestDatabase>
{
    private readonly StoreTestDatabase _database;

    public InboxRedeliveryContractTests(StoreTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task RedeliveryAfterAckLossCarriesAttemptCountAndNoDuplicateSideEffect()
    {
        var store = new InboxStore(_database.DataSource);
        await _database.ResetTablesAsync();
        var (source, eventId) = NewKey();
        await store.TryEnqueueAsync(Envelope(source, eventId));
        var handler = new DeduplicatingHandler(failFirstDelivery: true);

        var first = await store.ProcessPendingAsync(handler, batchSize: 10);
        var messageId = handler.SingleMessageId;
        await _database.ForceRetryDueAsync("inbox_messages", messageId);
        var second = await store.ProcessPendingAsync(handler, batchSize: 10);

        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(2, handler.Deliveries.Count);
        Assert.Equal(1, handler.EffectCount);
        Assert.Equal(messageId, handler.Deliveries[0].Id);
        Assert.Equal(messageId, handler.Deliveries[1].Id);
        Assert.Equal(0, handler.Deliveries[0].AttemptCount);
        Assert.Equal(1, handler.Deliveries[1].AttemptCount);
        Assert.Equal(1, await _database.ScalarAsync<int>(
            $"SELECT attempt_count FROM inbox_messages WHERE id = '{messageId}';"));
        Assert.Equal("processed", await _database.ScalarAsync<string>(
            $"SELECT status FROM inbox_messages WHERE id = '{messageId}';"));
    }

    [Fact]
    public async Task ExpiredLeaseRedeliveryIsIdempotentForTheHandler()
    {
        var store = new InboxStore(_database.DataSource, leaseTimeout: TimeSpan.FromSeconds(1));
        await _database.ResetTablesAsync();
        var (source, eventId) = NewKey();
        await store.TryEnqueueAsync(Envelope(source, eventId));
        var handler = new DeduplicatingHandler(failFirstDelivery: true);

        var first = await store.ProcessPendingAsync(handler, batchSize: 10);
        var messageId = handler.SingleMessageId;
        await _database.ExecuteAsync(
            """
            UPDATE inbox_messages
            SET status = 'in_flight',
                claimed_at = now() - interval '2 seconds',
                next_retry_at = NULL
            WHERE id = @id;
            """,
            ("id", messageId));
        var second = await store.ProcessPendingAsync(handler, batchSize: 10);

        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(2, handler.Deliveries.Count);
        Assert.Equal(1, handler.EffectCount);
        Assert.Equal(messageId, handler.Deliveries[0].Id);
        Assert.Equal(messageId, handler.Deliveries[1].Id);
        Assert.Equal(0, handler.Deliveries[0].AttemptCount);
        Assert.Equal(1, handler.Deliveries[1].AttemptCount);
        Assert.Equal("processed", await _database.ScalarAsync<string>(
            $"SELECT status FROM inbox_messages WHERE id = '{messageId}';"));
    }

    private static (string Source, string EventId) NewKey()
        => ($"qnb-{Guid.NewGuid():N}", Guid.NewGuid().ToString("N"));

    private static InboxEnvelope Envelope(string source, string eventId)
        => new(source, eventId, [1, 2, 3]);

    /// <summary>
    /// Applies the side effect exactly once per idempotency key; every
    /// delivery is recorded with the observed attempt information. The first
    /// delivery fails (returning false) to simulate an ack lost after the
    /// effect, forcing a redelivery.
    /// </summary>
    private sealed class DeduplicatingHandler : IInboxHandler
    {
        private readonly HashSet<string> _handledEventIds = new(StringComparer.Ordinal);

        public DeduplicatingHandler(bool failFirstDelivery)
        {
            FailFirstDelivery = failFirstDelivery;
        }

        public List<(Guid Id, int AttemptCount, string EventId)> Deliveries { get; } = [];

        public int EffectCount { get; private set; }

        public Guid SingleMessageId => Assert.Single(Deliveries).Id;

        private bool FailFirstDelivery { get; }

        public Task<bool> HandleAsync(InboxMessage message, CancellationToken cancellationToken)
        {
            Deliveries.Add((message.Id, message.AttemptCount, message.ExternalEventId));
            if (!_handledEventIds.Add(message.ExternalEventId))
                return Task.FromResult(true);

            EffectCount++;
            return Task.FromResult(!FailFirstDelivery);
        }
    }
}
