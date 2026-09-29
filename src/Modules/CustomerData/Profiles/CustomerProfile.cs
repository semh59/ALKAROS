namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// V14-CST-001. Field set matches V0-CMP-003's own KVKK inventory
/// (evidence/v0/compliance/V0-CMP-003/kvkk-data-inventory.md) "Customer
/// PII" row: name, phone, email, address - retention 10 years, disposal
/// "Anonymize after retention". V1-RMD-453 adds the buyer's tax identity
/// (Semih, 2026-09-29: an invoice takes it from the customer record). The
/// inventory lists the tax ID under its "Invoice data" row (Manager/Finance,
/// not Cashier), so <see cref="CustomerProfileAccessPolicy"/> masks it for the
/// cashier; it shares the envelope and the anonymization of the other fields.
/// </summary>
public sealed record CustomerProfile
{
    public Guid CustomerId { get; }
    // init (not plain get) so CustomerProfileAccessPolicy.Project's `with`
    // expression can redact these fields without a copy constructor.
    public string? Name { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public CustomerTaxIdentity? TaxIdentity { get; init; }
    public DateTimeOffset CreatedAt { get; }
    public bool Anonymized { get; }
    public int RowVersion { get; }

    /// <summary>V0-CMP-003's own "Customer PII" row: retention 10 years (tax-driven, not KVKK-minimum).</summary>
    public const int RetentionYears = 10;

    public CustomerProfile(
        Guid customerId,
        string? name,
        string? phone,
        string? email,
        string? address,
        DateTimeOffset createdAt,
        bool anonymized,
        int rowVersion,
        CustomerTaxIdentity? taxIdentity = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId must not be empty.", nameof(customerId));
        if (rowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(rowVersion), rowVersion, "Row version must be a positive integer.");

        CustomerId = customerId;
        Name = name;
        Phone = phone;
        Email = email;
        Address = address;
        TaxIdentity = taxIdentity;
        CreatedAt = createdAt;
        Anonymized = anonymized;
        RowVersion = rowVersion;
    }

    public bool IsRetentionExpired(DateTimeOffset asOf) => asOf >= CreatedAt.AddYears(RetentionYears);
}
