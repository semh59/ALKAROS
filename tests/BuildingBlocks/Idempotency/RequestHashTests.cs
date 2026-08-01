using ALKAROS.Idempotency;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

public sealed class RequestHashTests
{
    [Fact]
    public void ComputeEmptyBodyMatchesKnownSha256Vector()
    {
        var hash = RequestHash.Compute(ReadOnlyMemory<byte>.Empty);
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            hash);
    }

    [Fact]
    public void ComputeReturns64LowercaseHexCharacters()
    {
        var hash = RequestHash.Compute("request"u8.ToArray());
        Assert.Equal(64, hash.Length);
        Assert.True(hash.All(Uri.IsHexDigit));
        Assert.Equal(hash.ToLowerInvariant(), hash);
    }

    [Fact]
    public void ComputeDifferentBodiesProduceDifferentHashes()
    {
        var first = RequestHash.Compute("one"u8.ToArray());
        var second = RequestHash.Compute("two"u8.ToArray());
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeSameBodyIsDeterministic()
    {
        var body = "same payload"u8.ToArray();
        Assert.Equal(RequestHash.Compute(body), RequestHash.Compute(body));
    }
}
