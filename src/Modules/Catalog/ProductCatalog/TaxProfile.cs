namespace ALKAROS.Catalog.ProductCatalog;

/// <summary>
/// Represents a tax profile that can be assigned to products (PDF III.4.2).
/// VatRate is a percentage (e.g. 20 for 20% KDV) matching vat_rate numeric(5,2).
/// </summary>
public sealed class TaxProfile
{
    public TaxProfile(Guid id, string code, string name, decimal vatRate, bool active = true)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Tax profile code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tax profile name cannot be empty.", nameof(name));
        if (vatRate < 0 || vatRate > 100)
            throw new ArgumentOutOfRangeException(nameof(vatRate), "VAT rate must be between 0 and 100.");

        Id = id;
        Code = code;
        Name = name;
        VatRate = vatRate;
        Active = active;
    }

    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public decimal VatRate { get; }
    public bool Active { get; }
}
