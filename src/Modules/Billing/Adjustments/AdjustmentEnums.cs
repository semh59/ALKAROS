namespace ALKAROS.Billing.Adjustments;

/// <summary>
/// Canonical adjustment types for bill adjustments (PDF:III.7.2, V0-DOM-006).
/// </summary>
public enum AdjustmentType
{
    DiscountPercentage,
    DiscountAmount,
    ServiceFee,
    Kuver,
    Tip,
    CustomFee,
}

/// <summary>
/// Calculation method for bill adjustments.
/// </summary>
public enum AdjustmentCalculationType
{
    Percentage,
    FixedAmount,
}
