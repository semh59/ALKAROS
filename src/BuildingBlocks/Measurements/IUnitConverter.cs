namespace ALKAROS.Measurements;

/// <summary>
/// Dimension-safe unit conversion service interface.
/// </summary>
public interface IUnitConverter
{
    decimal Convert(decimal quantity, string fromUnitCode, string toUnitCode);
    bool TryConvert(decimal quantity, string fromUnitCode, string toUnitCode, out decimal result);
    bool CanConvert(string fromUnitCode, string toUnitCode);
    UnitDimension GetDimension(string unitCode);
    bool IsKnownUnit(string unitCode);

    /// <summary>
    /// V1-RMD-319 (independent 2026-09-26 audit, finding K7): was concrete-<see cref="UnitConverter"/>-only
    /// before this fix, so every caller holding the interface (every DI consumer in this codebase - the
    /// established convention is to inject <see cref="IUnitConverter"/>, never the concrete type) had no
    /// way to register a custom conversion at all, even once the DI lifetime became Singleton. Registers a
    /// bidirectional custom conversion rule (a bridge between two units the built-in dimension table does
    /// not otherwise relate) that <see cref="Convert"/>/<see cref="TryConvert"/> then honour.
    /// </summary>
    void RegisterConversion(string fromUnitCode, string toUnitCode, decimal factor);
}
