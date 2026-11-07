namespace ALKAROS.CustomerAccounts.Aging;

/// <summary>The business-day rule of the venue: a day runs from <paramref name="DayStartHour"/> local time to the same hour next day.</summary>
public sealed record BusinessCalendar(string TimeZoneId, int DayStartHour)
{
    public DateOnly DateOf(DateTimeOffset moment)
    {
        var local = TimeZoneInfo.ConvertTime(moment, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
        return DateOnly.FromDateTime(local.DateTime.AddHours(-DayStartHour));
    }
}

/// <summary>One ledger row as a signed balance effect (positive raises the debt).</summary>
public sealed record AgingLedgerEntry(DateTimeOffset OccurredAt, decimal Effect);

/// <summary>
/// What a customer still owes, by the age of the charge it came from, and what they paid beyond it.
/// <see cref="ProjectedBalance"/> is the cached balance, kept so a report can prove it agrees with the ledger.
/// </summary>
public sealed record CustomerAging(
    Guid CustomerId,
    decimal Days0To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal UnappliedCredit,
    decimal ProjectedBalance)
{
    public decimal TotalDebt => Days0To30 + Days31To60 + Days61To90 + Over90;

    public bool IsBalanced => TotalDebt - UnappliedCredit == ProjectedBalance;
}

public interface IReceivablesAgingService
{
    /// <summary>Every customer with a non-zero balance, largest debt first.</summary>
    Task<IReadOnlyList<CustomerAging>> GetAsync(DateOnly asOf, BusinessCalendar calendar, CancellationToken cancellationToken = default);
}

/// <summary>
/// Payments settle the oldest charges first - the same rule the credit policy uses to decide what is overdue.
/// A payment that exceeds all open debt is kept as credit and settles the next charge that arrives.
/// </summary>
public static class ReceivablesAgingCalculator
{
    public const int FirstBucketDays = 30;
    public const int SecondBucketDays = 60;
    public const int ThirdBucketDays = 90;

    public static CustomerAging Compute(
        Guid customerId,
        IEnumerable<AgingLedgerEntry> entriesOldestFirst,
        DateOnly asOf,
        BusinessCalendar calendar,
        decimal projectedBalance)
    {
        var open = new List<(DateOnly Date, decimal Amount)>();
        var head = 0;
        var credit = 0m;
        foreach (var entry in entriesOldestFirst)
        {
            if (entry.Effect > 0m)
            {
                var charge = entry.Effect;
                var used = Math.Min(credit, charge);
                credit -= used;
                charge -= used;
                if (charge > 0m)
                    open.Add((calendar.DateOf(entry.OccurredAt), charge));
            }
            else if (entry.Effect < 0m)
            {
                var payment = -entry.Effect;
                while (payment > 0m && head < open.Count)
                {
                    var used = Math.Min(payment, open[head].Amount);
                    payment -= used;
                    open[head] = (open[head].Date, open[head].Amount - used);
                    if (open[head].Amount == 0m)
                        head++;
                }

                credit += payment;
            }
        }

        decimal first = 0m, second = 0m, third = 0m, older = 0m;
        for (var index = head; index < open.Count; index++)
        {
            var age = asOf.DayNumber - open[index].Date.DayNumber;
            if (age <= FirstBucketDays) first += open[index].Amount;
            else if (age <= SecondBucketDays) second += open[index].Amount;
            else if (age <= ThirdBucketDays) third += open[index].Amount;
            else older += open[index].Amount;
        }

        return new CustomerAging(customerId, first, second, third, older, credit, projectedBalance);
    }
}
