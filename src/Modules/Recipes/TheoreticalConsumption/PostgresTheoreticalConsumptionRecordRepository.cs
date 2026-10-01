using Npgsql;

namespace ALKAROS.Recipes.TheoreticalConsumption;

public sealed class PostgresTheoreticalConsumptionRecordRepository : ITheoreticalConsumptionRecordRepository
{
    private const int MaxUnpagedGroups = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresTheoreticalConsumptionRecordRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AppendAsync(
        TheoreticalConsumptionRecord record,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        const string sql = @"
            INSERT INTO recipe.theoretical_consumption_records (
                id, order_item_id, product_id, recipe_id, recipe_version_id,
                stock_item_id, quantity, unit_code, recorded_at, modifier_id
            ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10);";

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue(record.Id);
        cmd.Parameters.AddWithValue(record.OrderItemId);
        cmd.Parameters.AddWithValue(record.ProductId);
        cmd.Parameters.AddWithValue((object?)record.RecipeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)record.RecipeVersionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(record.StockItemId);
        cmd.Parameters.AddWithValue(record.Quantity);
        cmd.Parameters.AddWithValue(record.UnitCode);
        cmd.Parameters.AddWithValue(record.RecordedAt);
        cmd.Parameters.AddWithValue((object?)record.ModifierId ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<TheoreticalConsumptionTotal>> GetTotalsByStockItemAsync(
        DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT stock_item_id, unit_code, SUM(quantity) AS total_quantity
            FROM recipe.theoretical_consumption_records
            WHERE recorded_at >= $1 AND recorded_at < $2
            GROUP BY stock_item_id, unit_code
            ORDER BY stock_item_id
            LIMIT {MaxUnpagedGroups + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(periodStart);
        cmd.Parameters.AddWithValue(periodEnd);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<TheoreticalConsumptionTotal>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new TheoreticalConsumptionTotal(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2)));
        }

        if (list.Count > MaxUnpagedGroups)
        {
            throw new InvalidOperationException(
                $"GetTotalsByStockItemAsync returned more than {MaxUnpagedGroups} groups; narrow the period or paginate.");
        }

        return list;
    }
}
