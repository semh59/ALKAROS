using ALKAROS.Cash.Contracts;

namespace ALKAROS.Cash.SessionLifecycle;

/// <summary>
/// The persisted state of a cash session: the policy-facing
/// <see cref="CashSessionSnapshot"/> (owned by V1-CSH-001, unchanged here)
/// plus the audit-trail fields (who closed/overrode/reconciled it and why)
/// that PDF:II.5.9's supervisor-override requirement needs recorded, but
/// which the snapshot contract itself does not carry.
/// </summary>
public sealed record CashSessionRecord(
    CashSessionSnapshot Snapshot,
    Guid? ClosedBy,
    bool IsSupervisorOverride,
    string? OverrideReason,
    Guid? ReconciledBy,
    string? ReconciliationNotes,
    DateTimeOffset? ReconciledAt);

/// <summary>A single physical cash count entry (PDF:III.9.3) recorded against a session.</summary>
public sealed record CashCountEntry(
    Guid Id,
    Guid CashSessionId,
    decimal CountedAmount,
    Guid CountedBy,
    string? Notes,
    DateTimeOffset CountedAt);
