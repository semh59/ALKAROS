namespace ALKAROS.Identity.Authorization.Offline;

/// <summary>
/// Reconciles the grant-class actions a device authorized offline against its
/// budget (docs/domain/authorization-model.md §5). On reconnect the device
/// replays each action; every one is written to the grant ledger — denied when
/// the offline authorization no longer holds (budget expired, over the line, or
/// the live policy now refuses it), otherwise pending for a manager
/// (offline_pending_review). Idempotent by the action's idempotency key.
/// </summary>
public interface IOfflineGrantReconciler
{
    Task<IReadOnlyList<OfflineReconciliationResult>> ReconcileAsync(
        Guid budgetId,
        IReadOnlyList<OfflineAuthorizedAction> actions,
        CancellationToken cancellationToken = default);
}

/// <summary>Raised when the replayed budget id is not a known offline authority budget.</summary>
public sealed class UnknownOfflineAuthorityBudgetException : Exception
{
    public UnknownOfflineAuthorityBudgetException(Guid budgetId)
        : base($"Offline authority budget {budgetId} is not known; the device must re-authenticate.")
    {
        BudgetId = budgetId;
    }

    public Guid BudgetId { get; }
}
