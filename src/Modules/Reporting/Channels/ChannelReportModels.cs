namespace ALKAROS.Reporting.Channels;

/// <summary>
/// Filters the V12-RPT-001 channel report to a range of business dates. A business date is the
/// Europe/Istanbul service day (V0-DOM-008 cross-cutting rule 3, V0-CMP-002), converted to a UTC window
/// internally, never a bare UTC calendar day. <see cref="Source"/> narrows to one channel.
/// </summary>
public sealed record ChannelReportFilter(
    DateOnly From,
    DateOnly To,
    string? Source = null,
    string TimeZoneId = "Europe/Istanbul")
{
    public const int MaxDays = 31;

    public static readonly IReadOnlyList<string> Channels = [ChannelSource.Qr, ChannelSource.Online];

    public void Validate()
    {
        if (To < From)
            throw new ArgumentException("The end date cannot be before the start date.", nameof(To));
        if (To.DayNumber - From.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"A channel report covers at most {MaxDays} business dates.", nameof(To));
        if (Source is not null && !Channels.Contains(Source))
            throw new ArgumentException("Source must be Qr or Online.", nameof(Source));
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new ArgumentException($"Unknown time zone id '{TimeZoneId}'.", nameof(TimeZoneId), ex);
        }
    }

    /// <summary>The range's [start, end) window in UTC.</summary>
    public (DateTimeOffset Start, DateTimeOffset End) ResolveWindow()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var localStart = From.ToDateTime(TimeOnly.MinValue);
        var localEnd = To.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return (new DateTimeOffset(localStart, zone.GetUtcOffset(localStart)),
                new DateTimeOffset(localEnd, zone.GetUtcOffset(localEnd)));
    }
}

public static class ChannelSource
{
    public const string Qr = "Qr";
    public const string Online = "Online";
}

/// <summary>
/// One channel on one business date. Every order counts in exactly one status bucket, so
/// <c>AwaitingConfirmation + Accepted + Rejected + Cancelled = OrdersReceived</c>.
/// <see cref="ProviderRefused"/> is the online orders the provider sent that never became a local
/// order (counted once per provider order however many webhooks carried it); it is not part of
/// <see cref="OrdersReceived"/>, which counts local orders only.
/// </summary>
public sealed record ChannelDayRow(
    DateOnly BusinessDate,
    string Source,
    int OrdersReceived,
    int AwaitingConfirmation,
    int Accepted,
    int Rejected,
    int Cancelled,
    decimal AcceptedValue,
    decimal CancelledValue,
    int ProviderRefused);

/// <summary>
/// The reconciliation difference for online orders (V0-DOM-008 "Reconciliation backlog": open cases,
/// classified by divergence source) for the cases opened on one business date.
/// </summary>
public sealed record ChannelReconciliationRow(
    DateOnly BusinessDate,
    string Kind,
    int OpenCases,
    int ClosedCases,
    decimal OpenDiscrepancyAmount);

/// <summary>
/// The report's own reconciliation total (V0-DOM-008 cross-cutting rule 2), read from the order ledger
/// independently of the per-day rows. <see cref="IsBalanced"/> false is a bug, never a report feature.
/// </summary>
public sealed record ChannelReportCheck(
    int LedgerOrderCount,
    decimal LedgerAcceptedValue,
    int ReportedOrderCount,
    decimal ReportedAcceptedValue)
{
    public bool IsBalanced => LedgerOrderCount == ReportedOrderCount && LedgerAcceptedValue == ReportedAcceptedValue;
}

public sealed record ChannelReport(
    string ReportVersion,
    DateOnly From,
    DateOnly To,
    string? Source,
    string TimeZoneId,
    IReadOnlyList<ChannelDayRow> Days,
    IReadOnlyList<ChannelReconciliationRow> Reconciliation,
    int RetryAttempts,
    ChannelReportCheck Check);
