namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// Canonical money and quantity rounding for billing (V0-CMP-002 same-basket invariant).
/// Uses kuruş rounding (per-line round-half-up to 2 decimals).
/// </summary>
public static class BillMath
{
    public static decimal RoundCurrency(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal RoundQuantity(decimal value)
        => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Splits <paramref name="total"/> over <paramref name="weights"/> in proportion, in whole kuruş (largest
    /// remainder): the shares add up to the total and each stays within a kuruş of its exact share.
    /// </summary>
    public static decimal[] Apportion(decimal total, IReadOnlyList<decimal> weights)
    {
        var shares = new decimal[weights.Count];
        var weightSum = weights.Sum();
        if (total <= 0m || weightSum <= 0m)
            return shares;

        var exact = weights.Select(weight => total * weight / weightSum).ToArray();
        for (var i = 0; i < shares.Length; i++)
            shares[i] = Math.Floor(exact[i] * 100m) / 100m;

        var missingCents = (int)Math.Round((total - shares.Sum()) * 100m);
        foreach (var index in Enumerable.Range(0, shares.Length)
                     .OrderByDescending(index => exact[index] - shares[index])
                     .ThenBy(index => index)
                     .Take(missingCents))
            shares[index] += 0.01m;
        return shares;
    }
}
