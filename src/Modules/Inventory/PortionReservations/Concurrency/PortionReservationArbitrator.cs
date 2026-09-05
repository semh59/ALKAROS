namespace ALKAROS.Inventory.PortionReservations.Concurrency;

public sealed class PortionReservationArbitrator : IPortionReservationArbitrator
{
    private readonly IPortionReservationArbitratorRepository _repository;

    public PortionReservationArbitrator(IPortionReservationArbitratorRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ReservationArbitrationResult> ArbitrateReservationAsync(
        ArbitrateReservationCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OrderId == Guid.Empty)
            throw new InvalidArbitrationCommandException("OrderId cannot be empty.");

        if (command.OrderItemId == Guid.Empty)
            throw new InvalidArbitrationCommandException("OrderItemId cannot be empty.");

        if (command.StockItemId == Guid.Empty)
            throw new InvalidArbitrationCommandException("StockItemId cannot be empty.");

        if (command.StockLocationId == Guid.Empty)
            throw new InvalidArbitrationCommandException("StockLocationId cannot be empty.");

        if (command.ActorId == Guid.Empty)
            throw new InvalidArbitrationCommandException("ActorId cannot be empty.");

        if (command.Quantity <= 0m)
            throw new InvalidArbitrationCommandException($"Reservation quantity must be strictly positive, got {command.Quantity}.");

        if (string.IsNullOrWhiteSpace(command.UnitCode))
            throw new InvalidArbitrationCommandException("UnitCode cannot be empty.");

        return await _repository.TryReserveAtomicAsync(command, ct);
    }
}
