using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// V12-REC-001: runs every online order source pair and turns each divergence into a reconciliation case
/// through the V1-REC-001 service, which owns deduplication: an open case for the same divergence is
/// reused, never duplicated, however often or concurrently the scan runs. One source's failure is reported
/// for that source alone and never stops the others.
/// </summary>
public sealed class OnlineOrderReconciliationScanner
{
    /// <summary>The system actor a scan-created case is attributed to (the same one V13-REC-001 uses).</summary>
    public static readonly Guid SystemScanActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Shown instead of an infrastructure message, which may be English and technical.</summary>
    public const string SourceUnreadableReason = "Kaynak okunamadı.";

    private readonly IReadOnlyList<IOnlineOrderSourcePair> _sourcePairs;
    private readonly IReconciliationService _reconciliationService;

    public OnlineOrderReconciliationScanner(
        IReadOnlyList<IOnlineOrderSourcePair> sourcePairs,
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
            try
            {
                var discrepancies = await source.ScanAsync(cancellationToken).ConfigureAwait(false);
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
                }

                results.Add(new SourceScanResult(source.Name, WasEnabled: true, null, discrepancies.Count, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new SourceScanResult(source.Name, WasEnabled: true, null, 0, SourceUnreadableReason));
            }
        }

        return results;
    }
}
