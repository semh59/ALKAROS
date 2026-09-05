using ALKAROS.Inventory.StockMaster;

namespace ALKAROS.Inventory.PortionReservations.Lifecycle;

public sealed class PortionReservationLifecycleService : IPortionReservationLifecycleService
{
    private readonly IPortionReservationRepository _repository;
    private readonly IStockItemRepository _itemRepo;
    private readonly IStockLocationRepository _locationRepo;

    public PortionReservationLifecycleService(
        IPortionReservationRepository repository,
        IStockItemRepository itemRepo,
        IStockLocationRepository locationRepo)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
    }

    public async Task<ReservationTransitionResult> CreateReservationAsync(
        CreateReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OrderId == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty.", nameof(command));

        if (command.OrderItemId == Guid.Empty)
            throw new ArgumentException("OrderItemId cannot be empty.", nameof(command));

        if (command.StockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(command));

        if (command.StockLocationId == Guid.Empty)
            throw new ArgumentException("StockLocationId cannot be empty.", nameof(command));

        if (command.Quantity <= 0m)
            throw new InvalidPortionReservationQuantityException($"Reservation quantity must be strictly positive, got {command.Quantity}.");

        if (string.IsNullOrWhiteSpace(command.UnitCode))
            throw new ArgumentException("UnitCode cannot be empty.", nameof(command));

        if (command.CreatedBy == Guid.Empty)
            throw new UnauthorizedReservationActorException("CreatedBy cannot be empty.");

        // Idempotency check on creation
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existing = await _repository.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
            if (existing != null)
            {
                return new ReservationTransitionResult(existing, IsIdempotentReplay: true);
            }
        }

        var item = await _itemRepo.GetByIdAsync(command.StockItemId, cancellationToken)
            ?? throw new ArgumentException($"Stock item '{command.StockItemId}' was not found.", nameof(command));
        item.EnsureActiveForMovement();

        var location = await _locationRepo.GetByIdAsync(command.StockLocationId, cancellationToken)
            ?? throw new ArgumentException($"Stock location '{command.StockLocationId}' was not found.", nameof(command));
        location.EnsureActiveForMovement();

        var reservation = PortionReservation.Create(
            orderId: command.OrderId,
            orderItemId: command.OrderItemId,
            stockItemId: command.StockItemId,
            stockLocationId: command.StockLocationId,
            quantity: command.Quantity,
            unitCode: command.UnitCode,
            createdBy: command.CreatedBy,
            idempotencyKey: command.IdempotencyKey,
            metadataJson: command.MetadataJson,
            id: command.Id);

        await _repository.InsertAsync(reservation, cancellationToken);

        return new ReservationTransitionResult(reservation, IsIdempotentReplay: false);
    }

    public Task<ReservationTransitionResult> ReleaseReservationAsync(
        TransitionReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteTransitionAsync(command with { TargetStatus = PortionReservationStatus.Released }, cancellationToken);
    }

    public Task<ReservationTransitionResult> ConsumeReservationAsync(
        TransitionReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteTransitionAsync(command with { TargetStatus = PortionReservationStatus.Consumed }, cancellationToken);
    }

    public Task<ReservationTransitionResult> WasteReservationAsync(
        TransitionReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteTransitionAsync(command with { TargetStatus = PortionReservationStatus.Waste }, cancellationToken);
    }

    public Task<PortionReservation?> GetReservationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    private async Task<ReservationTransitionResult> ExecuteTransitionAsync(
        TransitionReservationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ReservationId == Guid.Empty)
            throw new ArgumentException("ReservationId cannot be empty.", nameof(command));

        if (command.TransitionedBy == Guid.Empty)
            throw new UnauthorizedReservationActorException("TransitionedBy cannot be empty.");

        var reservation = await _repository.GetByIdAsync(command.ReservationId, cancellationToken)
            ?? throw new PortionReservationNotFoundException(command.ReservationId);

        // Idempotency: if already in the target terminal state
        if (reservation.Status == command.TargetStatus)
        {
            return new ReservationTransitionResult(reservation, IsIdempotentReplay: true);
        }

        // Terminal state conflict check: once in Released, Consumed, or Waste, cannot transition to another terminal status
        if (reservation.Status != PortionReservationStatus.Reserved)
        {
            throw new InvalidPortionReservationTransitionException(
                $"Cannot transition reservation '{reservation.Id}' from terminal status '{reservation.Status}' to '{command.TargetStatus}'.");
        }

        var expectedVersion = reservation.Version;
        reservation.TransitionTo(command.TargetStatus, command.TransitionedBy, command.Reason);

        var success = await _repository.UpdateStatusOptimisticAsync(reservation, expectedVersion, cancellationToken);
        if (!success)
        {
            // Concurrent race condition detected: reload current state
            var current = await _repository.GetByIdAsync(command.ReservationId, cancellationToken)
                ?? throw new PortionReservationNotFoundException(command.ReservationId);

            if (current.Status == command.TargetStatus)
            {
                // Another concurrent worker transitioned to the same status
                return new ReservationTransitionResult(current, IsIdempotentReplay: true);
            }

            // Another worker won the race to a different terminal status
            throw new PortionReservationConflictException(
                $"Concurrent race conflict on reservation '{command.ReservationId}': could not transition to '{command.TargetStatus}' because it was transitioned to '{current.Status}' by another process.");
        }

        return new ReservationTransitionResult(reservation, IsIdempotentReplay: false);
    }
}
