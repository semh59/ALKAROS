using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using Xunit;

namespace ALKAROS.Messaging.Tests;

public sealed class OutboxFanoutSinkTests
{
    private static OutboxMessage Message(string eventType, byte[] payload)
        => new(
            Guid.NewGuid(), eventType, "table_merge", Guid.NewGuid(), payload,
            OutboxStatus.InFlight, 0, 1, DateTimeOffset.UtcNow, null, null, null);

    [Fact]
    public async Task DeliversOnlyToConsumersThatHandleTheEventType()
    {
        var a = new RecordingConsumer(IntegrationEventTypes.TableMerged);
        var b = new RecordingConsumer(IntegrationEventTypes.TableTransferred);
        var sink = new OutboxFanoutSink(new IIntegrationEventConsumer[] { a, b });

        var ok = await sink.HandleAsync(Message(IntegrationEventTypes.TableMerged, [1, 2, 3]), CancellationToken.None);

        Assert.True(ok);
        var delivered = Assert.Single(a.Payloads);
        Assert.Equal(new byte[] { 1, 2, 3 }, delivered.ToArray());
        Assert.Empty(b.Payloads);
    }

    [Fact]
    public async Task AcknowledgesAMessageNoConsumerHandles()
    {
        var a = new RecordingConsumer(IntegrationEventTypes.TableMerged);
        var sink = new OutboxFanoutSink(new IIntegrationEventConsumer[] { a });

        var ok = await sink.HandleAsync(Message("some.unknown.event.v1", [9]), CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(a.Payloads);
    }

    [Fact]
    public async Task PropagatesAConsumerFailureSoTheMessageIsRetried()
    {
        var throwing = new ThrowingConsumer(IntegrationEventTypes.TableMerged);
        var sink = new OutboxFanoutSink(new IIntegrationEventConsumer[] { throwing });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sink.HandleAsync(Message(IntegrationEventTypes.TableMerged, [1]), CancellationToken.None));
    }

    [Fact]
    public void SerializerRoundTripsATableEvent()
    {
        var evt = new TableMerged(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        var bytes = IntegrationEventSerializer.Serialize(evt);
        var back = IntegrationEventSerializer.Deserialize<TableMerged>(bytes);

        Assert.Equal(evt, back);
    }

    private sealed class RecordingConsumer : IIntegrationEventConsumer
    {
        private readonly string _type;
        public List<ReadOnlyMemory<byte>> Payloads { get; } = new();
        public RecordingConsumer(string type) => _type = type;
        public bool CanHandle(string eventType) => eventType == _type;
        public Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            Payloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingConsumer : IIntegrationEventConsumer
    {
        private readonly string _type;
        public ThrowingConsumer(string type) => _type = type;
        public bool CanHandle(string eventType) => eventType == _type;
        public Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
            => throw new InvalidOperationException("consumer boom");
    }
}
