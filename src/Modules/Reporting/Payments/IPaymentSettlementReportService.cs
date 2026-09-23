namespace ALKAROS.Reporting.Payments;

/// <summary>Assembles the full payment/cash/fiscal/meal-card settlement report (V13-RPT-001).</summary>
public interface IPaymentSettlementReportService
{
    Task<PaymentSettlementReportResult> GetReportAsync(
        PaymentSettlementReportFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Combines the real, buildable-today sections (payment mix, unsettled
/// payments, cash sessions, reconciliation totals) with three honestly
/// disabled sections whose real source is blocked per the 2026-09-23
/// per-edge dependency waiver (V13-GOV-008): net refunds (needs
/// V13-ALC-004's own refund-finalization/net-paid mutation — today's
/// <c>payments.refund_intents</c> only ever reaches Pending/Rejected, never
/// a completed, netted amount), fiscal status (needs V13-FSC-001, which has
/// not shipped any FiscalDocument schema at all yet), and meal-card closure
/// (needs V13-MCD-002's settlement-period grouping, and no meal-card
/// handler is registered in V13-PAY-003's registry today). None of these
/// three report a fabricated zero silently — each carries its own reason
/// and blocking task id, mirroring V13-REC-001's
/// <c>DisabledReconciliationSourcePair</c> from the same session.
/// </summary>
public sealed class PaymentSettlementReportService : IPaymentSettlementReportService
{
    private readonly IPaymentSettlementReportRepository _repository;

    public PaymentSettlementReportService(IPaymentSettlementReportRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PaymentSettlementReportResult> GetReportAsync(
        PaymentSettlementReportFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Validate();

        var (start, end) = filter.ResolveWindow();

        var paymentMix = await _repository.GetPaymentMixAsync(start, end, cancellationToken).ConfigureAwait(false);
        var unsettled = await _repository.GetUnsettledPaymentsAsync(start, end, cancellationToken).ConfigureAwait(false);
        var cashSessions = await _repository.GetCashSessionsAsync(start, end, filter.TerminalId, cancellationToken).ConfigureAwait(false);
        var reconciliationTotals = await _repository.GetReconciliationTotalsAsync(start, end, cancellationToken).ConfigureAwait(false);

        return new PaymentSettlementReportResult(
            filter.BusinessDate,
            paymentMix,
            unsettled,
            cashSessions,
            reconciliationTotals,
            NetRefunds: new DisabledReportSection(
                "NetRefunds",
                "Net geri ödeme tutarları hesaplanamıyor: payments.refund_intents yalnız Pending/Rejected talepleri kaydediyor, gerçek net-paid mutasyonu V13-ALC-004'ün kapsamında ve henüz yapılmadı.",
                "V13-ALC-004"),
            FiscalStatus: new DisabledReportSection(
                "FiscalStatus",
                "Mali durum raporlanamıyor: V13-FSC-001 henüz hiçbir FiscalDocument şeması üretmedi.",
                "V13-FSC-001"),
            MealCardClosure: new DisabledReportSection(
                "MealCardClosure",
                "Yemek kartı kapanış raporu üretilemiyor: V13-PAY-003'ün tender registry'sinde kayıtlı bir MealCard handler yok, V13-MCD-002 henüz yapılmadı.",
                "V13-MCD-002"));
    }
}
