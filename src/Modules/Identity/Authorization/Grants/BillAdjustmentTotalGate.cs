using ALKAROS.Identity.Authorization.Catalog;

namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>
/// Caps what comps and discounts may take off one bill in total. Each request is judged on its own role limit, so
/// five comps of 50 lira each would all pass; this gate adds what the bill has already had taken off, by whoever and
/// by whichever path, to the new request and, above the cap, sends the request to a manager instead of approving it
/// automatically.
/// </summary>
public sealed class BillAdjustmentTotalGate : IPrePolicyGate
{
    public const string BillSubjectType = "Bill";

    private static readonly string[] CappedPermissions = [ApplicationPermissions.BillsComp, ApplicationPermissions.BillsDiscount];

    private readonly IBillAdjustmentTotals _totals;
    private readonly IBillAdjustmentTotalCap _cap;

    public BillAdjustmentTotalGate(IBillAdjustmentTotals totals, IBillAdjustmentTotalCap cap)
    {
        _totals = totals ?? throw new ArgumentNullException(nameof(totals));
        _cap = cap ?? throw new ArgumentNullException(nameof(cap));
    }

    public async Task<bool> ShouldForceEscalationAsync(
        GrantRequest request, DateTimeOffset instant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SubjectId is not { } billId
            || !string.Equals(request.SubjectType, BillSubjectType, StringComparison.Ordinal)
            || !CappedPermissions.Contains(request.PermissionCode, StringComparer.Ordinal))
            return false;

        var alreadyApplied = await _totals.GetAppliedAsync(billId, cancellationToken);
        return alreadyApplied + request.Amount > await _cap.GetAsync(cancellationToken);
    }
}
