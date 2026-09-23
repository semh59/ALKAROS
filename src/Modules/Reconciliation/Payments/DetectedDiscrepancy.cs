using ALKAROS.Reconciliation.CaseFoundation;

namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// One discrepancy found by an <see cref="IReconciliationSourcePair"/> scan
/// (V13-REC-001) — a plain data record, not yet persisted. The scanner
/// (<see cref="PaymentReconciliationScanner"/>) turns each of these into a
/// <see cref="CreateCaseRequest"/> call against the already-Done
/// <see cref="IReconciliationService"/> (V1-REC-001), which itself owns
/// deduplication by <see cref="DeduplicationKey"/> — this record only needs
/// to compute a STABLE key per real-world discrepancy so re-running the
/// same scan twice never produces two cases for the same divergence.
/// </summary>
public sealed record DetectedDiscrepancy(
    string DeduplicationKey,
    CaseType CaseType,
    string SourceARef,
    string SourceBRef,
    decimal DiscrepancyAmount,
    CaseSeverity Severity,
    string? DetailsJson = null);
