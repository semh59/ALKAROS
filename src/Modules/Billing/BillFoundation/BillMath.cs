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
}
