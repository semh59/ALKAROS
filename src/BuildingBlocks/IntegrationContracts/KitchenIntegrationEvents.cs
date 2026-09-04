namespace ALKAROS.IntegrationContracts;

/// <summary>
/// V1-KIT-005: stable wire names for Kitchen's integration events. Kept in a
/// sibling file to <see cref="IntegrationEventTypes"/> (Table Management's
/// registry) rather than added to it — each publisher owns its own contract
/// file (V0-ARC-001 §2).
/// </summary>
public static class KitchenIntegrationEventTypes
{
    public const string KitchenTicketItemStateChanged = "kitchen.ticket-item-state-changed.v1";
}

/// <summary>
/// A kitchen ticket item moved to a new state. Orders consumes this to mirror
/// the state onto its own <c>OrderItem.KitchenState</c> (V1-KIT-005) — only
/// published when a deployment has turned on kitchen live-sync (V1-SET-002).
/// <paramref name="ItemState"/> is one of Kitchen's own
/// <c>KitchenTicketItemState</c> names (<c>Queued</c>, <c>Preparing</c>,
/// <c>Ready</c>, <c>Served</c>, <c>Cancelled</c>) carried as a plain string
/// so this contract does not need a compile-time reference to Kitchen's own
/// enum type; the consumer maps <c>Queued</c> onto Orders' <c>Sent</c> name.
/// </summary>
public sealed record KitchenTicketItemStateChanged(
    Guid TicketId,
    Guid OrderId,
    Guid OrderItemId,
    string ItemState,
    DateTimeOffset OccurredAt);
