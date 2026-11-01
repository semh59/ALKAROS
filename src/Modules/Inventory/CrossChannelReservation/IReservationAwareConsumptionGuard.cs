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

    /// <summary>
    /// Takes back the portions of a consumed order item whose stock was given back: the counted portions are returned up
    /// to <paramref name="returnedUnits"/> of <paramref name="totalUnits"/> in total (cumulative; repeating a call changes
    /// nothing), in <paramref name="transaction"/>. Does nothing for an item that was not counted.
    /// </summary>
    Task ReturnPortionsAsync(
        Guid orderItemId,
        decimal returnedUnits,
        decimal totalUnits,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the portions of a consumed order item that were not given back as waste, for an item the kitchen had
    /// already started when it was cancelled (its stock is not returned). Repeating the call changes nothing.
    /// </summary>
    Task WastePortionsAsync(
        Guid orderItemId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
