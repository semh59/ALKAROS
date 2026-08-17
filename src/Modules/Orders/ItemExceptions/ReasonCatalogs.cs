namespace ALKAROS.Orders.ItemExceptions;

/// <summary>
/// Fixed reason catalog for order item void operations (PDF:I.24, PDF:I.28.1, V0-DOM-006).
/// Free-text reasons alone are rejected to keep audit records actionable.
/// </summary>
public static class VoidReasonCatalog
{
    public const string OperatorError = "OperatorError";
    public const string ProductUnavailable = "ProductUnavailable";
    public const string CustomerChange = "CustomerChange";
    public const string DuplicateEntry = "DuplicateEntry";

    private static readonly HashSet<string> ValidReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        OperatorError,
        ProductUnavailable,
        CustomerChange,
        DuplicateEntry,
    };

    public static bool IsValid(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && ValidReasons.Contains(reason.Trim());

    public static IReadOnlySet<string> AllReasons => ValidReasons;
}

/// <summary>
/// Fixed reason catalog for complimentary item operations (PDF:I.28.1, V0-DOM-006).
/// Mandatory reason required for manager-authorized zero-price hospitality/recovery.
/// </summary>
public static class ComplimentaryReasonCatalog
{
    public const string CustomerSatisfaction = "CustomerSatisfaction";
    public const string ManagerPromotion = "ManagerPromotion";
    public const string VIPGuest = "VIPGuest";
    public const string ServiceApology = "ServiceApology";

    private static readonly HashSet<string> ValidReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        CustomerSatisfaction,
        ManagerPromotion,
        VIPGuest,
        ServiceApology,
    };

    public static bool IsValid(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && ValidReasons.Contains(reason.Trim());

    public static IReadOnlySet<string> AllReasons => ValidReasons;
}
