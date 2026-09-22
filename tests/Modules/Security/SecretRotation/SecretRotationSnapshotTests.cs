using Xunit;

namespace ALKAROS.Security.SecretRotation.Tests;

public sealed class SecretRotationSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SummarizesRotationStateWithoutCarryingAnySecretValue()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Rotate(Now.AddDays(1), TimeSpan.FromHours(6))
            .Rotate(Now.AddDays(2), TimeSpan.FromHours(6));

        var snapshot = SecretRotationSnapshot.From(record);

        Assert.Equal("db-password", snapshot.SecretName);
        Assert.Equal(3, snapshot.ActiveVersion);
        Assert.Equal(Now.AddDays(2), snapshot.ActiveActivatedAtUtc);
        Assert.Equal(2, snapshot.OverlapVersionCount);
        Assert.Equal(Now.AddDays(1).AddHours(6), snapshot.EarliestOverlapExpiresAtUtc);
        Assert.Equal(0, snapshot.RevokedVersionCount);
        Assert.Equal(Now.AddDays(2), snapshot.LastRotationAtUtc);
    }

    [Fact]
    public void ReportsNullActiveVersionWhenTheActiveVersionWasRevokedAsAnIncidentResponse()
    {
        var record = SecretRotationRecord.Initialize("db-password", Now)
            .Revoke(1, Now.AddHours(1));

        var snapshot = SecretRotationSnapshot.From(record);

        Assert.Null(snapshot.ActiveVersion);
        Assert.Null(snapshot.ActiveActivatedAtUtc);
        Assert.Equal(1, snapshot.RevokedVersionCount);
    }
}
