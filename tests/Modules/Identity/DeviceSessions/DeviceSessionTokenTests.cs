using ALKAROS.Identity.DeviceSessions;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.DeviceSessions.Tests;

public sealed class DeviceSessionTokenTests
{
    [Fact]
    public void CreateReturnsPrefixedBase64UrlRawToken()
    {
        var (raw, _) = DeviceSessionToken.Create();

        raw.Should().StartWith("alkaros-device-session:");
        raw.Length.Should().Be("alkaros-device-session:".Length + 43);
        raw[^43..].Should().NotContainAny("+", "/", "=");
    }

    [Fact]
    public void CreateReturns64CharacterLowercaseHexHash()
    {
        var (_, hash) = DeviceSessionToken.Create();

        hash.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void HashIsDeterministicAndNeverEqualsRawToken()
    {
        var (raw, hash) = DeviceSessionToken.Create();

        DeviceSessionToken.Hash(raw).Should().Be(hash);
        hash.Should().NotBe(raw);
        raw.Should().NotContain(hash);
    }

    [Fact]
    public void CreateGeneratesUniqueRawTokens()
    {
        var (first, _) = DeviceSessionToken.Create();
        var (second, _) = DeviceSessionToken.Create();

        first.Should().NotBe(second);
    }

    [Fact]
    public void HashThrowsOnEmptyOrNullToken()
    {
        var actEmpty = () => DeviceSessionToken.Hash("");
        var actNull = () => DeviceSessionToken.Hash(null!);

        actEmpty.Should().Throw<ArgumentException>();
        actNull.Should().Throw<ArgumentException>();
    }
}