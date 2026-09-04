namespace ALKAROS.Identity.Authorization.Policies;

/// <summary>
/// One row of <c>identity.authorization_policies</c>: how <c>grant</c> requests
/// for <see cref="PermissionCode"/> raised by a member of <see cref="RoleCode"/>
/// are resolved. <see cref="LimitAmount"/>, <see cref="MaxCount"/> and
/// <see cref="WindowSeconds"/> are required for <see cref="PolicyMode.AutoWithin"/>
/// and null otherwise (enforced by the table's check constraint and by
/// <see cref="Validate"/>).
/// </summary>
public sealed record AuthorizationPolicy(
    Guid PolicyId,
    string PermissionCode,
    string RoleCode,
    PolicyMode Mode,
    decimal? LimitAmount,
    int? MaxCount,
    int? WindowSeconds,
    long RowVersion)
{
    /// <summary>
    /// Throws <see cref="ArgumentException"/> when the fields are internally
    /// inconsistent. Called by the repository before every write so a bad policy
    /// never reaches the database or the evaluator.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PermissionCode))
            throw new ArgumentException("PermissionCode is required.", nameof(PermissionCode));
        if (string.IsNullOrWhiteSpace(RoleCode))
            throw new ArgumentException("RoleCode is required.", nameof(RoleCode));

        if (Mode == PolicyMode.AutoWithin)
        {
            if (LimitAmount is null || MaxCount is null || WindowSeconds is null)
                throw new ArgumentException(
                    "AutoWithin requires LimitAmount, MaxCount and WindowSeconds.");
        }
        else if (LimitAmount is not null || MaxCount is not null || WindowSeconds is not null)
        {
            throw new ArgumentException(
                $"{Mode} must not carry LimitAmount, MaxCount or WindowSeconds.");
        }

        if (LimitAmount is < 0)
            throw new ArgumentException("LimitAmount must not be negative.", nameof(LimitAmount));
        if (MaxCount is < 0)
            throw new ArgumentException("MaxCount must not be negative.", nameof(MaxCount));
        if (WindowSeconds is <= 0)
            throw new ArgumentException("WindowSeconds must be positive.", nameof(WindowSeconds));
    }
}
