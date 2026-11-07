namespace ALKAROS.CustomerAccounts.BalanceProjection.Tests;

using ALKAROS.CustomerAccounts.Aging;
using Xunit;

public sealed class ReceivablesAgingCalculatorTests
{
    private static readonly BusinessCalendar Istanbul = new("Europe/Istanbul", 6);
    private static readonly DateOnly AsOf = new(2026, 11, 7);
    private static readonly Guid Customer = Guid.NewGuid();

    // Istanbul is UTC+3 all year: 12:00 local is 09:00Z.
    private static AgingLedgerEntry At(DateOnly businessDate, decimal effect, int localHour = 12)
        => new(new DateTimeOffset(businessDate.ToDateTime(new TimeOnly(localHour, 0)), TimeSpan.FromHours(3)), effect);

    private static CustomerAging Age(params AgingLedgerEntry[] entries)
        => ReceivablesAgingCalculator.Compute(Customer, entries, AsOf, Istanbul, entries.Sum(entry => entry.Effect));

    [Theory]
    [InlineData(0, 100, 0, 0, 0)]
    [InlineData(30, 100, 0, 0, 0)]
    [InlineData(31, 0, 100, 0, 0)]
    [InlineData(60, 0, 100, 0, 0)]
    [InlineData(61, 0, 0, 100, 0)]
    [InlineData(90, 0, 0, 100, 0)]
    [InlineData(91, 0, 0, 0, 100)]
    public void AChargeLandsInTheBucketOfItsAgeInDays(int ageDays, decimal first, decimal second, decimal third, decimal older)
    {
        var aging = Age(At(AsOf.AddDays(-ageDays), 100m));

        Assert.Equal((first, second, third, older), (aging.Days0To30, aging.Days31To60, aging.Days61To90, aging.Over90));
        Assert.Equal(100m, aging.TotalDebt);
        Assert.True(aging.IsBalanced);
    }

    [Fact]
    public void ABusinessDayStartsAtSixSoTheEarlyMorningBelongsToThePreviousDay()
    {
        var thirtyDaysAgo = AsOf.AddDays(-30);

        var atSix = Age(At(thirtyDaysAgo, 40m, 6));
        var justBeforeSix = ReceivablesAgingCalculator.Compute(
            Customer,
            [new AgingLedgerEntry(At(thirtyDaysAgo, 40m, 6).OccurredAt.AddMinutes(-1), 40m)],
            AsOf, Istanbul, 40m);

        Assert.Equal(40m, atSix.Days0To30);
        Assert.Equal(40m, justBeforeSix.Days31To60);
    }

    [Fact]
    public void PaymentsSettleTheOldestChargeFirstAndLeaveTheRestOfAPartlyPaidOneInItsBucket()
    {
        var aging = Age(
            At(AsOf.AddDays(-100), 100.50m),
            At(AsOf.AddDays(-70), 200m),
            At(AsOf.AddDays(-10), 50m),
            At(AsOf.AddDays(-5), -150.75m));

        Assert.Equal((50m, 0m, 149.75m, 0m), (aging.Days0To30, aging.Days31To60, aging.Days61To90, aging.Over90));
        Assert.True(aging.IsBalanced);
    }

    [Fact]
    public void ANegativeAdjustmentReducesTheOldestDebtLikeAPayment()
    {
        var aging = Age(At(AsOf.AddDays(-45), 80m), At(AsOf.AddDays(-2), 20m), At(AsOf.AddDays(-1), -30m));

        Assert.Equal((20m, 50m), (aging.Days0To30, aging.Days31To60));
    }

    [Fact]
    public void AnOverpaymentIsKeptAsCreditInsteadOfAFutureNegativeBucket()
    {
        var aging = Age(At(AsOf.AddDays(-40), 100m), At(AsOf.AddDays(-3), -130m));

        Assert.Equal(0m, aging.TotalDebt);
        Assert.Equal(30m, aging.UnappliedCredit);
        Assert.True(aging.IsBalanced);
    }

    [Fact]
    public void CreditPaidBeforeAChargeSettlesThatChargeWhenItArrives()
    {
        var aging = Age(At(AsOf.AddDays(-20), -60m), At(AsOf.AddDays(-10), 100m));

        Assert.Equal(40m, aging.Days0To30);
        Assert.Equal(0m, aging.UnappliedCredit);
    }

    [Fact]
    public void LeftoverCreditKeepsSettlingEachFollowingCharge()
    {
        var aging = Age(At(AsOf.AddDays(-20), -100m), At(AsOf.AddDays(-10), 30m), At(AsOf.AddDays(-5), 50m), At(AsOf.AddDays(-1), 40m));

        Assert.Equal((20m, 0m), (aging.Days0To30, aging.UnappliedCredit));
    }

    [Fact]
    public void ACustomerWhoseCachedBalanceDisagreesWithTheLedgerIsNotBalanced()
    {
        var aging = ReceivablesAgingCalculator.Compute(Customer, [At(AsOf.AddDays(-1), 100m)], AsOf, Istanbul, 99m);

        Assert.False(aging.IsBalanced);
    }
}
