using ALKAROS.Reconciliation.CaseFoundation;
using Npgsql;

namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// "Hugin Unknown" source pair (V13-REC-001 In scope) — a Payment sitting
/// at <c>Unknown</c>/<c>ReconciliationRequired</c> (V13-PAY-001's own
/// transition matrix: a provider timeout, never an implicit approval or
/// decline, CORR:C29). Detectable today with zero dependency on the real
/// Hugin/Token terminal (V13-HUG-001): V13-PAY-003's own BankCard
/// placeholder handler (<c>PendingBankCardTerminalIntegrationHandler</c>)
/// deliberately, honestly produces exactly this Payment state for every
/// real card attempt today, precisely so it is visible here rather than
/// silently disappearing. What is NOT built here (deliberately, matching
/// this task's own Out-of-scope for provider transport) is any enrichment
/// against the real terminal's own basket/transaction state — that needs
/// V13-HUG-002 (unknown-transaction reconciliation) once the real terminal
/// exists; until then, this source pair only reports the fact that a
/// Payment is stuck, which is itself real, actionable information for a
/// human reconciler today.
/// </summary>
public sealed class PaymentUnknownSourcePair : IReconciliationSourcePair
{
    // See ApprovedWithoutAllocationSourcePair's own comment on this bound.
    private const int MaxScanRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PaymentUnknownSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => "HuginUnknown";
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectedDiscrepancy>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT p.payment_id, p.bill_id, p.tendered_amount
            FROM payments.payments p
            WHERE p.status IN ('Unknown', 'ReconciliationRequired')
            LIMIT {MaxScanRows + 1};
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var paymentId = reader.GetGuid(0);
            var billId = reader.GetGuid(1);
            var tenderedAmount = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);

            results.Add(new DetectedDiscrepancy(
                DeduplicationKey: $"hugin-unknown:{paymentId}",
                CaseType: CaseType.PaymentMismatch,
                SourceARef: $"payments.payments:{paymentId}",
                SourceBRef: $"billing.bills:{billId}",
                DiscrepancyAmount: tenderedAmount,
                Severity: CaseSeverity.Critical,
                DetailsJson: $$"""{"paymentId":"{{paymentId}}","billId":"{{billId}}","tenderedAmount":{{tenderedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""));
        }

        if (results.Count > MaxScanRows)
            throw new InvalidOperationException(
                $"HuginUnknown scan returned more than {MaxScanRows} rows; narrow the filter or paginate.");

        return results;
    }
}
