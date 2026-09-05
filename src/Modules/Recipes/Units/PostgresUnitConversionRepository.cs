using Npgsql;

namespace ALKAROS.Recipes.Units;

public sealed class PostgresUnitConversionRepository : IUnitConversionRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresUnitConversionRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AddConversionAsync(UnitConversion conversion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conversion);

        const string sql = @"
            INSERT INTO recipe.unit_conversions (unit_conversion_id, from_unit_code, to_unit_code, factor, active, created_at)
            VALUES ($1, $2, $3, $4, $5, $6)
            ON CONFLICT (from_unit_code, to_unit_code) DO UPDATE
            SET factor = EXCLUDED.factor,
                active = EXCLUDED.active;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(conversion.Id);
        cmd.Parameters.AddWithValue(conversion.FromUnitCode);
        cmd.Parameters.AddWithValue(conversion.ToUnitCode);
        cmd.Parameters.AddWithValue(conversion.Factor);
        cmd.Parameters.AddWithValue(conversion.Active);
        cmd.Parameters.AddWithValue(conversion.CreatedAt);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<UnitConversion>> GetActiveConversionsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT unit_conversion_id, from_unit_code, to_unit_code, factor, active, created_at
            FROM recipe.unit_conversions
            WHERE active = true
            ORDER BY from_unit_code, to_unit_code;";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<UnitConversion>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new UnitConversion(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetBoolean(4),
                reader.GetFieldValue<DateTimeOffset>(5)
            ));
        }

        return list;
    }

    public async Task<UnitConversion?> FindConversionAsync(string fromUnitCode, string toUnitCode, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT unit_conversion_id, from_unit_code, to_unit_code, factor, active, created_at
            FROM recipe.unit_conversions
            WHERE from_unit_code = $1 AND to_unit_code = $2 AND active = true;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(fromUnitCode.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue(toUnitCode.Trim().ToLowerInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new UnitConversion(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetBoolean(4),
                reader.GetFieldValue<DateTimeOffset>(5)
            );
        }

        return null;
    }
}
