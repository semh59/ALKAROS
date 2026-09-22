using Xunit;

namespace ALKAROS.Support.DiagnosticBundle.Tests;

public sealed class SecretPatternScannerTests
{
    private readonly SecretPatternScanner _scanner = new();

    [Theory]
    [InlineData("card number is 4111111111111111")]
    [InlineData("pan: 4111 1111 1111 1111")]
    [InlineData("pan: 4111-1111-1111-1111")]
    public void RedactsCardLikeDigitRuns(string text)
    {
        var (redacted, found) = _scanner.Scan(text);

        Assert.True(found);
        Assert.DoesNotContain("4111", redacted);
        Assert.Contains(SecretPatternScanner.Placeholder, redacted);
    }

    [Fact]
    public void RedactsJwtLikeTokens()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";

        var (redacted, found) = _scanner.Scan($"token={jwt}");

        Assert.True(found);
        Assert.DoesNotContain(jwt, redacted);
    }

    [Fact]
    public void RedactsLongBase64LikeTokens()
    {
        const string token = "QWxhZGRpbjpvcGVuIHNlc2FtZUFsYWRkaW4=";

        var (redacted, found) = _scanner.Scan($"secret value {token} end");

        Assert.True(found);
        Assert.DoesNotContain(token, redacted);
    }

    [Theory]
    [InlineData("order accepted for table 12")]
    [InlineData("{\"eventName\":\"order.accepted\",\"actor\":\"waiter-1\"}")]
    [InlineData("correlationId=abc-123-def")]
    public void LeavesOrdinaryTextUntouched(string text)
    {
        var (redacted, found) = _scanner.Scan(text);

        Assert.False(found);
        Assert.Equal(text, redacted);
    }
}
