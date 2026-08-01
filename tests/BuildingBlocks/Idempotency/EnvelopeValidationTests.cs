using ALKAROS.Messaging;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

public sealed class EnvelopeValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void InboxEnvelopeInvalidSourceThrows(string? source)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new InboxEnvelope(source!, "event-1", [1, 2, 3]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void InboxEnvelopeInvalidExternalEventIdThrows(string? externalEventId)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new InboxEnvelope("qnb", externalEventId!, [1, 2, 3]));
    }

    [Fact]
    public void InboxEnvelopeOverMaxSourceLengthThrows()
    {
        Assert.Throws<ArgumentException>(
            () => new InboxEnvelope(new string('s', 101), "event-1", [1]));
    }

    [Fact]
    public void InboxEnvelopeOverMaxExternalEventIdLengthThrows()
    {
        Assert.Throws<ArgumentException>(
            () => new InboxEnvelope("qnb", new string('e', 201), [1]));
    }

    [Fact]
    public void InboxEnvelopeNullPayloadThrows()
    {
        Assert.Throws<ArgumentNullException>(
            () => new InboxEnvelope("qnb", "event-1", null!));
    }

    [Fact]
    public void InboxEnvelopeValidValuesAreStored()
    {
        var envelope = new InboxEnvelope("qnb", "event-1", [1, 2, 3]);
        Assert.Equal("qnb", envelope.Source);
        Assert.Equal("event-1", envelope.ExternalEventId);
        Assert.Equal([1, 2, 3], envelope.PayloadEnvelope);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void OutboxEnvelopeInvalidEventTypeThrows(string? eventType)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new OutboxEnvelope(eventType!, "Order", Guid.NewGuid(), [1]));
    }

    [Fact]
    public void OutboxEnvelopeOverMaxTypeLengthThrows()
    {
        Assert.Throws<ArgumentException>(
            () => new OutboxEnvelope(new string('e', 101), "Order", Guid.NewGuid(), [1]));
    }

    [Fact]
    public void OutboxEnvelopeNullPayloadThrows()
    {
        Assert.Throws<ArgumentNullException>(
            () => new OutboxEnvelope("OrderClosed", "Order", Guid.NewGuid(), null!));
    }

    [Fact]
    public void OutboxEnvelopeValidValuesAreStored()
    {
        var aggregateId = Guid.NewGuid();
        var envelope = new OutboxEnvelope("OrderClosed", "Order", aggregateId, [9, 8]);
        Assert.Equal("OrderClosed", envelope.EventType);
        Assert.Equal("Order", envelope.AggregateType);
        Assert.Equal(aggregateId, envelope.AggregateId);
        Assert.Equal([9, 8], envelope.PayloadEnvelope);
    }
}
