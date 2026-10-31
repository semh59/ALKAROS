using Npgsql;

namespace ALKAROS.Inventory.CrossChannelReservation;

/// <summary>
/// Lets an order's stock consumption respect every other order's holds. Order acceptance
/// consumes on-hand stock directly; without this check a cashier sale could take a portion
/// an online order already holds.
/// </summary>
public interface IReservationAwareConsumptionGuard
{
    /// <summary>
    /// Turns the order item's own active holds on this stock item into Consumed (their
    /// reserved quantity stops counting against availability, because the caller is about
    /// to consume the stock for real), then reports whether the remaining available quantity
    /// covers <paramref name="quantity"/>. The caller must already hold the stock row's
    /// on-hand lock in <paramref name="transaction"/>.
    /// </summary>
    Task<bool> ConvertOwnHoldsAndCheckAvailableAsync(
        Guid orderItemId,
        Guid stockItemId,
        Guid stockLocationId,
        decimal quantity,
        Guid actorId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a sale that consumed stock without a hold as consumed portions of its daily menu item: a
    /// <c>Consumed</c> reservation row carrying the portion keys, plus the counter event, in
    /// <paramref name="transaction"/>. Does nothing when the order item already has any reservation row (QR and
    /// online orders are counted from their hold), so an item is never counted twice.
    /// </summary>
    Task RecordDirectSaleAsync(
        DirectSale sale,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
