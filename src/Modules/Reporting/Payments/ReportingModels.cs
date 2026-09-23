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

    /// <summary>The business date's [start, end) window in UTC, per <see cref="TimeZoneId"/>.</summary>
    public (DateTimeOffset Start, DateTimeOffset End) ResolveWindow()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var localStart = BusinessDate.ToDateTime(TimeOnly.MinValue);
        var start = new DateTimeOffset(localStart, zone.GetUtcOffset(localStart));
        var localEnd = BusinessDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var end = new DateTimeOffset(localEnd, zone.GetUtcOffset(localEnd));
        return (start, end);
    }
}

/// <summary>
/// One tender method's real totals for the reporting window (V0-DOM-008:
/// "SUM(allocated_amount) per method; methods sum to paid_amount"). The
/// method itself is not a stored column on <c>payments.payments</c> (no
/// schema in this codebase records it directly) — it is inferred from which
/// side-table a real handler wrote alongside the Payment: a matching
/// <c>cash.cash_transactions</c> Sale row means Cash (V13-CSH-002); a
/// matching <c>payments.card_settlement_attempts</c> row means BankCard
/// (V13-PAY-004); an Approved payment with an allocation but neither side
/// row means Eft (V13-PAY-005, which — like Cash and BankCard — has no
/// dedicated per-payment table of its own, so it is the only remaining
/// registered method by elimination; see <c>PaymentSettlementReportRepository
/// .PaymentMixSql</c>'s own comment for the exact join). This inference is
/// only correct as long as exactly Cash/BankCard/Eft are the registered
/// methods (V13-PAY-003) — MealCard has no handler registered today, so it
/// can never appear here; if MealCard is registered in the future, this
/// method-inference query needs a fourth real discriminator, not another
/// "by elimination" guess.
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
