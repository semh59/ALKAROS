using Xunit;

namespace ALKAROS.Security.SecretRotation.Tests;

public sealed class SecretRotationRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InitializeStartsAtVersionOneActive()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now);

        Assert.Equal("db-password", record.SecretName);
        Assert.Single(record.Versions);
        Assert.Equal(1, record.ActiveVersion!.Version);
        Assert.Equal(SecretVersionStatus.Active, record.ActiveVersion.Status);
        Assert.Empty(record.OverlapVersions);
    }

    [Fact]
    public void RotateActivatesNextVersionAndDemotesPreviousActiveToOverlap()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now);

        var rotated = record.Rotate(Now.AddDays(30), TimeSpan.FromHours(6));

        Assert.Equal(2, rotated.ActiveVersion!.Version);
        var overlap = Assert.Single(rotated.OverlapVersions);
        Assert.Equal(1, overlap.Version);
        Assert.Equal(Now.AddDays(30).AddHours(6), overlap.OverlapExpiresAtUtc);
    }

    [Fact]
    public void RotateAfterActiveWasRevokedStillProducesANewActiveVersion()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Revoke(1, Now.AddHours(1));
        Assert.Null(record.ActiveVersion);

        var rotated = record.Rotate(Now.AddHours(2), TimeSpan.FromHours(6));

        Assert.Equal(2, rotated.ActiveVersion!.Version);
        Assert.Empty(rotated.OverlapVersions);
    }

    [Fact]
    public void RevokeUnknownVersionThrows()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now);

        var exception = Assert.Throws<SecretRotationConflictException>(() => record.Revoke(99, Now));
        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public void RevokeAlreadyRevokedVersionThrows()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Revoke(1, Now);

        Assert.Throws<SecretRotationConflictException>(() => record.Revoke(1, Now.AddMinutes(1)));
    }

    [Fact]
    public void RollbackPromotesOverlapVersionAndRevokesCurrentActive()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Rotate(Now.AddDays(1), TimeSpan.FromHours(6));

        var rolledBack = record.Rollback(1, Now.AddDays(1).AddHours(1));

        Assert.Equal(1, rolledBack.ActiveVersion!.Version);
        var revoked = Assert.Single(rolledBack.Versions, v => v.Version == 2);
        Assert.Equal(SecretVersionStatus.Revoked, revoked.Status);
    }

    [Fact]
    public void RollbackToNonOverlapVersionThrows()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Rotate(Now.AddDays(1), TimeSpan.FromHours(6))
            .Revoke(1, Now.AddDays(1).AddMinutes(1));

        var exception = Assert.Throws<SecretRotationConflictException>(() => record.Rollback(1, Now.AddDays(1).AddHours(1)));
        Assert.Contains("not in Overlap status", exception.Message);
    }

    [Fact]
    public void RollbackAfterOverlapWindowExpiredThrows()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Rotate(Now, TimeSpan.FromHours(1));

        var exception = Assert.Throws<SecretRotationConflictException>(() => record.Rollback(1, Now.AddHours(2)));
        Assert.Contains("expired overlap window", exception.Message);
    }

    [Fact]
    public void ExpireOverlapWindowsRevokesOnlyExpiredOverlapVersions()
    {
        // v1 -> Overlap at Now+1h, expires Now+2h (short window).
        // v2 -> Overlap at Now+3h, expires Now+13h (long window).
        // v3 -> Active at Now+3h.
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Rotate(Now.AddHours(1), TimeSpan.FromHours(1))
            .Rotate(Now.AddHours(3), TimeSpan.FromHours(10));

        var swept = record.ExpireOverlapWindows(Now.AddHours(5));

        var v1 = Assert.Single(swept.Versions, v => v.Version == 1);
        Assert.Equal(SecretVersionStatus.Revoked, v1.Status);
        var v2 = Assert.Single(swept.Versions, v => v.Version == 2);
        Assert.Equal(SecretVersionStatus.Overlap, v2.Status);
        var v3 = Assert.Single(swept.Versions, v => v.Version == 3);
        Assert.Equal(SecretVersionStatus.Active, v3.Status);
    }

    [Fact]
    public void RestoreRejectsMoreThanOneActiveVersion()
    {
        var versions = new[]
        {
            new SecretVersionRecord(1, SecretVersionStatus.Active, Now, null, null),
            new SecretVersionRecord(2, SecretVersionStatus.Active, Now, null, null),
        };

        Assert.Throws<ArgumentException>(() => SecretRotationRecord.Restore("db-password", versions));
    }
}
