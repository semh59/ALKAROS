using ALKAROS.Billing.BillFoundation;

namespace ALKAROS.Billing.Adjustments;

/// <summary>One tax rate of a bill after its discounts, fees and tips: what the receipt prints per KDV rate.</summary>
public sealed record TaxBreakdownLine(decimal RatePercent, decimal Net, decimal Tax, decimal Gross);

public static class TaxBreakdown
{
    /// <summary>
    /// The bill's lines grouped by tax rate, scaled to the adjusted totals: gross adds up to the payable amount
    /// without tips (tips carry no tax) and tax to <see cref="AdjustedBillSummary.AdjustedTaxTotal"/>, to the kuruş.
    /// </summary>
    public static IReadOnlyList<TaxBreakdownLine> Build(Bill bill, AdjustedBillSummary summary)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(summary);

        var groups = bill.Items
            .Where(item => item.GrossAmount > 0m)
            .GroupBy(item => item.TaxRate)
            .OrderBy(group => group.Key)
            .ToList();
        if (groups.Count == 0)
            return [];

        var gross = BillMath.Apportion(
            BillMath.RoundCurrency(summary.AdjustedPayableAmount - summary.TotalTips),
            groups.Select(group => group.Sum(item => item.GrossAmount)).ToArray());
        var tax = BillMath.Apportion(summary.AdjustedTaxTotal, groups.Select(group => group.Sum(item => item.TaxAmount)).ToArray());

        return groups
            .Select((group, index) => new TaxBreakdownLine(group.Key, gross[index] - tax[index], tax[index], gross[index]))
            .ToList();
    }
}
