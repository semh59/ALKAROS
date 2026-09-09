using ALKAROS.Messaging;
using Xunit;

namespace ALKAROS.Messaging.Tests;

public sealed class EnvelopeValidationTests
{
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
