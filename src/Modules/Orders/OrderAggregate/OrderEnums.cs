namespace ALKAROS.Orders.OrderAggregate;

/// <summary>
/// Canonical order lifecycle states (PDF:II.5.1 / canonical-value-catalog.md,
/// docs/domain/lifecycle-transition-contracts.md Order row). Database values
/// must match exactly (PDF:II.5).
/// </summary>
public enum OrderState
{
    Draft,
    Submitted,
    PendingConfirmation,
    Accepted,
    Rejected,
    Preparing,
    Ready,
    Served,
    Completed,
    Cancelled,
}

/// <summary>
/// Canonical order source (PDF:III.6.1, canonical-value-catalog.md).
/// </summary>
public enum OrderSource
{
    Cashier,
    Waiter,
    Qr,
    Online,
}

/// <summary>
/// Open/closed status of a single path on the confirmation flow (PDF:III.6.1
/// 'confirmation_status not null'). Value set is scope-owned by V1-ORD-001;
/// the QR channel (V14-QRO-001/002) reconciles its PendingConfirmation
/// workflow against these values.
/// </summary>
public enum ConfirmationStatus
{
    NotRequired,
    Pending,
    Accepted,
    Rejected,
}

/// <summary>
/// Canonical order item lifecycle state (PDF:III.6.2,
/// canonical-value-catalog.md).
/// </summary>
public enum OrderItemState
{
    Draft,
    Active,
    Cancelled,
    Waste,
    Complimentary,
}

/// <summary>
/// Canonical order item kitchen state (PDF:III.6.2,
/// canonical-value-catalog.md).
/// </summary>
public enum KitchenState
{
    NotSent,
    Sent,
    Preparing,
    Ready,
    Served,
    Cancelled,
}

/// <summary>
/// Canonical portion reservation status mirror (PDF:III.6.2 CORR:C2,
/// canonical-value-catalog.md). A denormalized read-model mirror of the most
/// recent inventory.portion_reservations.status row, never written
/// independently (PDF:III.6.2 normative).
/// </summary>
public enum PortionReservationStatus
{
    NotApplicable,
    NotReserved,
    Reserved,
    Released,
    Consumed,
    Waste,
}