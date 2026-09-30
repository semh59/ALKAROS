namespace ALKAROS.Orders.OrderAggregate;

/// <summary>
/// Money and quantity rounding used by the order aggregate. Postgres money
/// columns are NUMERIC(18,2) and quantity NUMERIC(18,3); every computed value
/// rounds half-up (kuruş, V0-CMP-002 same-basket rule).
/// </summary>
internal static class OrderMath
{
    public static decimal RoundCurrency(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Tax contained in a tax-inclusive gross amount (tax first, half-up; net is gross minus this).</summary>
    public static decimal TaxIncludedIn(decimal gross, decimal taxRatePercent)
        => RoundCurrency(gross * taxRatePercent / (100m + taxRatePercent));

    public static decimal RoundQuantity(decimal value)
        => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}