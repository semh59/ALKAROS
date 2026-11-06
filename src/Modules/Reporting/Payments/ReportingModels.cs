using ALKAROS.Reporting.BusinessDayTotals;

namespace ALKAROS.Reporting.Payments;

/// <summary>
/// Filters a payment/cash/reconciliation settlement report to one business
/// day (V0-DOM-008's own time-zone/business-date requirement — a business
/// date is a fixed local-time window, converted to UTC internally, never a
/// bare calendar-day UTC cut). An optional <see cref="TerminalId"/> narrows
/// only the Cash section (the only section with a real terminal linkage
/// today — see <see cref="PaymentMixEntry"/>'s own remarks).
/// </summary>
public sealed record PaymentSettlementReportFilter(
    DateOnly BusinessDate,
    Guid? TerminalId = null,
    string TimeZoneId = "Europe/Istanbul")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId))
            throw new ArgumentException("Time zone id cannot be empty.", nameof(TimeZoneId));
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new ArgumentException($"Unknown time zone id '{TimeZoneId}'.", nameof(TimeZoneId), ex);
        }
    }

    /// <summary>
    /// The business date's [start, end) window in UTC, per <see cref="TimeZoneId"/>. A hospitality business day does
    /// not end at midnight: a table still being served at 01:00 belongs to the day the restaurant opened for, so the
    /// window runs from 06:00 to the next day's 06:00 local time.
    /// </summary>
    public (DateTimeOffset Start, DateTimeOffset End) ResolveWindow()
        => BusinessDayWindow.Of(BusinessDate, TimeZoneId);
}

/// <summary>
/// One tender method's real totals for the reporting window (the sum of
/// the allocated amounts per method; methods sum to the paid amount). The
/// method is not a stored column on <c>payments.payments</c>; it is read from
/// the records each way leaves behind: <c>CustomerAccount</c> (an account
/// charge), <c>Cash</c> (a cash sale), <c>BankCard</c> (an approved card
/// attempt or an approved manual card confirmation) and <c>Eft</c> (what is
/// left, because the producers of approved payments are a closed set; see
/// <c>PostgresPaymentSettlementReportRepository.MethodSql</c>). A meal card
/// has no handler today; registering one needs a fifth real discriminator,
/// not another "by elimination" guess.
/// </summary>
public sealed record PaymentMixEntry(
    string Method,
    int ApprovedCount,
    decimal ApprovedAmount);

/// <summary>
/// Payments that never reached Approved/Declined for the window — shown as
/// their own bucket, never folded into any method's approved total (task's
/// own Acceptance evidence requires Unknown/ReconciliationRequired to stay
/// separately visible, never silently merged elsewhere).
/// </summary>
public sealed record UnsettledPaymentSummary(
    int UnknownCount,
    decimal UnknownAmount,
    int ReconciliationRequiredCount,
    decimal ReconciliationRequiredAmount);

/// <summary>One cash session overlapping the reporting window (V0-DOM-008's cash-report formula).</summary>
public sealed record CashSessionSummaryEntry(
    Guid CashSessionId,
    Guid TerminalId,
    string Status,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal Difference,
    bool IsOpen);

/// <summary>Open (unresolved) and resolved reconciliation case totals for the window, grouped by case type.</summary>
public sealed record ReconciliationTotalsEntry(
    string CaseType,
    int OpenCount,
    int ResolvedCount,
    decimal OpenDiscrepancyAmount);

/// <summary>
/// A report section this task cannot populate with real data today because
/// its source dependency is blocked/planned, not because the feature was
/// forgotten (V13-GOV-008's per-edge dependency waiver covers exactly this
/// task depending on <c>V13-ALC-004</c>/<c>V13-MCD-002</c>/<c>V13-FSC-001</c>).
/// Mirrors <c>ALKAROS.Reconciliation.Payments.DisabledReconciliationSourcePair</c>
/// (V13-REC-001, same session) — a section reports itself honestly instead
/// of being silently absent or throwing.
/// </summary>
public sealed record DisabledReportSection(string SectionName, string Reason, string BlockedBy);

/// <summary>The full payment/cash/fiscal/meal-card settlement report for one business date.</summary>
public sealed record PaymentSettlementReportResult(
    DateOnly BusinessDate,
    IReadOnlyList<PaymentMixEntry> PaymentMix,
    UnsettledPaymentSummary UnsettledPayments,
    IReadOnlyList<CashSessionSummaryEntry> CashSessions,
    IReadOnlyList<ReconciliationTotalsEntry> ReconciliationTotals,
    DisabledReportSection NetRefunds,
    DisabledReportSection FiscalStatus,
    DisabledReportSection MealCardClosure);
