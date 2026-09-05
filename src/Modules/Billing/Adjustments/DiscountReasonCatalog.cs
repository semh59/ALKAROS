namespace ALKAROS.Billing.Adjustments;

/// <summary>
/// Fixed reason catalog for bill-level discount adjustments (V0-DOM-006).
/// Free-text reasons alone are rejected to keep audit records actionable —
/// mirrors <c>ALKAROS.Orders.ItemExceptions.ComplimentaryReasonCatalog</c>'s
/// pattern for the sibling bills.comp grant.
/// </summary>
public static class DiscountReasonCatalog
{
    public const string CustomerLoyalty = "CustomerLoyalty";
    public const string PromotionalOffer = "PromotionalOffer";
    public const string ServiceRecovery = "ServiceRecovery";
    public const string ManagerDiscretion = "ManagerDiscretion";

    private static readonly HashSet<string> ValidReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        CustomerLoyalty,
        PromotionalOffer,
        ServiceRecovery,
        ManagerDiscretion,
    };

    public static bool IsValid(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && ValidReasons.Contains(reason.Trim());

    public static IReadOnlySet<string> AllReasons => ValidReasons;
}
