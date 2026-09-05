namespace ALKAROS.Inventory.MovementLedger;

public readonly record struct StockEffect(decimal OnHandDelta, decimal ReservedDelta, decimal AvailableDelta)
{
    public static StockEffect Calculate(StockMovementType movementType, MovementDirection direction, decimal quantity)
    {
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be strictly greater than zero.");

        return direction switch
        {
            MovementDirection.In => new StockEffect(
                OnHandDelta: quantity,
                ReservedDelta: 0m,
                AvailableDelta: quantity),

            MovementDirection.Out => new StockEffect(
                OnHandDelta: -quantity,
                ReservedDelta: 0m,
                AvailableDelta: -quantity),

            MovementDirection.Reserve => new StockEffect(
                OnHandDelta: 0m,
                ReservedDelta: quantity,
                AvailableDelta: -quantity),

            MovementDirection.Release => new StockEffect(
                OnHandDelta: 0m,
                ReservedDelta: -quantity,
                AvailableDelta: quantity),

            _ => throw new ArgumentOutOfRangeException(nameof(direction), $"Unsupported movement direction: {direction}")
        };
    }
}
