using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using Npgsql;

namespace ALKAROS.Inventory.CrossChannelReservation;

/// <summary>
/// The one reservation command every channel (Cashier, Waiter, QR, Online) uses to claim
/// stock for an order, so the last portion goes to exactly one of them.
/// </summary>
public interface ICrossChannelPortionArbiter
{
    /// <summary>
    /// Holds stock for every line of <paramref name="request"/>, all-or-nothing, inside the
    /// caller's transaction — so the caller's own order write and the holds commit or roll
    /// back together. Returns a typed <see cref="CrossChannelReservationOutcome.OutOfStock"/>
    /// or <see cref="CrossChannelReservationOutcome.NotConfigured"/> result without writing
    /// anything when the order cannot be held.
    /// </summary>
    Task<CrossChannelReservationResult> ReserveAsync(
        CrossChannelReservationRequest request,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes an order's holds after the channel refused it (a provider rejecting the
    /// acceptance, a customer abandoning it). Each hold goes through the V11-RSV-003
    /// cancellation decision, which alone decides Release (kitchen not started) or Waste
    /// (preparation started); holds already Consumed are left alone. Safe to repeat. Runs in a
    /// transaction of its own.
    /// </summary>
    Task<IReadOnlyList<CancellationDecisionResult>> CompensateAsync(
        Guid orderId,
        Guid actorId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-310: the same compensation inside the caller's transaction, so it commits or rolls back with the
    /// caller's order change. The stock rows are locked in the same order <see cref="ReserveAsync"/> locks them.
    /// </summary>
    Task<IReadOnlyList<CancellationDecisionResult>> CompensateAsync(
        Guid orderId,
        Guid actorId,
        string reason,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
