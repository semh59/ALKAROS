using ALKAROS.Identity.Authentication;
using Xunit;

namespace ALKAROS.Identity.Authentication.Tests;

public sealed class SessionTokenIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IssueProducesOpaqueTokenAndHash()
    {
        var issued = SessionTokenIssuer.Issue(Now);

        Assert.Equal(44, issued.Token.Length);
        Assert.Equal(64, issued.TokenHash.Length);
        Assert.True(issued.TokenHash.All(Uri.IsHexDigit));
    }

    [Fact]
    public void IssueTokensAreUnique()
    {
        var first = SessionTokenIssuer.Issue(Now);
        var second = SessionTokenIssuer.Issue(Now);

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.TokenHash, second.TokenHash);
    }

    [Fact]
    public void IssueAppliesTheDefaultLifetime()
    {
        var issued = SessionTokenIssuer.Issue(Now);

        Assert.Equal(Now + SessionTokenIssuer.DefaultLifetime, issued.ExpiresAt);
    }

    [Fact]
    public void IssueAppliesCustomLifetime()
    {
        var lifetime = TimeSpan.FromHours(2);
        var issued = SessionTokenIssuer.Issue(Now, lifetime);

        Assert.Equal(Now + lifetime, issued.ExpiresAt);
    }

    [Fact]
    public void IssueRejectsNonPositiveLifetime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SessionTokenIssuer.Issue(Now, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => SessionTokenIssuer.Issue(Now, TimeSpan.FromMinutes(-1)));
    }
}
