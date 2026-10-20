using System.Collections.Concurrent;

namespace ALKAROS.Measurements;

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

    // V1-RMD-319 (independent 2026-09-26 audit, finding K7): tracks which (From, To) pairs were
    // registered DIRECTLY (a real caller registered exactly this direction), as opposed to only ever
    // appearing as the auto-computed reverse of some other direct registration. Needed so that
    // RE-registering the SAME direction with a genuinely NEW factor (a legitimate update - e.g. an admin
    // correcting "1 kasa = 12 adet" to "1 kasa = 24 adet") is allowed, while a truly contradictory
    // registration of the OPPOSITE direction (RegisterConversionContradictoryFactorThrowsContradictoryConversionException's
    // own scenario) still throws exactly as before - this call was added because the live "apply on
    // write" fix for K7 (RecipeManagementEndpoints' POST /unit-conversions) needed to call
    // RegisterConversion for a pair that may already be registered (a factor correction), and the
    // contradiction check below used to fire on ANY re-registration of an existing pair, direct or not.
    private readonly ConcurrentDictionary<(string From, string To), bool> _directlyRegisteredPairs =
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

        // A physical dimension (mass, volume) has one true factor; only Count units (case, pack, ...) have
        // no fixed ratio and take an operator-defined one.
        if (_units.TryGetValue(from, out var knownFrom) && _units.TryGetValue(to, out var knownTo)
            && knownFrom.Dimension == knownTo.Dimension && knownFrom.Dimension != UnitDimension.Count)
        {
            var physicalFactor = knownFrom.FactorToBase / knownTo.FactorToBase;
            if (Math.Abs(factor - physicalFactor) > InvertibilityTolerance)
                throw new ContradictoryUnitConversionException(
                    $"Cannot register conversion from '{from}' to '{to}' with factor {factor}: both are {knownFrom.Dimension} units and the standard factor is {physicalFactor}.");
            return;
        }

        // Validate contradictory conversion with inverse if already exists - UNLESS this exact (from, to)
        // direction was already directly registered before (a legitimate factor update/correction for the
        // same pair, not a contradiction with some other pair's own reverse).
        if (!_directlyRegisteredPairs.ContainsKey((from, to))
            && _customConversions.TryGetValue((to, from), out var existingInverse))
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
        _directlyRegisteredPairs[(from, to)] = true;
    }

    // The reverse of a registered rule divides by the registered factor instead of multiplying by its stored
    // 1/factor: 24 / 12 is exactly 2, 24 * 0.08333... is not.
    private bool TryApplyCustom(string from, string to, decimal quantity, out decimal result)
    {
        if (_directlyRegisteredPairs.ContainsKey((from, to)) && _customConversions.TryGetValue((from, to), out var factor))
        {
            result = quantity * factor;
            return true;
        }

        if (_directlyRegisteredPairs.ContainsKey((to, from)) && _customConversions.TryGetValue((to, from), out var reverseFactor))
        {
            result = quantity / reverseFactor;
            return true;
        }

        result = 0m;
        return false;
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

        // An operator-registered rule wins over the standard table: koli -> adet is 12, not the 1:1 of two Count units.
        if (TryApplyCustom(from, to, quantity, out result))
            return true;

        if (!_units.TryGetValue(from, out var fromDef) || !_units.TryGetValue(to, out var toDef))
            return false;

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

        // Cross-dimension without a custom bridge rule cannot be converted.

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

        if (from != to && TryApplyCustom(from, to, quantity, out var registered))
            return registered;

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

        throw new IncompatibleUnitDimensionException(from, fromDef.Dimension, to, toDef.Dimension);
    }
}
