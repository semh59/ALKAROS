namespace ALKAROS.QrOrdering.CustomerSession.Tests;

using Xunit;

public sealed class CustomerSessionDomainTests
{
    private static CustomerSession NewSession(
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? lastActivityAt = null,
        DateTimeOffset? expiresAt = null)
    {
        var issued = issuedAt ?? DateTimeOffset.UtcNow;
        var lastActivity = lastActivityAt ?? issued;
        var expires = expiresAt ?? issued.AddHours(4);
        return new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), "hash-value", issued, lastActivity, expires);
    }

    [Fact]
    public void ConstructorRejectsEmptySessionId()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            new CustomerSession(Guid.Empty, Guid.NewGuid(), "hash", now, now, now.AddHours(4)));
    }

    [Fact]
    public void ConstructorRejectsEmptyTableId()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            new CustomerSession(Guid.NewGuid(), Guid.Empty, "hash", now, now, now.AddHours(4)));
    }

    [Fact]
    public void ConstructorRejectsEmptyHash()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), "", now, now, now.AddHours(4)));
    }

    [Fact]
    public void ConstructorRejectsExpiryAtOrBeforeIssuance()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now, now));
        Assert.Throws<ArgumentException>(() =>
            new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now, now.AddSeconds(-1)));
    }

    [Fact]
    public void ConstructorRejectsActivityBeforeIssuance()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now.AddSeconds(-1), now.AddHours(4)));
    }

    [Fact]
    public void ConstructorRejectsRevocationFieldsSetOnlyHalfway()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now, now.AddHours(4), revokedAt: now, revokedReason: null));
        Assert.Throws<ArgumentException>(() =>
            new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), "hash", now, now, now.AddHours(4), revokedAt: null, revokedReason: "why"));
    }

    [Fact]
    public void AFreshSessionIsActive()
    {
        var session = NewSession();
        Assert.True(session.IsActive(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30)));
        Assert.False(session.IsRevoked);
        Assert.False(session.IsAbsolutelyExpired(DateTimeOffset.UtcNow));
        Assert.False(session.IsIdleExpired(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void ASessionIsAbsolutelyExpiredExactlyAtItsExpiryInstant()
    {
        var issued = DateTimeOffset.UtcNow;
        var session = NewSession(issued, issued, issued.AddHours(4));
        Assert.False(session.IsAbsolutelyExpired(issued.AddHours(4).AddSeconds(-1)));
        Assert.True(session.IsAbsolutelyExpired(issued.AddHours(4)));
        Assert.False(session.IsActive(issued.AddHours(4).AddSeconds(-1), TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void ASessionIsIdleExpiredExactlyAtLastActivityPlusTimeout()
    {
        var issued = DateTimeOffset.UtcNow;
        var session = NewSession(issued, issued, issued.AddHours(4));
        var idleTimeout = TimeSpan.FromMinutes(30);

        Assert.False(session.IsIdleExpired(issued.AddMinutes(30).AddSeconds(-1), idleTimeout));
        Assert.True(session.IsIdleExpired(issued.AddMinutes(30), idleTimeout));
        Assert.False(session.IsActive(issued.AddMinutes(30), idleTimeout));
    }

    [Fact]
    public void RevokeProducesARevokedCopyWithoutMutatingTheOriginal()
    {
        var session = NewSession();
        var at = DateTimeOffset.UtcNow;
        var revoked = session.Revoke(at, "customer left");

        Assert.False(session.IsRevoked);
        Assert.True(revoked.IsRevoked);
        Assert.Equal(at, revoked.RevokedAt);
        Assert.Equal("customer left", revoked.RevokedReason);
        Assert.False(revoked.IsActive(at, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void RevokingAnAlreadyRevokedSessionThrows()
    {
        var revoked = NewSession().Revoke(DateTimeOffset.UtcNow, "first");
        Assert.Throws<InvalidOperationException>(() => revoked.Revoke(DateTimeOffset.UtcNow, "second"));
    }

    [Fact]
    public void RevokingWithAnEmptyReasonThrows()
    {
        Assert.Throws<ArgumentException>(() => NewSession().Revoke(DateTimeOffset.UtcNow, ""));
    }

    [Fact]
    public void WithActivityAtProducesACopyWithoutMutatingTheOriginal()
    {
        var issued = DateTimeOffset.UtcNow;
        var session = NewSession(issued, issued, issued.AddHours(4));
        var touched = session.WithActivityAt(issued.AddMinutes(10));

        Assert.Equal(issued, session.LastActivityAt);
        Assert.Equal(issued.AddMinutes(10), touched.LastActivityAt);
    }

    [Fact]
    public void WithActivityAtRejectsMovingBackward()
    {
        var issued = DateTimeOffset.UtcNow;
        var session = NewSession(issued, issued.AddMinutes(10), issued.AddHours(4));
        Assert.Throws<ArgumentException>(() => session.WithActivityAt(issued.AddMinutes(5)));
    }
}
