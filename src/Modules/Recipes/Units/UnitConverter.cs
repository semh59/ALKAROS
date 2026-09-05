using System.Collections.Concurrent;

namespace ALKAROS.Recipes.Units;

/// <summary>
/// Dimension-safe, deterministic unit converter.
/// Implements 1e-9 tolerance invertibility checks and rejects contradictory cycles or invalid dimensions.
/// </summary>
public sealed class UnitConverter : IUnitConverter
{
    public const decimal InvertibilityTolerance = 1e-9m;

    private readonly ConcurrentDictionary<string, UnitDefinition> _units =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<(string From, string To), decimal> _customConversions =
        new();

    public UnitConverter()
    {
        foreach (var (code, def) in StandardUnits.All)
        {
            _units[code] = def;
        }
    }

    public void RegisterUnit(UnitDefinition unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        _units[unit.Code] = unit;
    }

    public void RegisterConversion(string fromUnitCode, string toUnitCode, decimal factor)
    {
        if (string.IsNullOrWhiteSpace(fromUnitCode))
            throw new ArgumentException("From unit code cannot be empty.", nameof(fromUnitCode));
        if (string.IsNullOrWhiteSpace(toUnitCode))
            throw new ArgumentException("To unit code cannot be empty.", nameof(toUnitCode));
        if (factor <= 0m)
            throw new InvalidUnitConversionFactorException($"Conversion factor must be strictly positive, got {factor}.");

        var from = fromUnitCode.Trim().ToLowerInvariant();
        var to = toUnitCode.Trim().ToLowerInvariant();

        if (from == to)
        {
            if (Math.Abs(factor - 1.0m) > InvertibilityTolerance)
                throw new ContradictoryUnitConversionException($"Cannot register conversion from '{from}' to '{to}' with factor {factor} != 1.");
            return;
        }

        // Validate contradictory conversion with inverse if already exists
        if (_customConversions.TryGetValue((to, from), out var existingInverse))
        {
            var expectedInverse = 1.0m / factor;
            if (Math.Abs(existingInverse - expectedInverse) > InvertibilityTolerance)
            {
                throw new ContradictoryUnitConversionException(
                    $"Contradictory conversion detected: reverse conversion '{to}' -> '{from}' is {existingInverse}, but new factor '{from}' -> '{to}' is {factor} (expected reverse {expectedInverse}).");
            }
        }

        _customConversions[(from, to)] = factor;
        _customConversions[(to, from)] = 1.0m / factor;
    }

    public bool IsKnownUnit(string unitCode)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
            return false;
        return _units.ContainsKey(unitCode.Trim());
    }

    public UnitDimension GetDimension(string unitCode)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("Unit code cannot be empty.", nameof(unitCode));

        var key = unitCode.Trim().ToLowerInvariant();
        if (_units.TryGetValue(key, out var def))
            return def.Dimension;

        throw new UnknownUnitException(unitCode);
    }

    public bool CanConvert(string fromUnitCode, string toUnitCode)
    {
        return TryConvert(1.0m, fromUnitCode, toUnitCode, out _);
    }

    public bool TryConvert(decimal quantity, string fromUnitCode, string toUnitCode, out decimal result)
    {
        result = 0m;
        if (quantity < 0m)
            return false;

        if (string.IsNullOrWhiteSpace(fromUnitCode) || string.IsNullOrWhiteSpace(toUnitCode))
            return false;

        var from = fromUnitCode.Trim().ToLowerInvariant();
        var to = toUnitCode.Trim().ToLowerInvariant();

        if (!_units.TryGetValue(from, out var fromDef) || !_units.TryGetValue(to, out var toDef))
        {
            // Check direct custom conversion
            if (_customConversions.TryGetValue((from, to), out var factor))
            {
                result = quantity * factor;
                return true;
            }
            return false;
        }

        if (from == to)
        {
            result = quantity;
            return true;
        }

        if (fromDef.Dimension == toDef.Dimension)
        {
            // Invertible conversion via dimension base unit
            var baseQty = quantity * fromDef.FactorToBase;
            result = baseQty / toDef.FactorToBase;
            return true;
        }

        // Cross-dimension: requires custom bridge rule
        if (_customConversions.TryGetValue((from, to), out var customFactor))
        {
            result = quantity * customFactor;
            return true;
        }

        return false;
    }

    public decimal Convert(decimal quantity, string fromUnitCode, string toUnitCode)
    {
        if (quantity < 0m)
            throw new InvalidUnitQuantityException($"Quantity cannot be negative, got {quantity}.");

        if (string.IsNullOrWhiteSpace(fromUnitCode))
            throw new ArgumentException("From unit code cannot be empty.", nameof(fromUnitCode));
        if (string.IsNullOrWhiteSpace(toUnitCode))
            throw new ArgumentException("To unit code cannot be empty.", nameof(toUnitCode));

        var from = fromUnitCode.Trim().ToLowerInvariant();
        var to = toUnitCode.Trim().ToLowerInvariant();

        if (!_units.TryGetValue(from, out var fromDef))
            throw new UnknownUnitException(fromUnitCode);
        if (!_units.TryGetValue(to, out var toDef))
            throw new UnknownUnitException(toUnitCode);

        if (from == to)
            return quantity;

        if (fromDef.Dimension == toDef.Dimension)
        {
            var baseQty = quantity * fromDef.FactorToBase;
            var targetQty = baseQty / toDef.FactorToBase;

            // Invertibility check
            var invertedBase = targetQty * toDef.FactorToBase;
            var invertedOriginal = invertedBase / fromDef.FactorToBase;
            if (Math.Abs(invertedOriginal - quantity) > InvertibilityTolerance)
            {
                throw new ContradictoryUnitConversionException(
                    $"Invertibility failure: converting {quantity} {from} to {to} yields {targetQty}, but inverted yields {invertedOriginal}, difference exceeds tolerance {InvertibilityTolerance}.");
            }

            return targetQty;
        }

        if (_customConversions.TryGetValue((from, to), out var customFactor))
        {
            return quantity * customFactor;
        }

        throw new IncompatibleUnitDimensionException(from, fromDef.Dimension, to, toDef.Dimension);
    }
}
