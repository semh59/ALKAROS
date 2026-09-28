namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// V0-CMP-003's own "Customer PII" row grants read access to BOTH `Manager`
/// and `Cashier` - unlike "Invoice data" (Manager/Finance only), there is
/// no further field-level split within customer identity/contact fields
/// themselves. Any role outside this pair gets nothing.
/// </summary>
public enum CustomerAccessRole
{
    Cashier,
    Manager,
    Other,
}

/// <summary>
/// Field-level access policy over a <see cref="CustomerProfile"/> already
/// read from the store - separate from (and applied on top of)
/// <see cref="CustomerProfileEncryptionPolicy"/>, which only gates whether
/// this module's own persistence layer may decrypt the envelope at all.
/// This policy decides what an already-decrypted profile then shows to the
/// CALLER's role.
/// </summary>
public static class CustomerProfileAccessPolicy
{
    public static bool CanRead(CustomerAccessRole role) => role is CustomerAccessRole.Cashier or CustomerAccessRole.Manager;

    /// <summary>Returns the profile unchanged for an authorized role, or a fully-redacted stand-in otherwise - never a partial leak.</summary>
    public static CustomerProfile Project(CustomerProfile profile, CustomerAccessRole role)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return CanRead(role)
            ? profile
            : profile with { Name = null, Phone = null, Email = null, Address = null };
    }
}
