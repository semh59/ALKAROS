namespace ALKAROS.Measurements;

/// <summary>
/// Physical dimensions for dimension-safe units. Conversions between different
/// dimensions without an explicit bridging rule are rejected.
/// </summary>
public enum UnitDimension
{
    Mass,
    Volume,
    Count
}
