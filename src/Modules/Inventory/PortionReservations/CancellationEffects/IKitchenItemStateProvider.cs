using Npgsql;

namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

public interface IKitchenItemStateProvider
{
    Task<KitchenItemPreparationStatus> GetItemPreparationStatusAsync(Guid orderItemId, CancellationToken ct = default);

    /// <summary>
    /// V1-RMD-310: reads the kitchen status inside the caller's transaction (falls back for fakes). The kitchen
    /// row is not locked yet: the caller still saves the cancelled kitchen ticket on its own connection, which a
    /// share lock here would block. Locking it belongs to the task that moves that save into the transaction.
    /// </summary>
    Task<KitchenItemPreparationStatus> GetItemPreparationStatusAsync(
        Guid orderItemId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
        => GetItemPreparationStatusAsync(orderItemId, ct);
}
