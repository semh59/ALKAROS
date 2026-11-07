namespace ALKAROS.CustomerAccounts.Aging;

/// <summary>The business-day rule of the venue: a day runs from <paramref name="DayStartHour"/> local time to the same hour next day.</summary>
public sealed record BusinessCalendar(string TimeZoneId, int DayStartHour)
{
    public DateOnly DateOf(DateTimeOffset moment)
    {
        var local = TimeZoneInfo.ConvertTime(moment, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
        return DateOnly.FromDateTime(local.DateTime.AddHours(-DayStartHour));
    }

    /// <summary>The first instant of business day <paramref name="day"/>.</summary>
    public DateTimeOffset StartOf(DateOnly day)
    {
        var localStart = day.ToDateTime(new TimeOnly(DayStartHour, 0));
        return new DateTimeOffset(localStart, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId).GetUtcOffset(localStart));
    }
}

/// <summary>
/// What a customer still owes, by the age of the charge it came from, and what they paid beyond it.
/// <see cref="ProjectedBalance"/> is the cached balance, kept so a report can prove it agrees with the ledger.
/// <see cref="OverdueAmount"/> is the open debt older than the customer's <see cref="PaymentTermDays"/>; both are null
/// for a customer with no payment term.
/// </summary>
public sealed record CustomerAging(
    Guid CustomerId,
    decimal Days0To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal UnappliedCredit,
    decimal ProjectedBalance,
    int? PaymentTermDays,
    decimal? OverdueAmount)
{
    public decimal TotalDebt => Days0To30 + Days31To60 + Days61To90 + Over90;

    public bool IsBalanced => TotalDebt - UnappliedCredit == ProjectedBalance;
}

public interface IReceivablesAgingService
{
    /// <summary>Every customer with a non-zero balance, largest debt first.</summary>
    Task<IReadOnlyList<CustomerAging>> GetAsync(DateOnly asOf, BusinessCalendar calendar, CancellationToken cancellationToken = default);
}
