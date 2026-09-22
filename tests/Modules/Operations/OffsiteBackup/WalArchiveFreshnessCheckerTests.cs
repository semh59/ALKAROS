using Xunit;

namespace ALKAROS.Operations.OffsiteBackup.Tests;

public sealed class WalArchiveFreshnessCheckerTests
{
    [Fact]
    public void EvaluateOffsiteHasNewestLocalSegmentMeetsFiscalRpo()
    {
        string[] local = ["000000010000000000000001", "000000010000000000000002"];
        string[] offsite = ["000000010000000000000001", "000000010000000000000002"];

        var result = WalArchiveFreshnessChecker.Evaluate(local, offsite);

        Assert.True(result.MeetsFiscalRpo);
        Assert.Equal(TimeSpan.Zero, result.PositionGap);
    }

    [Fact]
    public void EvaluateOneSegmentPendingGapIsOneArchiveTimeoutInterval()
    {
        string[] local = ["000000010000000000000001", "000000010000000000000002"];
        string[] offsite = ["000000010000000000000001"];

        var result = WalArchiveFreshnessChecker.Evaluate(local, offsite);

        Assert.Equal(TimeSpan.FromMinutes(5), result.PositionGap);
        Assert.True(result.MeetsFiscalRpo);
    }

    [Fact]
    public void EvaluateTwoSegmentsPendingExceedsFiveMinuteFiscalTarget()
    {
        string[] local = ["000000010000000000000001", "000000010000000000000002", "000000010000000000000003"];
        string[] offsite = ["000000010000000000000001"];

        var result = WalArchiveFreshnessChecker.Evaluate(local, offsite);

        Assert.Equal(TimeSpan.FromMinutes(10), result.PositionGap);
        Assert.False(result.MeetsFiscalRpo);
    }

    [Fact]
    public void EvaluateNoOffsiteSegmentsYetAllLocalSegmentsArePending()
    {
        string[] local = ["000000010000000000000001", "000000010000000000000002"];

        var result = WalArchiveFreshnessChecker.Evaluate(local, []);

        Assert.Equal(TimeSpan.FromMinutes(10), result.PositionGap);
        Assert.False(result.MeetsFiscalRpo);
    }

    [Fact]
    public void EvaluateNoLocalSegmentsReturnsNullGapAndFails()
    {
        var result = WalArchiveFreshnessChecker.Evaluate([], []);

        Assert.Null(result.PositionGap);
        Assert.False(result.MeetsFiscalRpo);
    }
}
