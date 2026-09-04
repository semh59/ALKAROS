namespace ALKAROS.Identity.Authorization.Offline;

/// <summary>
/// The signed-in-spirit offline authority handed to a device at session start
/// (docs/domain/authorization-model.md §5). Each <see cref="Lines"/> entry is a
/// per-permission slice of the requester's own online <c>auto_within</c> policy;
/// offline, a <c>grant</c>-class action is allowed only while its line still has
/// headroom and <see cref="ExpiresAt"/> has not passed. The row is the authority
/// — the device's cached copy is advisory only.
/// </summary>
public sealed record OfflineAuthorityBudget(
    Guid BudgetId,
    Guid UserId,
    Guid SessionId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<OfflineAuthorityBudgetLine> Lines)
{
    /// <summary>True once <paramref name="instant"/> reaches or passes <see cref="ExpiresAt"/>.</summary>
    public bool IsExpiredAt(DateTimeOffset instant) => instant >= ExpiresAt;

    /// <summary>The line for <paramref name="permissionCode"/>, or null when the budget does not cover it.</summary>
    public OfflineAuthorityBudgetLine? LineFor(string permissionCode)
    {
        foreach (var line in Lines)
        {
            if (string.Equals(line.PermissionCode, permissionCode, StringComparison.Ordinal))
                return line;
        }

        return null;
    }
}

/// <summary>
/// One permission's offline headroom: at most <see cref="MaxCount"/> offline
/// grants over the budget's lifetime, each with a monetary delta no greater than
/// <see cref="LimitAmount"/> (null = count only, no monetary cap).
/// </summary>
public sealed record OfflineAuthorityBudgetLine(
    string PermissionCode,
    decimal? LimitAmount,
    int MaxCount)
{
    /// <summary>
    /// Whether an action of <paramref name="amount"/> is still within this line
    /// given <paramref name="priorGrants"/> already spent against it.
    /// </summary>
    public bool Admits(decimal amount, int priorGrants)
        => priorGrants < MaxCount
           && amount >= 0m
           && (LimitAmount is null || amount <= LimitAmount.Value);
}
