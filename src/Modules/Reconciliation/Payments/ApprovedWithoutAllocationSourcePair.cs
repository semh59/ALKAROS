using ALKAROS.Reconciliation.CaseFoundation;
using Npgsql;

namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// "Approved-without-allocation" source pair (V13-REC-001 In scope):
/// compares <c>payments.payments</c> (Payment aggregate, V13-PAY-001,
/// Done) against <c>payments.payment_allocations</c> (V13-ALC-001, Done) —
/// every <c>Approved</c> Payment must have exactly one allocation row
/// recorded against it (V0-DOM-004); a Payment that reached Approved with
/// none is a real divergence between the two authoritative sources, fully
/// detectable today with no external dependency at all. Read-only plain
/// SQL against the other modules' own tables (the established "read model"
/// pattern this codebase already uses, e.g. Tables' own
/// <c>CurrentOrderTotal</c> read) rather than a cross-module C# reference,
/// so this task's Owned surface never needs to touch
/// <c>IPaymentRepository</c>/<c>IPaymentAllocationRepository</c>.
/// </summary>
public sealed class ApprovedWithoutAllocationSourcePair : IReconciliationSourcePair
{
    // Same bound + fail-loud-on-overflow discipline as
    // PostgresReconciliationRepository.GetCaseActionsAsync's own
    // MaxUnpagedRows: an outgrown table (or a widening filter bug) must
    // surface as a loud error, never a silently truncated scan.
    private const int MaxScanRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public ApprovedWithoutAllocationSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => "ApprovedWithoutAllocation";
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectedDiscrepancy>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT p.payment_id, p.approved_amount, p.approved_at
            FROM payments.payments p
            WHERE p.status = 'Approved'
              AND NOT EXISTS (
                  SELECT 1 FROM payments.payment_allocations pa WHERE pa.payment_id = p.payment_id
              )
            LIMIT {MaxScanRows + 1};
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var paymentId = reader.GetGuid(0);
            var approvedAmount = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
            var approvedAt = reader.IsDBNull(2) ? (DateTimeOffset?)null : new DateTimeOffset(reader.GetDateTime(2));

            results.Add(new DetectedDiscrepancy(
                DeduplicationKey: $"approved-without-allocation:{paymentId}",
                CaseType: CaseType.PaymentMismatch,
                SourceARef: $"payments.payments:{paymentId}",
                SourceBRef: "payments.payment_allocations:none",
                DiscrepancyAmount: approvedAmount,
                Severity: CaseSeverity.High,
                DetailsJson: $$"""{"paymentId":"{{paymentId}}","approvedAmount":{{approvedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"approvedAt":"{{approvedAt:O}}"}"""));
        }

        if (results.Count > MaxScanRows)
            throw new InvalidOperationException(
                $"ApprovedWithoutAllocation scan returned more than {MaxScanRows} rows; narrow the filter or paginate.");

        return results;
    }
}
