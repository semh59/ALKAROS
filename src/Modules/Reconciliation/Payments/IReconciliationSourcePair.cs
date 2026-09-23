namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// One named "source pair" this task's Goal describes (produce deduplicated
/// ReconciliationCase records whenever v1.3's authoritative sources
/// diverge) — two authoritative sources of truth that should always agree,
/// and a scan that reports where they don't.
///
/// Some source pairs cannot be built for real today because one of their
/// two sides is a task Semih has separately, explicitly forbidden from
/// getting even a stub or schema (`plan/GATES.md`'s `V13_EXIT_ENTRY_WAIVER`
/// table, applied to this task's own dependencies via
/// `V13_PAYMENT_ORCHESTRATION_DEPENDENCY_WAIVER`: Hugin/Token terminal
/// integration, meal-card provider adapters, and the fiscal document
/// lifecycle). Those
/// source pairs are represented by <see cref="DisabledReconciliationSourcePair"/>
/// instead of being silently omitted, so the reconciliation surface always
/// reports its own full, honest coverage — which sources are live and which
/// are waiting on a real external contract. This interface (not a bespoke
/// method per source pair) is exactly the extension point a later task adds
/// a real implementation to, once the pending dependency actually ships:
/// swap the disabled instance for a real one in
/// <see cref="PaymentReconciliationModule"/>, nothing else changes.
/// </summary>
public interface IReconciliationSourcePair
{
    /// <summary>A short, stable, human-readable name for this source pair (used in scan reports, never shown raw to an end user without translation).</summary>
    string Name { get; }

    /// <summary>
    /// False when this source pair's real detection logic cannot run yet
    /// (one side depends on an external contract that doesn't exist) —
    /// <see cref="ScanAsync"/> still returns an empty, successful result in
    /// that case; a disabled source is a known, reported gap, never a
    /// silent failure.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// When disabled, the reason to surface in a scan report (why this
    /// source pair cannot run yet, and what would need to ship for it to).
    /// Null when <see cref="IsEnabled"/> is true.
    /// </summary>
    string? DisabledReason { get; }

    /// <summary>
    /// Scans both sides of this pair for divergence right now. Never
    /// throws for "nothing found" (returns an empty list) or for being
    /// disabled (also an empty list) — only for a genuine infrastructure
    /// failure (e.g. the database is unreachable), so one failing source
    /// pair does not need to abort a whole multi-source scan by itself;
    /// the caller (<see cref="PaymentReconciliationScanner"/>) isolates
    /// each source pair's own failure instead.
    /// </summary>
    Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default);
}
