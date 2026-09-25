namespace ALKAROS.Inventory.CrossChannelReservation;

/// <summary>The sales channel an order entered through. Every channel competes for the same stock pool.</summary>
public enum ReservationChannel
{
    Cashier,
    Waiter,
    Qr,
    Online
}

public enum CrossChannelReservationOutcome
{
    /// <summary>Every line was held; the holds are written in the caller's transaction.</summary>
    Reserved,

    /// <summary>The same order already holds exactly these lines; nothing new was written.</summary>
    Replayed,

    /// <summary>At least one stock item lacks available quantity; nothing was written.</summary>
    OutOfStock,

    /// <summary>A product has no stock mapping, or a mapped stock item has no default location; nothing was written.</summary>
    NotConfigured
}

/// <summary>One order item competing for stock: the product sold and how many.</summary>
public sealed record CrossChannelReservationLine(Guid OrderItemId, Guid ProductId, decimal Quantity);

/// <summary>
/// The channel-neutral reservation command. <see cref="ChannelOrderReference"/> is the
/// channel's own identifier for the order (a provider order id, a QR submission id)
/// and is kept on each hold for traceability; <see cref="OrderId"/> is the identity
/// replays and compensation key on.
/// </summary>
public sealed record CrossChannelReservationRequest(
    ReservationChannel Channel,
    string ChannelOrderReference,
    Guid OrderId,
    Guid ActorId,
    IReadOnlyList<CrossChannelReservationLine> Lines);

/// <summary>A hold on one stock item for one order item.</summary>
public sealed record CrossChannelHold(
    Guid ReservationId,
    Guid OrderItemId,
    Guid StockItemId,
    Guid StockLocationId,
    decimal Quantity,
    string UnitCode,
    string Status);

/// <summary>
/// A stock item whose available quantity cannot cover what the whole order needs from it;
/// <see cref="OrderItemIds"/> are the lines that draw on it.
/// </summary>
public sealed record StockShortage(
    Guid StockItemId,
    Guid StockLocationId,
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    IReadOnlyList<Guid> OrderItemIds);

/// <summary>Why a line could not be resolved to stock.</summary>
public enum StockConfigurationGap
{
    ProductHasNoStockMapping,
    StockItemHasNoDefaultLocation
}

public sealed record UnconfiguredLine(Guid OrderItemId, Guid ProductId, StockConfigurationGap Gap);

public sealed record CrossChannelReservationResult(
    CrossChannelReservationOutcome Outcome,
    IReadOnlyList<CrossChannelHold> Holds,
    IReadOnlyList<StockShortage> Shortages,
    IReadOnlyList<UnconfiguredLine> UnconfiguredLines)
{
    public bool IsHeld => Outcome is CrossChannelReservationOutcome.Reserved or CrossChannelReservationOutcome.Replayed;

    internal static CrossChannelReservationResult Held(CrossChannelReservationOutcome outcome, IReadOnlyList<CrossChannelHold> holds) =>
        new(outcome, holds, Array.Empty<StockShortage>(), Array.Empty<UnconfiguredLine>());

    internal static CrossChannelReservationResult OutOfStock(IReadOnlyList<StockShortage> shortages) =>
        new(CrossChannelReservationOutcome.OutOfStock, Array.Empty<CrossChannelHold>(), shortages, Array.Empty<UnconfiguredLine>());

    internal static CrossChannelReservationResult NotConfigured(IReadOnlyList<UnconfiguredLine> lines) =>
        new(CrossChannelReservationOutcome.NotConfigured, Array.Empty<CrossChannelHold>(), Array.Empty<StockShortage>(), lines);
}

public sealed class InvalidCrossChannelReservationException : Exception
{
    public InvalidCrossChannelReservationException(string message) : base(message) { }
}

/// <summary>The order already holds stock, but for different lines than this request — a replay must repeat the original request exactly.</summary>
public sealed class CrossChannelReservationConflictException : Exception
{
    public CrossChannelReservationConflictException(Guid orderId)
        : base($"Order '{orderId}' already holds stock for a different set of lines.")
    {
        OrderId = orderId;
    }

    public Guid OrderId { get; }
}
