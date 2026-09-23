using ALKAROS.Reconciliation.CaseFoundation;

namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// The result of scanning one <see cref="IReconciliationSourcePair"/> —
/// how many discrepancies it found (and turned into cases), or why it
/// could not run at all (disabled, or a genuine infrastructure failure).
/// </summary>
public sealed record SourceScanResult(
    string SourceName,
    bool WasEnabled,
    string? DisabledReason,
    int CasesCreatedOrDeduplicated,
    string? FailureReason);

/// <summary>
/// Runs every registered <see cref="IReconciliationSourcePair"/> and turns
/// each detected discrepancy into a deduplicated <see cref="ReconciliationCaseRecord"/>
/// via the already-Done <see cref="IReconciliationService"/> (V1-REC-001).
/// This is the ONLY path in the codebase that should ever call
/// <see cref="IReconciliationService.CreateOrDeduplicateCaseAsync"/> for a
/// payment-related discrepancy (V13-REC-001's own Acceptance evidence:
/// terminal-totals discrepancies must only ever produce a
/// ReconciliationCase through V13-REC-001's own API) — a future task
/// wiring in a currently-disabled source pair (Hugin terminal totals,
/// meal-card settlement, fiscal closure) must register its real
/// <see cref="IReconciliationSourcePair"/>
/// implementation here rather than calling
/// <see cref="IReconciliationService"/> directly.
///
/// One source pair's own failure (e.g. a transient database error specific
/// to its query) is caught and reported per-source, never aborting the
/// whole scan — the task's own Acceptance evidence requires that a
/// disabled/unavailable source never blocks the sources that ARE real
/// today.
/// </summary>
public sealed class PaymentReconciliationScanner
{
    private static readonly Guid SystemScanActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IReadOnlyList<IReconciliationSourcePair> _sourcePairs;
    private readonly IReconciliationService _reconciliationService;

    public PaymentReconciliationScanner(
        IReadOnlyList<IReconciliationSourcePair> sourcePairs,
        IReconciliationService reconciliationService)
    {
        _sourcePairs = sourcePairs ?? throw new ArgumentNullException(nameof(sourcePairs));
        _reconciliationService = reconciliationService ?? throw new ArgumentNullException(nameof(reconciliationService));
    }

    public async Task<IReadOnlyList<SourceScanResult>> ScanAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<SourceScanResult>(_sourcePairs.Count);

        foreach (var source in _sourcePairs)
        {
            if (!source.IsEnabled)
            {
                results.Add(new SourceScanResult(source.Name, WasEnabled: false, source.DisabledReason, 0, null));
                continue;
            }

            try
            {
                var discrepancies = await source.ScanAsync(cancellationToken).ConfigureAwait(false);
                var created = 0;
                foreach (var discrepancy in discrepancies)
                {
                    await _reconciliationService.CreateOrDeduplicateCaseAsync(
                        new CreateCaseRequest(
                            discrepancy.DeduplicationKey,
                            discrepancy.CaseType,
                            discrepancy.SourceARef,
                            discrepancy.SourceBRef,
                            discrepancy.DiscrepancyAmount,
                            discrepancy.Severity,
                            SystemScanActorId,
                            discrepancy.DetailsJson),
                        cancellationToken).ConfigureAwait(false);
                    created++;
                }

                results.Add(new SourceScanResult(source.Name, WasEnabled: true, null, created, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new SourceScanResult(source.Name, WasEnabled: true, null, 0, ex.Message));
            }
        }

        return results;
    }
}
