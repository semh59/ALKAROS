namespace ALKAROS.Reporting.ProductMargin;

/// <summary>Filters the product margin report to a range of Europe/Istanbul service days (the day the bill was opened).</summary>
public sealed record ProductMarginFilter(DateOnly From, DateOnly To)
{
    public const int MaxDays = 31;

    public void Validate()
    {
        if (To < From)
            throw new ArgumentException("The end date cannot be before the start date.", nameof(To));
        if (To.DayNumber - From.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"A product margin report covers at most {MaxDays} business dates.", nameof(To));
    }
}

/// <summary>
/// One product over the range. <see cref="NetRevenue"/> is the sale lines' net amount (VAT excluded, extras included);
/// <see cref="GivenAwayQuantity"/> is complimentary units, which carry cost but no revenue. <see cref="Cost"/> is the part of the cost
/// that could be valued; when <see cref="UnknownCostLines"/> is above zero the real cost is higher, so <see cref="GrossMargin"/> and
/// <see cref="MarginPercent"/> stay null instead of showing a flattering number.
/// </summary>
public sealed record ProductMarginRow(
    Guid ProductId,
    string ProductName,
    decimal SoldQuantity,
    decimal GivenAwayQuantity,
    decimal NetRevenue,
    decimal Cost,
    int UnknownCostLines,
    decimal? GrossMargin,
    decimal? MarginPercent);

/// <summary>
/// <see cref="ProductNetTotal"/> is the sum of the rows; <see cref="LinesNetTotal"/> is the same lines summed by a separate query,
/// so a grouping mistake shows as <see cref="IsBalanced"/> false. <see cref="BillLevelDiscounts"/> is what the bills' own
/// discounts took off on top, which is not spread over products.
/// </summary>
public sealed record ProductMarginCheck(decimal ProductNetTotal, decimal LinesNetTotal, decimal BillLevelDiscounts, bool IsBalanced);

public sealed record ProductMarginReport(
    string ReportVersion,
    DateOnly From,
    DateOnly To,
    string TimeZoneId,
    IReadOnlyList<ProductMarginRow> Rows,
    decimal TotalNetRevenue,
    decimal TotalCost,
    int UnknownCostLines,
    ProductMarginCheck Check);
