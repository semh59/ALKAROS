using System.Globalization;
using System.Text.RegularExpressions;

namespace ALKAROS.Purchasing.PurchaseInvoices;

public static partial class PurchaseInvoiceItemKey
{
    private const int MaxLength = 200;
    private static readonly CultureInfo Turkish = new("tr-TR");

    public static string For(string? supplierItemCode, string description)
    {
        var code = supplierItemCode?.Trim();
        var key = string.IsNullOrEmpty(code)
            ? "NAME:" + Whitespace().Replace(description.Trim(), " ").ToUpper(Turkish)
            : "CODE:" + code.ToUpper(Turkish);
        return key.Length <= MaxLength ? key : key[..MaxLength];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
