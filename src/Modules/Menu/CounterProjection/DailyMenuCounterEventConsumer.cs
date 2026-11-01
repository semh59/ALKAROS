using ALKAROS.IntegrationContracts;

namespace ALKAROS.Menu.CounterProjection;

/// <summary>
/// Applies Inventory's reservation events and Production's output events to the daily menu counters.
/// Delivery is at-least-once and unordered, so a status event always applies the reservation first (idempotent
/// per reservation id) and then its terminal state; a late or repeated delivery changes nothing.
/// </summary>
public sealed class DailyMenuCounterEventConsumer : IIntegrationEventConsumer
{
    private readonly IDailyMenuCounterProjector _projector;

    public DailyMenuCounterEventConsumer(IDailyMenuCounterProjector projector)
    {
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    public bool CanHandle(string eventType) =>
        eventType is DailyMenuCounterIntegrationEventTypes.PortionStatusChanged
            or DailyMenuCounterIntegrationEventTypes.PortionAdjusted
            or DailyMenuCounterIntegrationEventTypes.ProductionOutputRecorded;

    public async Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (eventType == DailyMenuCounterIntegrationEventTypes.ProductionOutputRecorded)
        {
            var output = IntegrationEventSerializer.Deserialize<DailyMenuProductionOutputRecorded>(payload.Span);
            await _projector.ApplyProductionOutputAsync(
                new ProductionOutputCounterEvent(output.DailyMenuItemId, output.ProductionOutputId, output.Quantity, output.OccurredAt),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (eventType == DailyMenuCounterIntegrationEventTypes.PortionAdjusted)
        {
            var adjusted = IntegrationEventSerializer.Deserialize<DailyMenuPortionAdjusted>(payload.Span);
            // An adjustment can overtake the reservation's own events; the adjustment is only meaningful on top of its consumption.
            await _projector.ApplyReservationReservedAsync(
                new ReservationReservedCounterEvent(adjusted.DailyMenuItemId, adjusted.ReservationId, adjusted.PortionCount, adjusted.OccurredAt),
                cancellationToken).ConfigureAwait(false);
            await _projector.ApplyReservationTerminalAsync(
                new ReservationTerminalCounterEvent(
                    adjusted.DailyMenuItemId,
                    adjusted.ReservationId,
                    ReservationCounterTerminalStatus.Consumed,
                    adjusted.PortionCount,
                    adjusted.OccurredAt),
                cancellationToken).ConfigureAwait(false);
            await _projector.ApplyReservationAdjustmentAsync(
                new ReservationAdjustmentCounterEvent(
                    adjusted.DailyMenuItemId, adjusted.ReservationId, adjusted.ReturnedPortions, adjusted.WastedPortions, adjusted.OccurredAt),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var change = IntegrationEventSerializer.Deserialize<DailyMenuPortionStatusChanged>(payload.Span);
        await _projector.ApplyReservationReservedAsync(
            new ReservationReservedCounterEvent(change.DailyMenuItemId, change.ReservationId, change.PortionCount, change.OccurredAt),
            cancellationToken).ConfigureAwait(false);
        if (change.Status == "Reserved")
            return;

        await _projector.ApplyReservationTerminalAsync(
            new ReservationTerminalCounterEvent(
                change.DailyMenuItemId,
                change.ReservationId,
                Enum.Parse<ReservationCounterTerminalStatus>(change.Status),
                change.PortionCount,
                change.OccurredAt),
            cancellationToken).ConfigureAwait(false);
    }
}
