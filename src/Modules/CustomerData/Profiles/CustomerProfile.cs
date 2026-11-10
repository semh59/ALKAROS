namespace ALKAROS.CustomerData.Profiles;

/// <summary>
/// Customer contact data: name, phone, email, address (the KVKK inventory's "Customer
/// PII" row), disposed by anonymization once its retention window ends (the Privacy
/// retention classes set the window: 2 years without an invoice, 10 years with one).
/// The buyer's tax identity is also kept here because an invoice takes it from the
/// customer record. The
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
}
