using ALKAROS.Identity.Authorization.Behavioural;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Behavioural;

public sealed class BehaviouralRateAssessorTests
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    [Fact]
    public void ASteadyRateAtBaselineIsNotASpike()
    {
        // 30 granted in 30 days -> 1/day baseline; 1 in the trailing day.
        var assessment = BehaviouralRateAssessor.Assess(recentCount: 1, baselinePeriodCount: 30, Day);

        assessment.IsSpiking.Should().BeFalse();
        assessment.BaselinePerWindow.Should().Be(1m);
    }

    [Fact]
    public void ThreeTimesTheBaselineInTheWindowIsASpike()
    {
        // 30 in 30 days -> 1/day; 3 today == 3x.
        BehaviouralRateAssessor.Assess(3, 30, Day).IsSpiking.Should().BeTrue();
        BehaviouralRateAssessor.Assess(2, 30, Day).IsSpiking.Should().BeFalse("below 3x and below the floor");
    }

    [Fact]
    public void BelowTheMinimumRecentCountNothingIsASpike()
    {
        // No baseline at all, but only two actions today.
        BehaviouralRateAssessor.Assess(2, 0, Day).IsSpiking.Should().BeFalse();
        BehaviouralRateAssessor.Assess(3, 0, Day).IsSpiking.Should().BeTrue("floor reached, no baseline to dilute it");
    }

    [Fact]
    public void TheRatioIsReportedAndNeverInfiniteWhenTheBaselineIsZero()
    {
        var assessment = BehaviouralRateAssessor.Assess(9, 0, Day);

        assessment.Ratio.Should().Be(9m, "9 / floor(1)");
        assessment.IsSpiking.Should().BeTrue();
    }

    [Fact]
    public void AShorterWindowScalesTheBaselineDown()
    {
        // 60 in 30 days -> 2/day -> 0.5 per 6h window (below the floor of 1);
        // 3 actions in 6h still clears the minimum-count gate and is a spike.
        var sixHours = TimeSpan.FromHours(6);
        var assessment = BehaviouralRateAssessor.Assess(3, 60, sixHours);

        assessment.BaselinePerWindow.Should().Be(0.5m);
        assessment.IsSpiking.Should().BeTrue();
    }

    [Fact]
    public void NegativeCountsAndAZeroWindowAreRejected()
    {
        FluentActions.Invoking(() => BehaviouralRateAssessor.Assess(-1, 0, Day))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => BehaviouralRateAssessor.Assess(0, -1, Day))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => BehaviouralRateAssessor.Assess(0, 0, TimeSpan.Zero))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
