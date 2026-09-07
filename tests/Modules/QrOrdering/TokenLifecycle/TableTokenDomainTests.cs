namespace ALKAROS.QrOrdering.TokenLifecycle.Tests;

using Xunit;

public sealed class TableTokenDomainTests
{
    private static TableToken NewToken(DateTimeOffset? issuedAt = null, DateTimeOffset? expiresAt = null)
    {
        var issued = issuedAt ?? DateTimeOffset.UtcNow;
        var expires = expiresAt ?? issued.AddHours(4);
        return new TableToken(Guid.NewGuid(), Guid.NewGuid(), "hash-value", issued, expires);
    }

    [Fact]
    public void ConstructorRejectsEmptyTokenId()
    {
        Assert.Throws<ArgumentException>(() =>
            new TableToken(Guid.Empty, Guid.NewGuid(), "hash", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(4)));
    }

    [Fact]
    public void ConstructorRejectsEmptyTableId()
    {
        Assert.Throws<ArgumentException>(() =>
            new TableToken(Guid.NewGuid(), Guid.Empty, "hash", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(4)));
    }

    [Fact]
    public void ConstructorRejectsEmptyHash()
    {
        Assert.Throws<ArgumentException>(() =>
            new TableToken(Guid.NewGuid(), Guid.NewGuid(), "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(4)));
    }

    [Fact]
    public void ConstructorRejectsExpiryAtOrBeforeIssuance()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new TableToken(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now));
        Assert.Throws<ArgumentException>(() => new TableToken(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now.AddSeconds(-1)));
    }

    [Fact]
    public void ConstructorRejectsRevocationFieldsSetOnlyHalfway()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            new TableToken(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now.AddHours(4), revokedAt: now, revokedReason: null));
        Assert.Throws<ArgumentException>(() =>
            new TableToken(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now.AddHours(4), revokedAt: null, revokedReason: "why"));
    }

    [Fact]
    public void AFreshTokenIsActive()
    {
        var token = NewToken();
        Assert.True(token.IsActive(DateTimeOffset.UtcNow));
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ATokenIsExpiredExactlyAtItsExpiryInstant()
    {
        var issued = DateTimeOffset.UtcNow;
        var token = NewToken(issued, issued.AddHours(4));
        Assert.False(token.IsExpired(issued.AddHours(4).AddSeconds(-1)));
        Assert.True(token.IsExpired(issued.AddHours(4)));
        Assert.False(token.IsActive(issued.AddHours(4)));
    }

    [Fact]
    public void RevokeProducesARevokedCopyWithoutMutatingTheOriginal()
    {
        var token = NewToken();
        var at = DateTimeOffset.UtcNow;
        var revoked = token.Revoke(at, "table closed");

        Assert.False(token.IsRevoked);
        Assert.True(revoked.IsRevoked);
        Assert.Equal(at, revoked.RevokedAt);
        Assert.Equal("table closed", revoked.RevokedReason);
        Assert.False(revoked.IsActive(at));
    }

    [Fact]
    public void RevokingAnAlreadyRevokedTokenThrows()
    {
        var revoked = NewToken().Revoke(DateTimeOffset.UtcNow, "first");
        Assert.Throws<InvalidOperationException>(() => revoked.Revoke(DateTimeOffset.UtcNow, "second"));
    }

    [Fact]
    public void RevokingWithAnEmptyReasonThrows()
    {
        Assert.Throws<ArgumentException>(() => NewToken().Revoke(DateTimeOffset.UtcNow, ""));
    }
}
