namespace ALKAROS.Tables.Reservations;

/// <summary>
/// Data access contract for table reservation persistence and lifecycle state projection (V1-TBL-004, PDF:II.5.15, V0-DOM-005).
/// </summary>
public interface ITableReservationRepository
{
    /// <summary>
    /// Retrieves a single reservation record by ID.
    /// </summary>
    Task<TableReservationRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the active reservation for a given table, if any.
    /// </summary>
    Task<TableReservationRecord?> GetActiveByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a reservation associated with an order ID.
    /// </summary>
    Task<TableReservationRecord?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active reservations for a table.
    /// </summary>
    Task<IReadOnlyList<TableReservationRecord>> GetHistoryByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new reservation and atomically transitions the table from Available to Reserved.
    /// </summary>
    Task<TableReservationResult> CreateReservationAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims a reservation and atomically transitions the table from Reserved to Occupied.
    /// </summary>
    Task<TableReservationReleaseResult> ClaimReservationAsync(
        ClaimReservationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a reservation and atomically transitions the table from Reserved back to Available.
    /// </summary>
    Task<TableReservationReleaseResult> CancelReservationAsync(
        CancelReservationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires a reservation and atomically transitions the table from Reserved back to Available.
    /// </summary>
    Task<TableReservationReleaseResult> ExpireReservationAsync(
        ExpireReservationRequest request,
        CancellationToken cancellationToken = default);
}
