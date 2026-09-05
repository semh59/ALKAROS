namespace ALKAROS.Recipes.Units;

/// <summary>
/// Represents a direct conversion rule between two units.
/// </summary>
public sealed class UnitConversion
{
    public Guid Id { get; }
    public string FromUnitCode { get; }
    public string ToUnitCode { get; }
    public decimal Factor { get; }
    public bool Active { get; }
    public DateTimeOffset CreatedAt { get; }

    public UnitConversion(Guid id, string fromUnitCode, string toUnitCode, decimal factor, bool active = true, DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(fromUnitCode))
            throw new ArgumentException("From unit code cannot be empty.", nameof(fromUnitCode));
        if (string.IsNullOrWhiteSpace(toUnitCode))
            throw new ArgumentException("To unit code cannot be empty.", nameof(toUnitCode));
        if (factor <= 0m)
            throw new InvalidUnitConversionFactorException("Conversion factor must be strictly positive.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        FromUnitCode = fromUnitCode.Trim().ToLowerInvariant();
        ToUnitCode = toUnitCode.Trim().ToLowerInvariant();
        Factor = factor;
        Active = active;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }
}
