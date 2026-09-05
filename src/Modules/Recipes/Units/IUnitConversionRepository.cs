namespace ALKAROS.Recipes.Units;

public interface IUnitConversionRepository
{
    Task AddConversionAsync(UnitConversion conversion, CancellationToken ct = default);
    Task<IReadOnlyList<UnitConversion>> GetActiveConversionsAsync(CancellationToken ct = default);
    Task<UnitConversion?> FindConversionAsync(string fromUnitCode, string toUnitCode, CancellationToken ct = default);
}
