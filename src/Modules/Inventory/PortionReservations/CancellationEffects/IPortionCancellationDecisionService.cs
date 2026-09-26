using Npgsql;

namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

public interface IPortionCancellationDecisionService
{
    /// <summary>
    /// Releases or wastes one reservation; each step commits on its own (waste movement before the reserved
    /// quantity, so available stock never rises). Use the overload below whenever a transaction is open.
    /// </summary>
    Task<CancellationDecisionResult> ProcessCancellationAsync(ProcessCancellationCommand command, CancellationToken ct = default);

    /// <summary>
    /// V1-RMD-310: releases or wastes one reservation inside the caller's transaction. Every effect (reservation
    /// status, reserved quantity, waste movement and on-hand) commits or rolls back with the caller's other
    /// writes, so no partial state is ever visible and nothing is left half-done.
    /// </summary>
    Task<CancellationDecisionResult> ProcessCancellationAsync(
        ProcessCancellationCommand command, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default);
}
