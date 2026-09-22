using Xunit;

namespace ALKAROS.Operations.OffsiteBackup.Tests;

public sealed class RpoCoverageCheckerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EvaluateNewestReceiptWithinTargetMeetsTarget()
    {
        var receipts = new[]
        {
            Receipt(DataClass.Fiscal, Now - TimeSpan.FromMinutes(3)),
        };

        var result = RpoCoverageChecker.Evaluate(DataClass.Fiscal, receipts, Now);

        Assert.True(result.MeetsTarget);
        Assert.Equal(TimeSpan.FromMinutes(5), result.Target);
        Assert.Equal(TimeSpan.FromMinutes(3), result.MeasuredGap);
    }

    [Fact]
    public void EvaluateNewestReceiptBeyondTargetDoesNotMeetTarget()
    {
        var receipts = new[]
        {
            Receipt(DataClass.Fiscal, Now - TimeSpan.FromMinutes(10)),
        };

        var result = RpoCoverageChecker.Evaluate(DataClass.Fiscal, receipts, Now);

        Assert.False(result.MeetsTarget);
    }

    [Fact]
    public void EvaluateNoReceiptsForDataClassDoesNotMeetTarget()
    {
        var result = RpoCoverageChecker.Evaluate(DataClass.OrdersInventory, [], Now);

        Assert.False(result.MeetsTarget);
        Assert.Null(result.MeasuredGap);
    }

    [Fact]
    public void EvaluateUsesOnlyTheRequestedDataClassAndPicksTheNewest()
    {
        var receipts = new[]
        {
            Receipt(DataClass.OrdersInventory, Now - TimeSpan.FromMinutes(1)),
            Receipt(DataClass.Fiscal, Now - TimeSpan.FromHours(2)),
            Receipt(DataClass.Fiscal, Now - TimeSpan.FromMinutes(4)),
        };

        var result = RpoCoverageChecker.Evaluate(DataClass.Fiscal, receipts, Now);

        Assert.Equal(TimeSpan.FromMinutes(4), result.MeasuredGap);
        Assert.True(result.MeetsTarget);
    }

    private static OffsiteBackupReceipt Receipt(DataClass dataClass, DateTimeOffset uploadedAt) => new(
        Guid.NewGuid().ToString("N"),
        dataClass,
        new string('a', 64),
        "test-key",
        1,
        1024,
        "local://test",
        uploadedAt);
}
