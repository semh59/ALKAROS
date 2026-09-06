namespace ALKAROS.Measurements;

/// <summary>
/// Immutable definition of a measurement unit.
/// </summary>
public sealed record UnitDefinition
{
    public string Code { get; }
    public UnitDimension Dimension { get; }
    public string DisplayName { get; }
    public decimal FactorToBase { get; }
    public bool IsBaseUnit { get; }

    public UnitDefinition(string code, UnitDimension dimension, string displayName, decimal factorToBase, bool isBaseUnit = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Unit code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        if (factorToBase <= 0m)
            throw new ArgumentOutOfRangeException(nameof(factorToBase), "Factor to base must be strictly positive.");

        Code = code.Trim().ToLowerInvariant();
        Dimension = dimension;
        DisplayName = displayName.Trim();
        FactorToBase = factorToBase;
        IsBaseUnit = isBaseUnit;
    }
}
