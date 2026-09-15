using ALKAROS.Identity.Authorization.Grants;

namespace ALKAROS.Identity.Authorization.Offline;

/// <summary>
/// One grant-class action a device authorized locally against its offline budget
/// while disconnected, replayed to the Host on reconnect. Mirrors
/// <see cref="GrantRequest"/> plus the local decision instant.
/// </summary>
public sealed record OfflineAuthorizedAction(
    string IdempotencyKey,
    string PermissionCode,
    Guid RequesterUserId,
    string RequesterRoleCode,
    string ReasonCode,
    decimal Amount,
    DateTimeOffset OfflineAuthorizedAt,
    string? SubjectType = null,
    Guid? SubjectId = null,
    Guid? SubjectServingUserId = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(IdempotencyKey));
        if (string.IsNullOrWhiteSpace(PermissionCode))
            throw new ArgumentException("PermissionCode is required.", nameof(PermissionCode));
        if (RequesterUserId == Guid.Empty)
            throw new ArgumentException("RequesterUserId is required.", nameof(RequesterUserId));
        if (string.IsNullOrWhiteSpace(RequesterRoleCode))
            throw new ArgumentException("RequesterRoleCode is required.", nameof(RequesterRoleCode));
        if (string.IsNullOrWhiteSpace(ReasonCode))
            throw new ArgumentException("ReasonCode is required.", nameof(ReasonCode));
        if (Amount < 0m)
            throw new ArgumentException("Amount must not be negative.", nameof(Amount));
        if (OfflineAuthorizedAt == default)
            throw new ArgumentException("OfflineAuthorizedAt is required.", nameof(OfflineAuthorizedAt));
        if ((SubjectType is null) != (SubjectId is null))
            throw new ArgumentException("SubjectType and SubjectId must be provided together.");
    }
}

/// <summary>
/// What reconciliation decided for one <see cref="OfflineAuthorizedAction"/>.
/// <see cref="GrantStatus.Pending"/> means the grant was recorded and now waits
/// for a manager (offline_pending_review); <see cref="GrantStatus.Denied"/>
/// means the offline authorization did not hold up and the action must be
/// reversed on the device.
/// </summary>
public sealed record OfflineReconciliationResult(
    string IdempotencyKey,
    Guid GrantId,
    GrantStatus Status,
    string Detail,
    bool IsBehaviourallyFlagged,
    bool IsReplay);
