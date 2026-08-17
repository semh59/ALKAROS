namespace ALKAROS.Billing.SplitDesign;

/// <summary>
/// Canonical owner types for bill allocations (PDF:III.7.3).
/// </summary>
public enum AllocationOwnerType
{
    Person,
    Item,
    Amount,
    CustomerAccount,
}

/// <summary>
/// Operational split mode for a bill partition design.
/// </summary>
public enum SplitMode
{
    EqualByPerson,
    ByItem,
    ByAmount,
    Custom,
}
