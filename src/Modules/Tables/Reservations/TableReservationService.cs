namespace ALKAROS.Tables.Reservations;

/// <summary>
/// Domain service interface for table reservation operations and lifecycle orchestration (V1-TBL-004).
/// </summary>
public interface ITableReservationService
{
    /// <summary>
    /// Retrieves a single reservation by ID — the GET counterpart clients
    /// need to recover a reservation's current row version (V1-RMD-117).
    /// </summary>
    Task<TableReservationRecord> GetByIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<TableReservationResult> CreateReservationAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<TableReservationReleaseResult> ClaimReservationAsync(
        ClaimReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<TableReservationReleaseResult> CancelReservationAsync(
        CancelReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<TableReservationReleaseResult> ExpireReservationAsync(
        ExpireReservationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain service implementation for table reservation lifecycle (V1-TBL-004).
/// </summary>
public sealed class TableReservationService : ITableReservationService
{
    private readonly ITableReservationRepository _repository;

    public TableReservationService(ITableReservationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TableReservationRecord> GetByIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        if (reservationId == Guid.Empty)
            throw new ArgumentException("Reservation ID cannot be empty.", nameof(reservationId));

        return await _repository.GetByIdAsync(reservationId, cancellationToken)
            ?? throw new ReservationNotFoundException(reservationId);
    }

    public Task<TableReservationResult> CreateReservationAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.CreateReservationAsync(request, cancellationToken);
    }

    public Task<TableReservationReleaseResult> ClaimReservationAsync(
        ClaimReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.ClaimReservationAsync(request, cancellationToken);
    }

    public Task<TableReservationReleaseResult> CancelReservationAsync(
        CancelReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.CancelReservationAsync(request, cancellationToken);
    }

    public Task<TableReservationReleaseResult> ExpireReservationAsync(
        ExpireReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.ExpireReservationAsync(request, cancellationToken);
    }
}
