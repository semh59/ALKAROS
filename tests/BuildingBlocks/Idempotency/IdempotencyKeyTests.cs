using ALKAROS.Idempotency;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

public sealed class IdempotencyKeyTests
{
    [Fact]
    public void ConstructorEmptyClientIdThrows()
    {
        Assert.Throws<ArgumentException>(() => new IdempotencyKey("", "op-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ConstructorWhitespaceClientIdThrows(string? clientId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new IdempotencyKey(clientId!, "op-1"));
    }

    [Fact]
    public void ConstructorEmptyOperationIdThrows()
    {
        Assert.Throws<ArgumentException>(() => new IdempotencyKey("client-1", ""));
    }

    [Fact]
    public void ConstructorOverMaxLengthThrows()
    {
        Assert.Throws<ArgumentException>(
            () => new IdempotencyKey(new string('a', 101), "op-1"));
        Assert.Throws<ArgumentException>(
            () => new IdempotencyKey("client-1", new string('b', 101)));
    }

    [Fact]
    public void ConstructorMaxLengthValuesAreAccepted()
    {
        var key = new IdempotencyKey(new string('a', 100), new string('b', 100));
        Assert.Equal(new string('a', 100), key.ClientId);
        Assert.Equal(new string('b', 100), key.OperationId);
    }

    [Fact]
    public void RecordEqualityIsStructural()
    {
        var first = new IdempotencyKey("client", "op");
        var second = new IdempotencyKey("client", "op");
        Assert.Equal(first, second);
    }
}
