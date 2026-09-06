using System.Collections.Frozen;

namespace ALKAROS.Measurements;

/// <summary>
/// Canonical standard units and dimensions supported natively.
/// </summary>
public static class StandardUnits
{
    // Mass
    public static readonly UnitDefinition Kilogram = new("kg", UnitDimension.Mass, "Kilogram", 1.0m, isBaseUnit: true);
    public static readonly UnitDefinition Gram = new("g", UnitDimension.Mass, "Gram", 0.001m);
    public static readonly UnitDefinition Milligram = new("mg", UnitDimension.Mass, "Milligram", 0.000001m);

    // Volume
    public static readonly UnitDefinition Liter = new("l", UnitDimension.Volume, "Liter", 1.0m, isBaseUnit: true);
    public static readonly UnitDefinition Milliliter = new("ml", UnitDimension.Volume, "Milliliter", 0.001m);
    public static readonly UnitDefinition Centiliter = new("cl", UnitDimension.Volume, "Centiliter", 0.01m);

    // Count
    public static readonly UnitDefinition Piece = new("piece", UnitDimension.Count, "Piece", 1.0m, isBaseUnit: true);
    public static readonly UnitDefinition Portion = new("portion", UnitDimension.Count, "Portion", 1.0m);
    public static readonly UnitDefinition Pack = new("pack", UnitDimension.Count, "Pack", 1.0m);
    public static readonly UnitDefinition Box = new("box", UnitDimension.Count, "Box", 1.0m);

    // Turkish aliases
    public static readonly UnitDefinition Adet = new("adet", UnitDimension.Count, "Adet", 1.0m, isBaseUnit: true);
    public static readonly UnitDefinition Porsiyon = new("porsiyon", UnitDimension.Count, "Porsiyon", 1.0m);
    public static readonly UnitDefinition Paket = new("paket", UnitDimension.Count, "Paket", 1.0m);
    public static readonly UnitDefinition Koli = new("koli", UnitDimension.Count, "Koli", 1.0m);

    public static readonly FrozenDictionary<string, UnitDefinition> All = new Dictionary<string, UnitDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        [Kilogram.Code] = Kilogram,
        [Gram.Code] = Gram,
        [Milligram.Code] = Milligram,

        [Liter.Code] = Liter,
        [Milliliter.Code] = Milliliter,
        [Centiliter.Code] = Centiliter,

        [Piece.Code] = Piece,
        [Portion.Code] = Portion,
        [Pack.Code] = Pack,
        [Box.Code] = Box,

        [Adet.Code] = Adet,
        [Porsiyon.Code] = Porsiyon,
        [Paket.Code] = Paket,
        [Koli.Code] = Koli
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
}
