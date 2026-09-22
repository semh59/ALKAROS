using Xunit;

namespace ALKAROS.Security.SecretRotation.Tests;

public sealed class SecretVersionRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RejectsNonPositiveVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SecretVersionRecord(0, SecretVersionStatus.Active, Now, null, null));
    }

    [Fact]
    public void OverlapWithoutExpiryIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new SecretVersionRecord(1, SecretVersionStatus.Overlap, Now, null, null));
    }

    [Fact]
    public void ActiveWithOverlapExpiryIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new SecretVersionRecord(1, SecretVersionStatus.Active, Now, Now.AddHours(1), null));
    }

    [Fact]
    public void RevokedWithoutRevocationTimestampIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new SecretVersionRecord(1, SecretVersionStatus.Revoked, Now, null, null));
    }
}
