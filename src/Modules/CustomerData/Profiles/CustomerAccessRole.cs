namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// V0-CMP-003's own "Customer PII" row grants read access to BOTH `Manager`
/// and `Cashier`. The tax identity (V1-RMD-453) is "Invoice data" (Manager/
/// Finance only): the cashier sees it masked. Any role outside this pair gets
/// nothing.
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

    /// <summary>
    /// Returns the profile unchanged for the manager, with the tax number masked for the cashier, or a fully-redacted
    /// stand-in otherwise - never a partial leak.
    /// </summary>
    public static CustomerProfile Project(CustomerProfile profile, CustomerAccessRole role)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return role switch
        {
            CustomerAccessRole.Manager => profile,
            CustomerAccessRole.Cashier => profile with { TaxIdentity = profile.TaxIdentity?.Masked() },
            _ => profile with { Name = null, Phone = null, Email = null, Address = null, TaxIdentity = null },
        };
    }
}
