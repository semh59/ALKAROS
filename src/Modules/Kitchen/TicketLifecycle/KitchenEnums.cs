namespace ALKAROS.Kitchen.TicketLifecycle;

/// <summary>
/// Canonical kitchen ticket lifecycle states (PDF:I.16-I.20, PDF:II.5.7,
/// docs/domain/lifecycle-transition-contracts.md KitchenTicket row).
/// Database values must match exactly.
/// </summary>
public enum KitchenTicketState
{
    Queued,
    Accepted,
    Preparing,
    Ready,
    Cancelled,
}

/// <summary>
/// Canonical kitchen ticket item lifecycle states (PDF:I.16-I.20, PDF:II.5.8,
/// docs/domain/lifecycle-transition-contracts.md KitchenTicketItem row).
/// Database values must match exactly.
/// </summary>
public enum KitchenTicketItemState
{
    Queued,
    Preparing,
    Ready,
    Served,
    Cancelled,
}
