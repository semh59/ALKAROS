using ALKAROS.Observability.AlertFoundation;

namespace ALKAROS.Observability.ErrorEvents;

/// <summary>
/// Turns a new or more severe error into one alert in the existing alert system; the fingerprint is the alert's
/// deduplication key, so a repeating error never opens a second alert.
/// </summary>
public sealed class AlertErrorEventSink(IAlertService alerts) : IErrorEventChangeSink
{
    public const string AlertType = "error_event";

    public async Task OnChangesAsync(IReadOnlyList<ErrorEventChange> changes, CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            if (change.Severity < ErrorSeverity.Error)
                continue;

            var severity = change.Severity == ErrorSeverity.Critical ? AlertSeverity.Critical : AlertSeverity.Warning;
            await alerts.RaiseAlertAsync(
                new RaiseAlertRequest(
                    AlertType,
                    severity,
                    $"Hata: {change.Category}",
                    change.Detail ?? change.Category,
                    DeduplicationKey: $"error:{change.Fingerprint}"),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
