using ALKAROS.Host.DualScreen;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

public sealed class DualScreenTokenTests
{
    [Fact]
    public void RawTokenMatchesOnlyItsOwnHash()
    {
        var (raw, hash) = DualScreenToken.Create("display:");
        var (otherRaw, _) = DualScreenToken.Create("display:");

        Assert.True(DualScreenToken.Matches(raw, hash));
        Assert.False(DualScreenToken.Matches(otherRaw, hash));
        Assert.False(DualScreenToken.Matches(raw, "not-a-hash"));
        Assert.DoesNotContain(raw, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyTokenMaterialIsRejected()
    {
        Assert.Throws<ArgumentException>(() => DualScreenToken.Create(" "));
        Assert.Throws<ArgumentException>(() => DualScreenToken.Hash(""));
    }
}
