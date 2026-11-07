using ALKAROS.Identity.Authorization.Catalog;

namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>
/// Caps what comp and discount grants may take off one bill in total. Each request is judged on its own role limit, so
/// five comps of 50 lira each would all pass; this gate adds what the bill has already been granted to the new request
/// and, above the cap, sends the request to a manager instead of approving it automatically.
/// </summary>
public sealed class BillAdjustmentTotalGate : IPrePolicyGate
{
    public const string BillSubjectType = "Bill";

    private static readonly string[] CappedPermissions = [ApplicationPermissions.BillsComp, ApplicationPermissions.BillsDiscount];

    private readonly IAuthorizationGrantRepository _grants;
    private readonly IBillAdjustmentTotalCap _cap;

    public BillAdjustmentTotalGate(IAuthorizationGrantRepository grants, IBillAdjustmentTotalCap cap)
    {
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
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

        var alreadyGranted = await _grants.SumGrantedForSubjectAsync(billId, CappedPermissions, cancellationToken);
        return alreadyGranted + request.Amount > await _cap.GetAsync(cancellationToken);
    }
}
