namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

public interface IPortionCancellationDecisionService
{
    Task<CancellationDecisionResult> ProcessCancellationAsync(ProcessCancellationCommand command, CancellationToken ct = default);
}
