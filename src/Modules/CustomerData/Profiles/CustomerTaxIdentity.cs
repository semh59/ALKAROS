namespace ALKAROS.CustomerData.Profiles;

/// <summary>V1-RMD-453. A company is identified by its VKN (10 digits); a private person by their TCKN (11 digits).</summary>
public enum CustomerTaxIdKind
{
    Vkn,
    Tckn,
}

/// <summary>Why a tax identity was refused. The caller turns this into its own user-facing message.</summary>
public enum CustomerTaxIdentityError
{
    VknLength,
    VknChecksum,
    TcknLength,
    TcknChecksum,
    TaxOfficeRequired,
    TaxOfficeTooLong,
}

public sealed class InvalidCustomerTaxIdentityException(CustomerTaxIdentityError error)
    : ArgumentException($"Customer tax identity is invalid: {error}.")
{
    public CustomerTaxIdentityError Error { get; } = error;
}

/// <summary>
/// V1-RMD-453: the buyer's tax identity an invoice is issued to (Semih, 2026-09-29: it lives on the customer record).
/// Stored inside the profile's encrypted envelope with the other PII. V0-CMP-003 lists the tax ID under "Invoice data"
/// (Manager/Finance, not Cashier), so <see cref="CustomerProfileAccessPolicy"/> hands the cashier only
/// <see cref="Masked"/>.
/// </summary>
public sealed record CustomerTaxIdentity
{
    public const int TaxOfficeMaxLength = 80;

    public CustomerTaxIdKind Kind { get; }

    /// <summary>The full number, or - when <see cref="IsMasked"/> - asterisks followed by its last three digits.</summary>
    public string Number { get; }

    public string? TaxOffice { get; }

    public bool IsMasked { get; }

    private CustomerTaxIdentity(CustomerTaxIdKind kind, string number, string? taxOffice, bool isMasked)
    {
        Kind = kind;
        Number = number;
        TaxOffice = taxOffice;
        IsMasked = isMasked;
    }

    /// <summary>
    /// Validates length and the official check digits (VKN: the Revenue Administration's mod-9 weighting; TCKN: the
    /// 10th and 11th digit rules); a VKN also needs its tax office. Whitespace around the number and office is trimmed.
    /// </summary>
    public static CustomerTaxIdentity Create(CustomerTaxIdKind kind, string number, string? taxOffice)
    {
        ArgumentNullException.ThrowIfNull(number);
        var digits = number.Trim();
        var office = string.IsNullOrWhiteSpace(taxOffice) ? null : taxOffice.Trim();

        switch (kind)
        {
            case CustomerTaxIdKind.Vkn:
                if (digits.Length != 10 || !digits.All(char.IsAsciiDigit))
                    throw new InvalidCustomerTaxIdentityException(CustomerTaxIdentityError.VknLength);
                if (!IsValidVkn(digits))
                    throw new InvalidCustomerTaxIdentityException(CustomerTaxIdentityError.VknChecksum);
                if (office is null)
                    throw new InvalidCustomerTaxIdentityException(CustomerTaxIdentityError.TaxOfficeRequired);
                break;
            case CustomerTaxIdKind.Tckn:
                if (digits.Length != 11 || !digits.All(char.IsAsciiDigit) || digits[0] == '0')
                    throw new InvalidCustomerTaxIdentityException(CustomerTaxIdentityError.TcknLength);
                if (!IsValidTckn(digits))
                    throw new InvalidCustomerTaxIdentityException(CustomerTaxIdentityError.TcknChecksum);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tax identity kind.");
        }

        if (office is { Length: > TaxOfficeMaxLength })
            throw new InvalidCustomerTaxIdentityException(CustomerTaxIdentityError.TaxOfficeTooLong);

        return new CustomerTaxIdentity(kind, digits, office, isMasked: false);
    }

    /// <summary>Rebuilds a value this module validated when it was written; never used for caller input.</summary>
    internal static CustomerTaxIdentity Restore(CustomerTaxIdKind kind, string number, string? taxOffice)
        => new(kind, number, taxOffice, isMasked: false);

    public CustomerTaxIdentity Masked()
    {
        if (IsMasked)
            return this;
        var visible = Number.Length <= 3 ? string.Empty : Number[^3..];
        return new CustomerTaxIdentity(Kind, new string('*', Number.Length - visible.Length) + visible, TaxOffice, isMasked: true);
    }

    public static bool IsValidVkn(string vkn)
    {
        if (vkn.Length != 10 || !vkn.All(char.IsAsciiDigit))
            return false;

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var shifted = (vkn[i] - '0' + 9 - i) % 10;
            if (shifted == 0)
                continue;
            var weighted = shifted * (1 << (9 - i)) % 9;
            sum += weighted == 0 ? 9 : weighted;
        }

        return (10 - sum % 10) % 10 == vkn[9] - '0';
    }

    public static bool IsValidTckn(string tckn)
    {
        if (tckn.Length != 11 || !tckn.All(char.IsAsciiDigit) || tckn[0] == '0')
            return false;

        var d = tckn.Select(c => c - '0').ToArray();
        var odd = d[0] + d[2] + d[4] + d[6] + d[8];
        var even = d[1] + d[3] + d[5] + d[7];
        var tenth = ((odd * 7 - even) % 10 + 10) % 10;
        return tenth == d[9] && d.Take(10).Sum() % 10 == d[10];
    }
}
