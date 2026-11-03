using ALKAROS.Reporting.BusinessDayTotals;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Reporting.V1Operations.Tests;

public sealed class BusinessDayWindowTests
{
    private static readonly DateOnly Day = new(2031, 3, 10);

    [Fact]
    public void TheWindowRunsFromSixInTheMorningToSixTheNextMorningInTheBusinessZone()
    {
        var (start, end) = BusinessDayWindow.Of(Day);

        start.Should().Be(new DateTimeOffset(2031, 3, 10, 3, 0, 0, TimeSpan.Zero));
        end.Should().Be(new DateTimeOffset(2031, 3, 11, 3, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(2031, 3, 10, 2, 59, 59, false)]
    [InlineData(2031, 3, 10, 3, 0, 0, true)]
    [InlineData(2031, 3, 11, 2, 59, 59, true)]
    [InlineData(2031, 3, 11, 3, 0, 0, false)]
    public void TheBoundaryBelongsToTheDayThatStartsAtSix(int year, int month, int day, int hour, int minute, int second, bool inside)
    {
        var (start, end) = BusinessDayWindow.Of(Day);
        var instant = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);

        (instant >= start && instant < end).Should().Be(inside);
    }

    [Fact]
    public void ARangeCoversEveryDayFromTheFirstSixToTheSixAfterTheLastDay()
    {
        var (start, end) = BusinessDayWindow.Of(Day, Day.AddDays(2));

        start.Should().Be(BusinessDayWindow.Of(Day).Start);
        end.Should().Be(BusinessDayWindow.Of(Day.AddDays(2)).End);
    }

    [Fact]
    public void AZoneWithDaylightSavingKeepsTheSixOClockStartAndTheShortDayIsTwentyThreeHours()
    {
        var (start, end) = BusinessDayWindow.Of(new DateOnly(2031, 3, 29), "Europe/Berlin");

        start.Should().Be(new DateTimeOffset(2031, 3, 29, 5, 0, 0, TimeSpan.Zero));
        end.Should().Be(new DateTimeOffset(2031, 3, 30, 4, 0, 0, TimeSpan.Zero));
        (end - start).Should().Be(TimeSpan.FromHours(23));
    }
}
