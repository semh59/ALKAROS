namespace ALKAROS.IntegrationContracts;

/// <summary>
/// Stable wire names for the events that feed the daily menu counters. Inventory and Production
/// publish them in the transaction that changes their own rows; Menu applies them to its counters.
/// </summary>
public static class DailyMenuCounterIntegrationEventTypes
{
    public const string PortionStatusChanged = "inventory.daily-menu-portion-status-changed.v1";
    public const string PortionAdjusted = "inventory.daily-menu-portion-adjusted.v1";
    public const string ProductionOutputRecorded = "production.daily-menu-output-recorded.v1";
}

/// <summary>
/// A stock reservation that counts as portions of a daily menu item was created (<paramref name="Status"/>
/// <c>Reserved</c>) or reached a terminal state (<c>Released</c>, <c>Consumed</c>, <c>Waste</c>).
/// <paramref name="PortionCount"/> is the ordered quantity of the order item, not the stock quantity held.
/// </summary>
public sealed record DailyMenuPortionStatusChanged(
    Guid ReservationId,
    Guid DailyMenuItemId,
    decimal PortionCount,
    string Status,
    DateTimeOffset OccurredAt);

/// <summary>
/// Portions of an already <c>Consumed</c> reservation were given back (stock returned by a void or refund) or
/// wasted (the kitchen had started). Both amounts are cumulative for the reservation, so a repeated or late
/// delivery never counts twice; <paramref name="PortionCount"/> is the reservation's ordered portions.
/// </summary>
public sealed record DailyMenuPortionAdjusted(
    Guid ReservationId,
    Guid DailyMenuItemId,
    decimal PortionCount,
    decimal ReturnedPortions,
    decimal WastedPortions,
    DateTimeOffset OccurredAt);

/// <summary>A production batch linked to a daily menu item recorded <paramref name="Quantity"/> prepared portions.</summary>
public sealed record DailyMenuProductionOutputRecorded(
    Guid ProductionOutputId,
    Guid DailyMenuItemId,
    decimal Quantity,
    DateTimeOffset OccurredAt);
