namespace ALKAROS.Orders.Integration;

/// <summary>
/// V1-RMD-149: announces that an order is now waiting for a staff member to
/// confirm it. Orders owns the interface and knows nothing about how the
/// announcement travels — the same shape as
/// <see cref="ALKAROS.Orders.SubmitOrder.IOrderSubmissionDispatcher"/>, which
/// lets the module hand work to the Host without depending on it.
///
/// Before this existed a QR order reached PendingConfirmation and simply sat
/// there: the waiter hub carried a single event (an item being ready) and
/// only the kitchen published it, so a guest could order and nobody found
/// out.
/// </summary>
public interface IPendingOrderAnnouncer
{
    Task AnnounceAsync(PendingOrderAnnouncement announcement, CancellationToken cancellationToken = default);
}

/// <summary>
/// V1-RMD-149: everything a waiter device needs to draw the banner without
/// making a second call — which table, how many lines and how much.
/// </summary>
public sealed record PendingOrderAnnouncement(
    Guid OrderId,
    Guid? TableId,
    string TableNumber,
    int ItemCount,
    decimal Total,
    DateTimeOffset SubmittedAt);
