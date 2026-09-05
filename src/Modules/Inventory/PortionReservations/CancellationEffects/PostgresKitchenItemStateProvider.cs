using Npgsql;

namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

public sealed class PostgresKitchenItemStateProvider : IKitchenItemStateProvider
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresKitchenItemStateProvider(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<KitchenItemPreparationStatus> GetItemPreparationStatusAsync(Guid orderItemId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT status
            FROM kitchen.kitchen_ticket_items
            WHERE order_item_id = $1
            ORDER BY created_at DESC
            LIMIT 1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(orderItemId);

        var statusObj = await cmd.ExecuteScalarAsync(ct);
        if (statusObj is not string statusStr)
        {
            return KitchenItemPreparationStatus.NotStarted;
        }

        return statusStr switch
        {
            "Queued" => KitchenItemPreparationStatus.NotStarted,
            "Preparing" => KitchenItemPreparationStatus.InProgress,
            "Ready" or "Served" => KitchenItemPreparationStatus.Completed,
            _ => KitchenItemPreparationStatus.NotStarted
        };
    }
}
