namespace ALKAROS.Billing.Adjustments;

/// <summary>The discounts on a bill add up to more than its payable amount plus fees.</summary>
public sealed class DiscountExceedsPayableException(string message) : InvalidOperationException(message);
