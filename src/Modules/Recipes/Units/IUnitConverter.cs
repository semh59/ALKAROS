namespace ALKAROS.Recipes.Units;

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
}
