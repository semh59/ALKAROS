namespace ALKAROS.Invoicing.Generation;

/// <summary>A bill's tax-inclusive amount at one KDV rate - the weight a charge on that bill is split by.</summary>
public sealed record BillTaxWeight(decimal TaxRate, decimal GrossAmount);

/// <summary>An invoiced tax group before it becomes a line: every charged amount at one rate.</summary>
public sealed record InvoiceTaxGroup(decimal TaxRate, decimal NetAmount, decimal TaxAmount, decimal GrossAmount);

/// <summary>
/// V14-INV-002: the invoice's arithmetic, kept free of I/O. Money rounds half away from zero to kurus (V0-CMP-002).
/// </summary>
public static class InvoiceTaxCalculator
{
    /// <summary>
    /// Splits <paramref name="amount"/> across the bill's KDV rates in proportion to each rate's gross, to the kurus,
    /// by largest remainder (ties to the higher rate): the parts always add up to <paramref name="amount"/>. Rates
    /// whose bill total is not positive (fully discounted) take no share. Returns null when no rate has a positive
    /// total - the charge cannot be split.
    /// </summary>
    public static IReadOnlyDictionary<decimal, decimal>? SplitByTaxRate(decimal amount, IEnumerable<BillTaxWeight> weights)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentNullException.ThrowIfNull(weights);

        var positive = weights
            .GroupBy(weight => weight.TaxRate)
            .Select(group => (Rate: group.Key, Gross: group.Sum(weight => weight.GrossAmount)))
            .Where(group => group.Gross > 0m)
            .OrderByDescending(group => group.Rate)
            .ToList();
        if (positive.Count == 0)
            return null;

        var total = positive.Sum(group => group.Gross);
        var amountInKurus = decimal.ToInt64(RoundMoney(amount) * 100m);
        var shares = positive
            .Select(group =>
            {
                var exact = amountInKurus * group.Gross / total;
                var floor = decimal.Floor(exact);
                return (group.Rate, Kurus: decimal.ToInt64(floor), Remainder: exact - floor);
            })
            .ToList();

        var left = amountInKurus - shares.Sum(share => share.Kurus);
        foreach (var index in shares
                     .Select((share, index) => (share, index))
                     .OrderByDescending(item => item.share.Remainder)
                     .ThenByDescending(item => item.share.Rate)
                     .Select(item => item.index)
                     .Take((int)left))
        {
            shares[index] = shares[index] with { Kurus = shares[index].Kurus + 1 };
        }

        return shares
            .Where(share => share.Kurus > 0)
            .ToDictionary(share => share.Rate, share => share.Kurus / 100m);
    }

    /// <summary>
    /// Turns tax-inclusive totals per rate into invoice groups: the tax is rounded once per group and the net is the
    /// remainder, so net + tax equals the gross the buyer was charged. Ordered by rate, highest first.
    /// </summary>
    public static IReadOnlyList<InvoiceTaxGroup> Groups(IReadOnlyDictionary<decimal, decimal> grossByRate)
    {
        ArgumentNullException.ThrowIfNull(grossByRate);

        return grossByRate
            .Where(pair => pair.Value != 0m)
            .OrderByDescending(pair => pair.Key)
            .Select(pair =>
            {
                var gross = RoundMoney(pair.Value);
                var tax = RoundMoney(gross * pair.Key / (100m + pair.Key));
                return new InvoiceTaxGroup(pair.Key, gross - tax, tax, gross);
            })
            .ToList();
    }

    public static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
