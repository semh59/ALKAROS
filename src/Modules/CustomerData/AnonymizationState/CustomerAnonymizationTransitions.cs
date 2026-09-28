namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>
/// V0-DOM-001's general transition contract applied to this new machine:
/// allowed transitions are explicit, forbidden transitions are rejected, no
/// wildcard edges, terminal states never reopen. Mirrors
/// `docs/domain/lifecycle-transition-contracts.md`'s own transition-matrix
/// convention (this task predates that doc's fixed entity list, so the
/// matrix lives here instead of adding a row to that already-`Done` task's
/// Owned surface).
/// </summary>
public static class CustomerAnonymizationTransitions
{
    private static readonly Dictionary<AnonymizationRequestStatus, AnonymizationRequestStatus[]> Allowed = new()
    {
        // Requested is transient (see its own doc comment) - both edges are
        // exercised inside CustomerAnonymizationService.RequestAsync's
        // single synchronous guard check, never as two separate persisted
        // steps.
        [AnonymizationRequestStatus.Requested] =
            [AnonymizationRequestStatus.RetentionBlocked, AnonymizationRequestStatus.Pending],
        // A later re-check (CustomerAnonymizationService.ReevaluateAsync)
        // can find the earlier blocker has cleared.
        [AnonymizationRequestStatus.RetentionBlocked] = [AnonymizationRequestStatus.Pending],
        [AnonymizationRequestStatus.Pending] = [AnonymizationRequestStatus.Anonymized],
        // Terminal: an anonymized request never reopens (V0-DOM-001's own
        // "terminal states cannot silently reopen" rule, same as Order's
        // Completed or Bill's Paid).
        [AnonymizationRequestStatus.Anonymized] = [],
    };

    public static bool CanTransition(AnonymizationRequestStatus from, AnonymizationRequestStatus to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;
}
