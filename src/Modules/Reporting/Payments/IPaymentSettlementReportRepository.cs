namespace ALKAROS.Reporting.Payments;

/// <summary>Read-only queries backing the payment/cash/reconciliation settlement report (V13-RPT-001).</summary>
public interface IPaymentSettlementReportRepository
{
    Task<IReadOnlyList<PaymentMixEntry>> GetPaymentMixAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default);

    Task<UnsettledPaymentSummary> GetUnsettledPaymentsAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashSessionSummaryEntry>> GetCashSessionsAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, Guid? terminalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReconciliationTotalsEntry>> GetReconciliationTotalsAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default);
}
