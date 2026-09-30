using System.Text.RegularExpressions;

namespace ALKAROS.Invoicing.Generation.OrderInvoices;

/// <summary>
/// The business as it appears on its invoices (legal name, VKN or TCKN, tax office, address). One profile per
/// installation; a drafted invoice copies it, so later edits never change an existing draft.
/// </summary>
public sealed record SellerProfile(
    string LegalName,
    string TaxIdKind,
    string TaxIdNumber,
    string TaxOffice,
    string Address,
    string District,
    string City,
    string? Email)
{
    public const string Vkn = "Vkn";
    public const string Tckn = "Tckn";

    /// <summary>The fields that cannot back an invoice, by field name; empty when the profile is complete.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        Require(LegalName, nameof(LegalName), problems);
        Require(TaxOffice, nameof(TaxOffice), problems);
        Require(Address, nameof(Address), problems);
        Require(District, nameof(District), problems);
        Require(City, nameof(City), problems);
        var digits = TaxIdKind switch { Vkn => 10, Tckn => 11, _ => 0 };
        if (digits == 0 || !Regex.IsMatch(TaxIdNumber ?? "", $"^[0-9]{{{digits}}}$"))
            problems.Add(nameof(TaxIdNumber));
        if (Email is { Length: > 0 } && !Email.Contains('@'))
            problems.Add(nameof(Email));
        return problems;
    }

    private static void Require(string? value, string field, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
            problems.Add(field);
    }
}

public interface ISellerProfileStore
{
    Task<SellerProfile?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the profile. Throws <see cref="ArgumentException"/> when <see cref="SellerProfile.Problems"/> is not empty.</summary>
    Task SaveAsync(SellerProfile profile, Guid? updatedBy, CancellationToken cancellationToken = default);
}
