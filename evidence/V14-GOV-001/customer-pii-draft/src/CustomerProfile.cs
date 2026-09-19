namespace ALKAROS.CustomerData.Draft;

/// <summary>
/// V14-GOV-001 DRAFT. Field set matches V0-CMP-003's own KVKK inventory
/// (`evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md`) "Customer
/// PII" row EXACTLY: name, phone, email, address — retention 10 years,
/// disposal "Anonymize after retention". Deliberately does NOT include a
/// tax identity number: that field is listed under the inventory's
/// SEPARATE "Invoice data" row (customer name, tax ID, amount — Manager/
/// Finance only, not Cashier), which is `V14-INV-002`'s own domain, not
/// this one — confirmed by re-reading the real inventory rather than
/// assuming customer identity and invoice identity share one record.
/// </summary>
public sealed record CustomerProfile(
    Guid CustomerId,
    string? Name,
    string? Phone,
    string? Email,
    string? Address,
    DateTimeOffset CreatedAt,
    bool Anonymized)
{
    /// <summary>V0-CMP-003's own "Customer PII" row: retention 10 years (tax-driven, not KVKK-minimum).</summary>
    public const int RetentionYears = 10;

    public bool IsRetentionExpired(DateTimeOffset asOf) => asOf >= CreatedAt.AddYears(RetentionYears);
}

/// <summary>
/// V0-CMP-003's "Customer PII" row grants read access to BOTH `Manager`
/// and `Cashier` — unlike "Invoice data" (Manager/Finance only), there is
/// no further field-level split within customer identity/contact fields
/// themselves. Any role outside this pair gets nothing.
/// </summary>
public enum CustomerAccessRole
{
    Cashier,
    Manager,
    Other,
}

public static class CustomerProfileAccessPolicy
{
    public static bool CanRead(CustomerAccessRole role) => role is CustomerAccessRole.Cashier or CustomerAccessRole.Manager;

    /// <summary>Returns the profile unchanged for an authorized role, or a fully-redacted stand-in otherwise — never a partial leak.</summary>
    public static CustomerProfile Project(CustomerProfile profile, CustomerAccessRole role)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return CanRead(role)
            ? profile
            : profile with { Name = null, Phone = null, Email = null, Address = null };
    }
}

/// <summary>
/// V0-CMP-003's disposal action for "Customer PII" is "Anonymize after
/// retention" (not delete) — the record's existence and id survive
/// (referential integrity for historical orders/invoices), only the
/// identifying fields are wiped.
/// </summary>
public static class CustomerProfileRetention
{
    public static CustomerProfile Anonymize(CustomerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile with { Name = null, Phone = null, Email = null, Address = null, Anonymized = true };
    }

    /// <summary>
    /// V14-CST-001's own In scope note: "Anonimleştirilmiş müşteri kaydına
    /// e-Fatura düzenlenemez" (an anonymized customer record cannot have an
    /// e-Fatura issued against it) — a guard `V14-INV-002` will need at the
    /// point it builds an invoice draft from a customer reference.
    /// </summary>
    public static void ThrowIfCannotInvoice(CustomerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Anonymized)
            throw new InvalidOperationException($"Customer {profile.CustomerId} is anonymized; an invoice cannot be issued against it.");
    }
}
