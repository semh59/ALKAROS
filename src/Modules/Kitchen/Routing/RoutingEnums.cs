namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Specificity hierarchy levels for kitchen printer routing (V0-DOM-011 / PDF:I.16-I.20, PDF:II.3.13-II.3.14).
/// Specificity order: Item (1) > Product (2) > DailySpecial (3) > Category (4) > Default (5).
/// </summary>
public enum RouteLevel
{
    /// <summary>
    /// Level 1: Specific kitchen item or order item override.
    /// </summary>
    Item = 1,

    /// <summary>
    /// Level 2: Specific product override.
    /// </summary>
    Product = 2,

    /// <summary>
    /// Level 3: Daily special category / date-specific override.
    /// </summary>
    DailySpecial = 3,

    /// <summary>
    /// Level 4: Category-wide route.
    /// </summary>
    Category = 4,

    /// <summary>
    /// Level 5: System-wide default fallback printer route.
    /// </summary>
    Default = 5
}
