namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

public interface IKitchenItemStateProvider
{
    Task<KitchenItemPreparationStatus> GetItemPreparationStatusAsync(Guid orderItemId, CancellationToken ct = default);
}
